namespace FluxKit.Tools.FluxIsland.Activities;

/// <summary>A stopwatch with laps. Like the timer it never ticks; it only remembers when it started.</summary>
internal sealed class StopwatchModel(TimeProvider time)
{
    public const int MaxLaps = 20;

    private StopwatchSnapshot _state = StopwatchSnapshot.Idle;
    private TimeSpan _lapStart;

    public StopwatchSnapshot Snapshot => _state;

    public void Start()
    {
        if (_state.State == TimerState.Idle)
        {
            _lapStart = TimeSpan.Zero;
            _state = new StopwatchSnapshot(TimerState.Running, TimeSpan.Zero, time.GetUtcNow(), []);
        }
    }

    public void PauseOrResume()
    {
        DateTimeOffset now = time.GetUtcNow();
        _state = _state.State switch
        {
            TimerState.Running => _state with { State = TimerState.Paused, Banked = _state.ElapsedAt(now), RunningSince = null },
            TimerState.Paused => _state with { State = TimerState.Running, RunningSince = now },
            _ => _state,
        };
    }

    /// <summary>Ends the current lap and starts the next. Only while running.</summary>
    public void Lap()
    {
        if (_state.State != TimerState.Running)
        {
            return;
        }

        TimeSpan elapsed = _state.ElapsedAt(time.GetUtcNow());
        TimeSpan lap = elapsed - _lapStart;
        _lapStart = elapsed;
        _state = _state with { Laps = [lap, .. _state.Laps.Take(MaxLaps - 1)] };
    }

    public void Reset()
    {
        _lapStart = TimeSpan.Zero;
        _state = StopwatchSnapshot.Idle;
    }
}
