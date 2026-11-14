namespace FluxKit.Tools.FluxIsland.Activities;

/// <summary>
/// A countdown timer, or a focus cycle that alternates focus and break phases. It never ticks: the owner asks for
/// <see cref="DueAt"/> and calls <see cref="Advance"/> when that time comes.
/// </summary>
internal sealed class FocusTimer(TimeProvider time)
{
    private FocusTimerSnapshot _state = FocusTimerSnapshot.Idle;
    private TimeSpan _focus;
    private TimeSpan _break;

    public FocusTimerSnapshot Snapshot => _state;

    /// <summary>When the current phase ends, or null if nothing is running.</summary>
    public DateTimeOffset? DueAt => _state.State == TimerState.Running ? _state.EndsAt : null;

    public void StartCountdown(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return;
        }

        _state = Running(TimerMode.Countdown, duration, round: 0);
    }

    public void StartFocus(TimeSpan focus, TimeSpan breakLength)
    {
        _focus = focus;
        _break = breakLength;
        _state = Running(TimerMode.Focus, focus, round: 1);
    }

    public void PauseOrResume()
    {
        DateTimeOffset now = time.GetUtcNow();
        _state = _state.State switch
        {
            TimerState.Running => _state with { State = TimerState.Paused, Remaining = _state.RemainingAt(now), EndsAt = null },
            TimerState.Paused => _state with { State = TimerState.Running, EndsAt = now + _state.Remaining },
            _ => _state,
        };
    }

    public void Cancel() => _state = FocusTimerSnapshot.Idle;

    /// <summary>Adds time to the running or paused phase, such as "+1 min" when a countdown is about to end.</summary>
    public void AddTime(TimeSpan extra)
    {
        if (!_state.IsActive || extra <= TimeSpan.Zero)
        {
            return;
        }

        _state = _state with
        {
            Total = _state.Total + extra,
            Remaining = _state.Remaining + extra,
            EndsAt = _state.EndsAt + extra,
        };
    }

    /// <summary>
    /// Moves on if the current phase has ended. Returns the mode that just finished, or null if nothing ended.
    /// A countdown stops; a focus phase starts a break; a break starts the next focus round.
    /// </summary>
    public TimerMode? Advance()
    {
        if (DueAt is not { } due || time.GetUtcNow() < due)
        {
            return null;
        }

        TimerMode finished = _state.Mode;
        _state = finished switch
        {
            TimerMode.Focus => Running(TimerMode.Break, _break, _state.Round),
            TimerMode.Break => Running(TimerMode.Focus, _focus, _state.Round + 1),
            _ => FocusTimerSnapshot.Idle,
        };
        return finished;
    }

    private FocusTimerSnapshot Running(TimerMode mode, TimeSpan length, int round) =>
        new(mode, TimerState.Running, length, length, time.GetUtcNow() + length, round);
}
