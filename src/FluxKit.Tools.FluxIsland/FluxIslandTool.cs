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
    private bool? _wasOnline;
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
        _clipboard = Start(_options.ShowClipboard ? _factories.Clipboard : null, s => s.Copied += OnCopied);
        _fullScreen = Start(_options.HideInFullScreen ? _factories.FullScreen : null, s => s.Changed += OnAnyChanged);
        _volume = Start(_options.ScrollForVolume ? _factories.Volume : null, _ => { });
        _wasOnline = _network?.IsOnline;
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
        Change(() =>
        {
            if (_battery.Evaluate(power) is { } alert)
            {
                _arbiter.Raise(new IslandActivity(alert) { Value = power.Percent });
            }
        });
    }

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

    private void OnCopied(object? sender, ClipboardKind kind) =>
        Change(() => _arbiter.Raise(new IslandActivity(ActivityKind.Copied) { Clipboard = kind }));

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
