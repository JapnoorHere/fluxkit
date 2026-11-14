namespace FluxKit.Tools.FluxIsland.Activities;

/// <summary>
/// The stopwatch at one moment. While it runs, <see cref="RunningSince"/> is set and the view works out the time on
/// its own, so the tool never ticks. <see cref="Laps"/> holds each lap's own length, newest first.
/// </summary>
public sealed record StopwatchSnapshot(TimerState State, TimeSpan Banked, DateTimeOffset? RunningSince, IReadOnlyList<TimeSpan> Laps)
{
    public static StopwatchSnapshot Idle { get; } = new(TimerState.Idle, TimeSpan.Zero, null, []);

    public bool IsActive => State != TimerState.Idle;

    public TimeSpan ElapsedAt(DateTimeOffset now) =>
        Banked + (State == TimerState.Running && RunningSince is { } since && now > since ? now - since : TimeSpan.Zero);
}
