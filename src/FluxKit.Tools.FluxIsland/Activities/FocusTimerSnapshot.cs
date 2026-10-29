namespace FluxKit.Tools.FluxIsland.Activities;

public enum TimerMode
{
    Countdown,
    Focus,
    Break,
}

public enum TimerState
{
    Idle,
    Running,
    Paused,
}

/// <summary>
/// The timer at one moment. While it runs, <see cref="EndsAt"/> is set and the view counts down on its own, so the
/// tool doesn't need to wake every second.
/// </summary>
public sealed record FocusTimerSnapshot(TimerMode Mode, TimerState State, TimeSpan Total, TimeSpan Remaining, DateTimeOffset? EndsAt, int Round)
{
    public static FocusTimerSnapshot Idle { get; } = new(TimerMode.Countdown, TimerState.Idle, TimeSpan.Zero, TimeSpan.Zero, null, 0);

    public bool IsActive => State != TimerState.Idle;

    public TimeSpan RemainingAt(DateTimeOffset now)
    {
        TimeSpan left = EndsAt is { } end && State == TimerState.Running ? end - now : Remaining;
        return left < TimeSpan.Zero ? TimeSpan.Zero : left;
    }

    /// <summary>How much of the current phase has passed, from 0 to 1.</summary>
    public double ProgressAt(DateTimeOffset now) =>
        Total <= TimeSpan.Zero ? 0 : Math.Clamp(1 - (RemainingAt(now) / Total), 0, 1);
}
