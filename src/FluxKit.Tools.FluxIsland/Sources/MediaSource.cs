using FluxKit.Tools.FluxIsland.Abstractions;
using Serilog;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace FluxKit.Tools.FluxIsland.Sources;

/// <summary>
/// <see cref="IMediaSource"/> over the Windows media sessions (the ones the media keys control). Everything is
/// event driven. Title and artist are only shown on screen; they are never logged.
/// </summary>
internal sealed class MediaSource(ILogger log) : IMediaSource
{
    private const int MaxThumbnailBytes = 2 * 1024 * 1024;

    private readonly ILogger _log = log.ForContext<MediaSource>();
    private readonly Lock _sync = new();
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private MediaInfo? _current;
    private string? _thumbnailFor;
    private ReadOnlyMemory<byte> _thumbnail;
    private int _refreshVersion;
    private bool _disposed;

    public MediaInfo? Current => Volatile.Read(ref _current);

    public event EventHandler? Changed;

    public void Start() => _ = StartAsync();

    public void Dispose()
    {
        lock (_sync)
        {
            _disposed = true;
            if (_manager is not null)
            {
                _manager.CurrentSessionChanged -= OnCurrentSessionChanged;
            }

            Detach();
            _manager = null;
        }
    }

    public Task PlayPauseAsync() => Send(s => s.TryTogglePlayPauseAsync().AsTask());

    public Task NextAsync() => Send(s => s.TrySkipNextAsync().AsTask());

    public Task PreviousAsync() => Send(s => s.TrySkipPreviousAsync().AsTask());

    public Task SeekAsync(TimeSpan position) => Send(s => s.TryChangePlaybackPositionAsync(position.Ticks).AsTask());

    private async Task StartAsync()
    {
        try
        {
            GlobalSystemMediaTransportControlsSessionManager manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                _manager = manager;
                manager.CurrentSessionChanged += OnCurrentSessionChanged;
            }

            Attach(manager.GetCurrentSession());
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "Media sessions are unavailable");
        }
    }

    private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args) =>
        Attach(sender.GetCurrentSession());

    private void Attach(GlobalSystemMediaTransportControlsSession? session)
    {
        lock (_sync)
        {
            if (_disposed || ReferenceEquals(session, _session))
            {
                return;
            }

            Detach();
            _session = session;
            if (session is not null)
            {
                session.MediaPropertiesChanged += OnSessionChanged;
                session.PlaybackInfoChanged += OnSessionChanged;
                session.TimelinePropertiesChanged += OnSessionChanged;
            }
        }

        _ = RefreshAsync();
    }

    private void Detach()
    {
        if (_session is { } old)
        {
            old.MediaPropertiesChanged -= OnSessionChanged;
            old.PlaybackInfoChanged -= OnSessionChanged;
            old.TimelinePropertiesChanged -= OnSessionChanged;
        }

        _session = null;
    }

    private void OnSessionChanged<T>(GlobalSystemMediaTransportControlsSession sender, T args) => _ = RefreshAsync();

    private async Task RefreshAsync()
    {
        int version = Interlocked.Increment(ref _refreshVersion);
        GlobalSystemMediaTransportControlsSession? session;
        lock (_sync)
        {
            session = _session;
        }

        MediaInfo? info = null;
        try
        {
            if (session is not null)
            {
                info = await ReadAsync(session);
            }
        }
        catch (Exception ex)
        {
            // Players come and go; a session that vanished mid-read is normal.
            _log.Debug(ex, "Reading the media session failed");
        }

        // A newer refresh started while this one awaited; let it win.
        if (version != Volatile.Read(ref _refreshVersion) || Equals(info, Current))
        {
            return;
        }

        Volatile.Write(ref _current, info);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private async Task<MediaInfo?> ReadAsync(GlobalSystemMediaTransportControlsSession session)
    {
        GlobalSystemMediaTransportControlsSessionMediaProperties props = await session.TryGetMediaPropertiesAsync();
        GlobalSystemMediaTransportControlsSessionPlaybackInfo playback = session.GetPlaybackInfo();
        GlobalSystemMediaTransportControlsSessionTimelineProperties timeline = session.GetTimelineProperties();
        if (props is null || (string.IsNullOrEmpty(props.Title) && string.IsNullOrEmpty(props.Artist)))
        {
            return null;
        }

        string trackKey = $"{props.Title}\u001f{props.Artist}\u001f{props.AlbumTitle}";
        if (trackKey != _thumbnailFor)
        {
            _thumbnailFor = trackKey;
            _thumbnail = await ReadThumbnailAsync(props.Thumbnail);
        }

        GlobalSystemMediaTransportControlsSessionPlaybackControls controls = playback.Controls;
        return new MediaInfo(
            props.Title ?? string.Empty,
            string.IsNullOrEmpty(props.Artist) ? props.AlbumArtist ?? string.Empty : props.Artist,
            playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
            timeline.Position,
            timeline.EndTime - timeline.StartTime,
            timeline.LastUpdatedTime,
            controls.IsNextEnabled,
            controls.IsPreviousEnabled,
            controls.IsPlaybackPositionEnabled)
        {
            Thumbnail = _thumbnail,
        };
    }

    private static async Task<ReadOnlyMemory<byte>> ReadThumbnailAsync(IRandomAccessStreamReference? reference)
    {
        if (reference is null)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        using IRandomAccessStreamWithContentType stream = await reference.OpenReadAsync();
        if (stream.Size == 0 || stream.Size > MaxThumbnailBytes)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        var buffer = new byte[stream.Size];
        using var reader = new DataReader(stream);
        await reader.LoadAsync((uint)stream.Size);
        reader.ReadBytes(buffer);
        return buffer;
    }

    private Task Send(Func<GlobalSystemMediaTransportControlsSession, Task<bool>> command)
    {
        GlobalSystemMediaTransportControlsSession? session;
        lock (_sync)
        {
            session = _session;
        }

        return session is null ? Task.CompletedTask : command(session);
    }
}
