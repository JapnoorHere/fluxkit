using FluxKit.Tools.FluxIsland.Abstractions;
using FluxKit.Tools.FluxIsland.Activities;
using Microsoft.Extensions.Time.Testing;

namespace FluxKit.FluxIsland.Tests;

public sealed class ActivityArbiterTests
{
    private readonly FakeTimeProvider _time = new();
    private readonly ActivityArbiter _arbiter;

    public ActivityArbiterTests() => _arbiter = new ActivityArbiter(_time);

    [Fact]
    public void Nothing_set_shows_nothing()
    {
        Assert.Null(_arbiter.Primary);
        Assert.Null(_arbiter.Secondary);
        Assert.Null(_arbiter.NextChangeAt);
    }

    [Fact]
    public void Alert_takes_over_and_then_hands_back_to_music()
    {
        _arbiter.SetLasting(ActivityKind.Media, Playing());

        _arbiter.Raise(new IslandActivity(ActivityKind.Copied));

        Assert.Equal(ActivityKind.Copied, _arbiter.Primary?.Kind);
        Assert.Equal(ActivityKind.Media, _arbiter.Secondary?.Kind);

        _time.Advance(ActivityArbiter.DurationOf(ActivityKind.Copied));
        Assert.True(_arbiter.Expire());

        Assert.Equal(ActivityKind.Media, _arbiter.Primary?.Kind);
        Assert.Null(_arbiter.Secondary);
    }

    [Fact]
    public void Dismiss_drops_the_alert_and_shows_the_next_queued_one()
    {
        _arbiter.SetLasting(ActivityKind.Media, Playing());
        _arbiter.Raise(new IslandActivity(ActivityKind.LowBattery));
        _arbiter.Raise(new IslandActivity(ActivityKind.Copied));

        Assert.True(_arbiter.Dismiss());
        Assert.Equal(ActivityKind.Copied, _arbiter.Primary?.Kind);

        Assert.True(_arbiter.Dismiss());
        Assert.Equal(ActivityKind.Media, _arbiter.Primary?.Kind);
        Assert.False(_arbiter.Dismiss());
    }

    [Fact]
    public void Swap_puts_the_bubble_in_front_until_it_ends()
    {
        _arbiter.SetLasting(ActivityKind.Media, Playing());
        _arbiter.SetLasting(ActivityKind.Timer, new IslandActivity(ActivityKind.Timer));
        Assert.Equal(ActivityKind.Timer, _arbiter.Primary?.Kind);

        Assert.True(_arbiter.Swap());

        Assert.Equal(ActivityKind.Media, _arbiter.Primary?.Kind);
        Assert.Equal(ActivityKind.Timer, _arbiter.Secondary?.Kind);

        _arbiter.SetLasting(ActivityKind.Media, null);
        Assert.Equal(ActivityKind.Timer, _arbiter.Primary?.Kind);
        Assert.False(_arbiter.Swap());
    }

    [Fact]
    public void Screenshot_outranks_a_volume_change()
    {
        _arbiter.Raise(new IslandActivity(ActivityKind.Volume));
        _arbiter.Raise(new IslandActivity(ActivityKind.Screenshot));

        Assert.Equal(ActivityKind.Screenshot, _arbiter.Primary?.Kind);
    }

    [Fact]
    public void Running_timer_outranks_music_and_music_moves_to_the_side()
    {
        _arbiter.SetLasting(ActivityKind.Media, Playing());
        _arbiter.SetLasting(ActivityKind.Timer, new IslandActivity(ActivityKind.Timer));

        Assert.Equal(ActivityKind.Timer, _arbiter.Primary?.Kind);
        Assert.Equal(ActivityKind.Media, _arbiter.Secondary?.Kind);
    }

    [Fact]
    public void Less_important_alert_waits_until_the_current_one_ends()
    {
        _arbiter.Raise(new IslandActivity(ActivityKind.LowBattery));
        _arbiter.Raise(new IslandActivity(ActivityKind.Copied));

        Assert.Equal(ActivityKind.LowBattery, _arbiter.Primary?.Kind);

        _time.Advance(ActivityArbiter.DurationOf(ActivityKind.LowBattery));
        _arbiter.Expire();

        Assert.Equal(ActivityKind.Copied, _arbiter.Primary?.Kind);
    }

    [Fact]
    public void More_important_alert_replaces_the_current_one_which_comes_back_after()
    {
        _arbiter.Raise(new IslandActivity(ActivityKind.Copied), TimeSpan.FromSeconds(10));
        _arbiter.Raise(new IslandActivity(ActivityKind.TimerDone));

        Assert.Equal(ActivityKind.TimerDone, _arbiter.Primary?.Kind);

        _time.Advance(ActivityArbiter.DurationOf(ActivityKind.TimerDone));
        _arbiter.Expire();

        Assert.Equal(ActivityKind.Copied, _arbiter.Primary?.Kind);
    }

    [Fact]
    public void Same_kind_alert_refreshes_instead_of_queueing()
    {
        _arbiter.Raise(new IslandActivity(ActivityKind.Volume) { Value = 40 });
        _time.Advance(TimeSpan.FromSeconds(1));
        _arbiter.Raise(new IslandActivity(ActivityKind.Volume) { Value = 44 });

        _time.Advance(TimeSpan.FromSeconds(1));
        _arbiter.Expire();

        Assert.Equal(44, _arbiter.Primary?.Value);

        _time.Advance(ActivityArbiter.DurationOf(ActivityKind.Volume));
        _arbiter.Expire();

        Assert.Null(_arbiter.Primary);
    }

    [Fact]
    public void Lasting_activity_with_an_end_time_runs_out()
    {
        DateTimeOffset until = _time.GetUtcNow() + TimeSpan.FromSeconds(20);
        _arbiter.SetLasting(ActivityKind.Media, Playing() with { Media = Playing().Media! with { IsPlaying = false } }, until);

        Assert.Equal(until, _arbiter.NextChangeAt);

        _time.Advance(TimeSpan.FromSeconds(20));

        Assert.True(_arbiter.Expire());
        Assert.Null(_arbiter.Primary);
    }

    [Fact]
    public void Back_online_replaces_offline_at_once_and_drops_it()
    {
        _arbiter.Raise(new IslandActivity(ActivityKind.Offline));
        _arbiter.Raise(new IslandActivity(ActivityKind.Online));

        Assert.Equal(ActivityKind.Online, _arbiter.Primary?.Kind);

        _time.Advance(ActivityArbiter.DurationOf(ActivityKind.Online));
        _arbiter.Expire();

        Assert.Null(_arbiter.Primary);
    }

    [Fact]
    public void Queue_keeps_only_the_most_important_alerts()
    {
        _arbiter.Raise(new IslandActivity(ActivityKind.TimerDone));
        _arbiter.Raise(new IslandActivity(ActivityKind.Copied));
        _arbiter.Raise(new IslandActivity(ActivityKind.Online));
        _arbiter.Raise(new IslandActivity(ActivityKind.Volume));
        _arbiter.Raise(new IslandActivity(ActivityKind.Offline));

        List<ActivityKind> shown = [];
        for (int i = 0; i < 5; i++)
        {
            shown.Add(_arbiter.Primary!.Kind);
            _time.Advance(TimeSpan.FromSeconds(10));
            _arbiter.Expire();
            if (_arbiter.Primary is null)
            {
                break;
            }
        }

        // Offline cancels the queued Online, and the queue keeps the three most important of the rest.
        Assert.Equal([ActivityKind.TimerDone, ActivityKind.Offline, ActivityKind.Volume, ActivityKind.Copied], shown);
    }

    private static IslandActivity Playing() => new(ActivityKind.Media)
    {
        Media = new MediaInfo("Song", "Artist", true, TimeSpan.Zero, TimeSpan.FromMinutes(3), DateTimeOffset.UnixEpoch, true, true, true),
    };
}
