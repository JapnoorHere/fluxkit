using FluxKit.Tools.FluxIsland.Activities;
using Microsoft.Extensions.Time.Testing;

namespace FluxKit.FluxIsland.Tests;

public sealed class FocusTimerTests
{
    private readonly FakeTimeProvider _time = new();
    private readonly FocusTimer _timer;

    public FocusTimerTests() => _timer = new FocusTimer(_time);

    [Fact]
    public void Countdown_finishes_and_goes_idle()
    {
        _timer.StartCountdown(TimeSpan.FromMinutes(5));

        _time.Advance(TimeSpan.FromMinutes(4));
        Assert.Null(_timer.Advance());
        Assert.Equal(TimeSpan.FromMinutes(1), _timer.Snapshot.RemainingAt(_time.GetUtcNow()));

        _time.Advance(TimeSpan.FromMinutes(1));

        Assert.Equal(TimerMode.Countdown, _timer.Advance());
        Assert.False(_timer.Snapshot.IsActive);
        Assert.Null(_timer.DueAt);
    }

    [Fact]
    public void Pause_keeps_the_time_left_and_resume_continues_from_it()
    {
        _timer.StartCountdown(TimeSpan.FromMinutes(10));
        _time.Advance(TimeSpan.FromMinutes(3));

        _timer.PauseOrResume();
        _time.Advance(TimeSpan.FromHours(1));

        Assert.Equal(TimerState.Paused, _timer.Snapshot.State);
        Assert.Equal(TimeSpan.FromMinutes(7), _timer.Snapshot.RemainingAt(_time.GetUtcNow()));
        Assert.Null(_timer.DueAt);

        _timer.PauseOrResume();

        Assert.Equal(_time.GetUtcNow() + TimeSpan.FromMinutes(7), _timer.DueAt);
    }

    [Fact]
    public void Focus_cycle_alternates_focus_and_break_and_counts_rounds()
    {
        _timer.StartFocus(TimeSpan.FromMinutes(25), TimeSpan.FromMinutes(5));

        _time.Advance(TimeSpan.FromMinutes(25));
        Assert.Equal(TimerMode.Focus, _timer.Advance());
        Assert.Equal(TimerMode.Break, _timer.Snapshot.Mode);
        Assert.Equal(1, _timer.Snapshot.Round);

        _time.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(TimerMode.Break, _timer.Advance());
        Assert.Equal(TimerMode.Focus, _timer.Snapshot.Mode);
        Assert.Equal(2, _timer.Snapshot.Round);
    }

    [Fact]
    public void Progress_runs_from_zero_to_one()
    {
        _timer.StartCountdown(TimeSpan.FromMinutes(4));

        Assert.Equal(0, _timer.Snapshot.ProgressAt(_time.GetUtcNow()));
        _time.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(0.25, _timer.Snapshot.ProgressAt(_time.GetUtcNow()), 3);
    }

    [Fact]
    public void Cancel_stops_everything()
    {
        _timer.StartFocus(TimeSpan.FromMinutes(25), TimeSpan.FromMinutes(5));

        _timer.Cancel();

        Assert.False(_timer.Snapshot.IsActive);
        Assert.Null(_timer.DueAt);
    }

    [Fact]
    public void Zero_length_countdown_is_ignored()
    {
        _timer.StartCountdown(TimeSpan.Zero);

        Assert.False(_timer.Snapshot.IsActive);
    }
}
