using FluxKit.Core.Settings;
using FluxKit.Core.Tools;
using FluxKit.Tools.FluxIsland.Abstractions;
using FluxKit.Tools.FluxIsland.Activities;
using Serilog;

namespace FluxKit.Tools.FluxIsland;

/// <summary>
/// The Flux Island tool. It listens to system events, decides what the island shows through the
/// <see cref="ActivityArbiter"/>, and hands a <see cref="IslandScene"/> to the view. Nothing here polls: the only timer
/// is one-shot, set for the next moment something runs out.
/// </summary>
public sealed class FluxIslandTool : IFluxTool, IIslandCommands
{
    public const string Id = "flux-island";

    /// <summary>How long paused music stays on the pill before it steps aside.</summary>
    internal static readonly TimeSpan PausedMediaLinger = TimeSpan.FromSeconds(20);

    /// <summary>How long "Microphone muted/on" shows after the mute changes.</summary>
    internal static readonly TimeSpan MicToggleDuration = TimeSpan.FromSeconds(1.6);

    /// <summary>A download that hasn't grown for this long is taken off the island (the browser may have stopped it).</summary>
    internal static readonly TimeSpan StalledDownload = TimeSpan.FromMinutes(2);

    /// <summary>A copied link, address, number or colour stays a little longer than a plain copy, so its button can be reached.</summary>
    internal static readonly TimeSpan SmartCopyDuration = TimeSpan.FromSeconds(4);

    private readonly ISettingsService _settings;
    private readonly Func<IIslandCommands, IIslandView> _viewFactory;
    private readonly IslandSources _factories;
    private readonly TimeProvider _time;
    private readonly ILogger _log;
    private readonly Lock _sync = new();
    private readonly ActivityArbiter _arbiter;
    private readonly FocusTimer _timer;
    private readonly ShelfModel _shelf = new();
    private readonly BatteryAlertPolicy _battery = new();
    private readonly List<IIslandSource> _active = [];
    private readonly Dictionary<string, (DownloadUpdate Update, DateTimeOffset At)> _downloads = [];
    private FluxIslandSettings _options = new();
    private IIslandView? _view;
    private ITimer? _wake;
    private IMediaSource? _media;
    private IPowerSource? _power;
    private INetworkSource? _network;
    private IPrivacySource? _privacy;
    private IClipboardSource? _clipboard;
    private IFullScreenSource? _fullScreen;
    private IVolumeControl? _volume;
    private IAudioSource? _audio;
    private IDesktopSource? _desktops;
    private IFileMomentsSource? _files;
    private IBrightnessSource? _brightness;
    private bool _micMuted;
    private bool? _wasOnline;
    private bool? _saverWasOn;
    private bool _mediaWasPlaying;

    public FluxIslandTool(ISettingsService settings, Func<IIslandCommands, IIslandView> viewFactory, IslandSources sources, TimeProvider time, ILogger log)
    {
        _settings = settings;
        _viewFactory = viewFactory;
        _factories = sources;
        _time = time;
        _log = log.ForContext<FluxIslandTool>();
        _arbiter = new ActivityArbiter(time);
        _timer = new FocusTimer(time);
        _settings.SettingsChanged += OnSettingsChanged;
    }

    public ToolDescriptor Descriptor { get; } = new(
        Id,
        "Flux Island",
        "A live pill at the top of the screen for music, timers and quick status.",
        "",
        20,
        "FluxIsland.png");

    public ToolState State { get; private set; } = ToolState.Disabled;

    public event EventHandler<ToolState>? StateChanged;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_view is not null)
        {
            return Task.CompletedTask;
        }

        SetState(ToolState.Starting);
        try
        {
            _options = _settings.Read<FluxIslandSettings>(FluxIslandSettings.Section);
            _wake = _time.CreateTimer(_ => OnWake(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _view = _viewFactory(this);
            StartSources();
            Publish();
            SetState(ToolState.Running);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Flux Island failed to start");
            SetState(new ToolState(ToolStatus.Faulted, "Flux Island couldn't start. Open the log folder for details."));
            _ = StopAsync(CancellationToken.None);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        StopSources();
        _wake?.Dispose();
        _wake = null;
        _view?.Close();
        _view = null;
        SetState(ToolState.Disabled);
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _settings.SettingsChanged -= OnSettingsChanged;
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
    }

    public void PlayPause() => Run(_media?.PlayPauseAsync(), "play/pause");

    public void NextTrack() => Run(_media?.NextAsync(), "next track");

    public void PreviousTrack() => Run(_media?.PreviousAsync(), "previous track");

    public void Seek(TimeSpan position) => Run(_media?.SeekAsync(position), "seek");

    public void StartTimer(TimeSpan duration) => Change(() => _timer.StartCountdown(duration));

    public void StartFocus() => Change(() => _timer.StartFocus(TimeSpan.FromMinutes(_options.FocusMinutes), TimeSpan.FromMinutes(_options.BreakMinutes)));

    public void PauseOrResumeTimer() => Change(_timer.PauseOrResume);

    public void CancelTimer() => Change(_timer.Cancel);

    public void AddToShelf(IEnumerable<string> paths) => Change(() => _shelf.Add(paths));

    public void RemoveFromShelf(string path) => Change(() => _shelf.Remove(path));

    public void ClearShelf() => Change(() => _shelf.Clear());

    public void StepVolume(int notches)
    {
        if (_volume is null || !_options.ScrollForVolume || notches == 0)
        {
            return;
        }

        try
        {
            _volume.ChangeBy(notches);
            Change(() => _arbiter.Raise(new IslandActivity(ActivityKind.Volume) { Value = _volume.IsMuted ? 0 : _volume.Level }));
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Changing the volume failed");
        }
    }

    public void ToggleMicMute()
    {
        if (_audio is null)
        {
            return;
        }

        try
        {
            _audio.SetMicMuted(!_audio.MicMuted);
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Muting the microphone failed");
        }
    }

    public void DismissAlert() => Change(() => _arbiter.Dismiss());

    public void SwapActivities() => Change(() => _arbiter.Swap());

    /// <summary>Builds the scene from the current state. Internal so tests can check what the view would get.</summary>
    internal IslandScene BuildScene()
    {
        lock (_sync)
        {
            SyncLastingActivities();
            return new IslandScene
            {
                Primary = _arbiter.Primary,
                Secondary = _arbiter.Secondary,
                Media = _options.ShowMedia ? _media?.Current : null,
                Timer = _timer.Snapshot,
                Shelf = _shelf.Items,
                Power = _power?.Current ?? default,
                Privacy = _options.ShowPrivacy ? _privacy?.Current ?? default : default,
                MicMuted = _micMuted,
                Hidden = _options.HideInFullScreen && _fullScreen?.IsFullScreen == true,
                Settings = _options,
            };
        }
    }

    private void StartSources()
    {
        _media = Start(_options.ShowMedia ? _factories.Media : null, s => s.Changed += OnMediaChanged);
        _power = Start(_options.ShowPower ? _factories.Power : null, s => s.Changed += OnPowerChanged);
        _network = Start(_options.ShowNetwork ? _factories.Network : null, s => s.Changed += OnNetworkChanged);
        _privacy = Start(_options.ShowPrivacy ? _factories.Privacy : null, s => s.Changed += OnAnyChanged);
        Func<IClipboardSource>? clipboard = _options.ShowClipboard && _factories.Clipboard is { } makeClipboard ? () => makeClipboard(_options.SmartCopy) : null;
        _clipboard = Start(clipboard, s => s.Copied += OnCopied);
        _fullScreen = Start(_options.HideInFullScreen ? _factories.FullScreen : null, s => s.Changed += OnAnyChanged);
        _volume = Start(_options.ScrollForVolume ? _factories.Volume : null, _ => { });
        bool listenToAudio = _options.ShowVolumeChanges || _options.ShowAudioDevices || _options.ShowMicMute;
        _audio = Start(listenToAudio ? _factories.Audio : null, s =>
        {
            s.VolumeChanged += OnVolumeChanged;
            s.OutputChanged += OnOutputChanged;
            s.MicMuteChanged += OnMicMuteChanged;
        });
        _micMuted = _audio?.MicMuted == true;
        _desktops = Start(_options.ShowDesktopSwitches ? _factories.Desktops : null, s => s.Switched += OnDesktopSwitched);
        Func<IFileMomentsSource>? files = _factories.Files is { } makeFiles && (_options.ShowDownloads || _options.ShowScreenshots)
            ? () => makeFiles(_options.ShowDownloads, _options.ShowScreenshots)
            : null;
        _brightness = Start(_options.ShowBrightness ? _factories.Brightness : null, s => s.Changed += OnBrightnessChanged);
        _files = Start(files, s =>
        {
            s.DownloadChanged += OnDownloadChanged;
            s.ScreenshotSaved += OnScreenshotSaved;
        });
        _wasOnline = _network?.IsOnline;
        _saverWasOn = _power?.Current.SaverOn;
        lock (_sync)
        {
            _battery.Evaluate(_power?.Current ?? default);
        }
    }

    private T? Start<T>(Func<T>? factory, Action<T> subscribe) where T : class, IIslandSource
    {
        if (factory is null)
        {
            return null;
        }

        T? source = null;
        try
        {
            source = factory();
            subscribe(source);
            source.Start();
            _active.Add(source);
            return source;
        }
        catch (Exception ex)
        {
            // One feature failing (say, no audio device) must not take the island down.
            _log.Warning(ex, "Flux Island source {Source} is unavailable", typeof(T).Name);
            source?.Dispose();
            return null;
        }
    }

    private void StopSources()
    {
        foreach (IIslandSource source in _active)
        {
            try
            {
                source.Dispose();
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Stopping Flux Island source {Source} failed", source.GetType().Name);
            }
        }

        _active.Clear();
        _media = null;
        _power = null;
        _network = null;
        _privacy = null;
        _clipboard = null;
        _fullScreen = null;
        _volume = null;
        _audio = null;
        _desktops = null;
        _files = null;
        _brightness = null;
        lock (_sync)
        {
            _arbiter.SetLasting(ActivityKind.Media, null);
        }
    }

    private void OnSettingsChanged(object? sender, string section)
    {
        if (section != FluxIslandSettings.Section || _view is null)
        {
            return;
        }

        _options = _settings.Read<FluxIslandSettings>(FluxIslandSettings.Section);
        StopSources();
        StartSources();
        Publish();
    }

    private void OnMediaChanged(object? sender, EventArgs e) => Publish();

    private void OnAnyChanged(object? sender, EventArgs e) => Publish();

    private void OnPowerChanged(object? sender, EventArgs e)
    {
        PowerSnapshot power = _power?.Current ?? default;
        int minutesLeft = power.TimeLeft is { } left ? (int)Math.Ceiling(left.TotalMinutes) : 0;
        Change(() =>
        {
            if (_battery.Evaluate(power) is { } alert)
            {
                _arbiter.Raise(new IslandActivity(alert) { Value = power.Percent, Total = minutesLeft });
            }

            if (_options.ShowBatterySaver && power.HasBattery && _saverWasOn is { } was && was != power.SaverOn)
            {
                _arbiter.Raise(new IslandActivity(ActivityKind.BatterySaver) { Flag = power.SaverOn, Value = power.Percent, Total = minutesLeft });
            }

            _saverWasOn = power.SaverOn;
        });
    }

    private void OnBrightnessChanged(object? sender, int level) =>
        Change(() => _arbiter.Raise(new IslandActivity(ActivityKind.Brightness) { Value = level }));

    private void OnNetworkChanged(object? sender, EventArgs e)
    {
        bool online = _network?.IsOnline ?? true;
        Change(() =>
        {
            if (_wasOnline is { } was && was != online)
            {
                _arbiter.Raise(new IslandActivity(online ? ActivityKind.Online : ActivityKind.Offline));
            }

            _wasOnline = online;
        });
    }

    private void OnVolumeChanged(object? sender, AudioLevel level)
    {
        if (_options.ShowVolumeChanges)
        {
            Change(() => _arbiter.Raise(new IslandActivity(ActivityKind.Volume) { Value = level.Muted ? 0 : level.Level }));
        }
    }

    private void OnOutputChanged(object? sender, string name)
    {
        if (_options.ShowAudioDevices && !string.IsNullOrWhiteSpace(name))
        {
            Change(() => _arbiter.Raise(new IslandActivity(ActivityKind.AudioDevice) { Text = name }));
        }
    }

    private void OnMicMuteChanged(object? sender, bool muted)
    {
        Change(() =>
        {
            _micMuted = muted;
            if (_options.ShowMicMute)
            {
                _arbiter.Raise(new IslandActivity(ActivityKind.MicMuted) { Flag = muted }, MicToggleDuration);
            }
        });
    }

    private void OnDesktopSwitched(object? sender, DesktopLanding landing) =>
        Change(() => _arbiter.Raise(new IslandActivity(ActivityKind.DesktopSwitched) { Value = landing.Number, Total = landing.Count, Text = landing.Name }));

    private void OnDownloadChanged(object? sender, DownloadUpdate update)
    {
        Change(() =>
        {
            if (update.Previous is { } previous)
            {
                _downloads.Remove(previous);
            }

            switch (update.Stage)
            {
                case DownloadStage.Arriving:
                    _downloads[update.Key] = (update, _time.GetUtcNow());
                    break;
                case DownloadStage.Finished:
                    _downloads.Remove(update.Key);
                    _arbiter.Raise(new IslandActivity(ActivityKind.DownloadDone) { Text = update.Name, Path = update.Path });
                    break;
                default:
                    _downloads.Remove(update.Key);
                    break;
            }
        });
    }

    private void OnScreenshotSaved(object? sender, string path) =>
        Change(() => _arbiter.Raise(new IslandActivity(ActivityKind.Screenshot) { Text = Path.GetFileName(path), Path = path }));

    private void OnCopied(object? sender, ClipboardCopied copy)
    {
        var moment = new IslandActivity(ActivityKind.Copied) { Clipboard = copy.Kind };
        if (_options.SmartCopy && copy.Text is { } text)
        {
            // Looked at once, here, and dropped with the moment. Never logged.
            (SmartText kind, string display, string? target, int rgb) = CopiedTextClassifier.Classify(text);
            moment = moment with { Smart = kind, Text = display.Length > 0 ? display : null, Path = target, Value = kind == SmartText.Color ? rgb : text.Trim().Length };
        }

        Change(() => _arbiter.Raise(moment, moment.Smart is SmartText.Link or SmartText.Email or SmartText.Color or SmartText.Phone ? SmartCopyDuration : ActivityArbiter.DurationOf(ActivityKind.Copied)));
    }

    private void OnWake()
    {
        Change(() =>
        {
            if (_timer.Advance() is { } finished)
            {
                _arbiter.Raise(new IslandActivity(ActivityKind.TimerDone) { Timer = _timer.Snapshot with { Mode = finished } });
            }

            _arbiter.Expire();
        });
    }

    /// <summary>Applies a change under the lock, then publishes the new scene.</summary>
    private void Change(Action change)
    {
        lock (_sync)
        {
            change();
        }

        Publish();
    }

    private void Publish()
    {
        IIslandView? view = _view;
        if (view is null)
        {
            return;
        }

        IslandScene scene = BuildScene();
        Schedule();
        view.Render(scene);
    }

    /// <summary>Keeps the lasting activities (music, timer, shelf) in step with their sources.</summary>
    private void SyncLastingActivities()
    {
        MediaInfo? media = _options.ShowMedia ? _media?.Current : null;
        if (media is null)
        {
            _arbiter.SetLasting(ActivityKind.Media, null);
        }
        else if (media.IsPlaying)
        {
            _arbiter.SetLasting(ActivityKind.Media, new IslandActivity(ActivityKind.Media) { Media = media });
        }
        else if (_mediaWasPlaying)
        {
            // Just paused: stay for a while, then step aside.
            _arbiter.SetLasting(ActivityKind.Media, new IslandActivity(ActivityKind.Media) { Media = media }, _time.GetUtcNow() + PausedMediaLinger);
        }

        _mediaWasPlaying = media?.IsPlaying == true;

        FocusTimerSnapshot timer = _timer.Snapshot;
        _arbiter.SetLasting(ActivityKind.Timer, timer.IsActive ? new IslandActivity(ActivityKind.Timer) { Timer = timer } : null);
        _arbiter.SetLasting(ActivityKind.Shelf, _options.ShelfEnabled && _shelf.Count > 0 ? new IslandActivity(ActivityKind.Shelf) { Value = _shelf.Count } : null);

        // A muted microphone matters while something is listening, such as a call: keep reminding until it ends.
        bool listening = _privacy?.Current.MicrophoneInUse == true;
        _arbiter.SetLasting(ActivityKind.MicMuted, _options.ShowMicMute && _micMuted && listening ? new IslandActivity(ActivityKind.MicMuted) { Flag = true } : null);

        // The newest download that is still arriving; one that stalls drops off by itself.
        DateTimeOffset now = _time.GetUtcNow();
        foreach (string stalled in _downloads.Where(d => now - d.Value.At >= StalledDownload).Select(d => d.Key).ToList())
        {
            _downloads.Remove(stalled);
        }

        if (_downloads.Values.OrderByDescending(d => d.At).FirstOrDefault() is { Update.Key: not null } latest)
        {
            _arbiter.SetLasting(
                ActivityKind.Download,
                new IslandActivity(ActivityKind.Download)
                {
                    Text = latest.Update.Name.Length > 0 ? latest.Update.Name : null,
                    Value = (int)(latest.Update.Bytes / (1024 * 1024)),
                    Total = _downloads.Count,
                },
                latest.At + StalledDownload);
        }
        else
        {
            _arbiter.SetLasting(ActivityKind.Download, null);
        }
    }

    private void Schedule()
    {
        DateTimeOffset? next;
        lock (_sync)
        {
            next = new[] { _arbiter.NextChangeAt, _timer.DueAt }.Where(t => t is not null).Min();
        }

        TimeSpan due = next is { } at ? at - _time.GetUtcNow() : Timeout.InfiniteTimeSpan;
        if (due != Timeout.InfiniteTimeSpan && due < TimeSpan.Zero)
        {
            due = TimeSpan.Zero;
        }

        _wake?.Change(due, Timeout.InfiniteTimeSpan);
    }

    private void Run(Task? task, string action)
    {
        if (task is null)
        {
            return;
        }

        _ = task.ContinueWith(t => _log.Warning(t.Exception, "Media {Action} failed", action), CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
    }

    private void SetState(ToolState state)
    {
        State = state;
        StateChanged?.Invoke(this, state);
    }
}
