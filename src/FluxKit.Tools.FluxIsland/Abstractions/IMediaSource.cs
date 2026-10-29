namespace FluxKit.Tools.FluxIsland.Abstractions;

/// <summary>The media session Windows considers current (the one the media keys control).</summary>
public interface IMediaSource : IIslandSource
{
    MediaInfo? Current { get; }

    event EventHandler? Changed;

    Task PlayPauseAsync();

    Task NextAsync();

    Task PreviousAsync();

    Task SeekAsync(TimeSpan position);
}

/// <summary>What is playing. <see cref="Thumbnail"/> holds encoded image bytes (PNG or JPEG), or nothing.</summary>
public sealed record MediaInfo(
    string Title,
    string Artist,
    bool IsPlaying,
    TimeSpan Position,
    TimeSpan Duration,
    DateTimeOffset PositionUpdatedAt,
    bool CanGoNext,
    bool CanGoPrevious,
    bool CanSeek)
{
    public ReadOnlyMemory<byte> Thumbnail { get; init; }

    /// <summary>Where playback is now: the last reported position plus the time since, while playing.</summary>
    public TimeSpan EstimatedPosition(DateTimeOffset now)
    {
        TimeSpan position = IsPlaying ? Position + (now - PositionUpdatedAt) : Position;
        if (position < TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        return Duration > TimeSpan.Zero && position > Duration ? Duration : position;
    }
}
