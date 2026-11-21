using FluxKit.FluxIsland.Tests.Fakes;
using FluxKit.Tools.FluxIsland;
using FluxKit.Tools.FluxIsland.Abstractions;
using FluxKit.Tools.FluxIsland.Activities;
using Microsoft.Extensions.Time.Testing;
using Serilog;

namespace FluxKit.FluxIsland.Tests;

public sealed class GestureTests
{
    [Theory]
    [InlineData(2, 3, 0, 0, Flick.None)]
    [InlineData(0, 12, 0, 0.1, Flick.None)]
    [InlineData(0, 40, 0, 0.1, Flick.Down)]
    [InlineData(0, -14, 0, -0.9, Flick.Up)]
    [InlineData(45, 6, 0.2, 0, Flick.Side)]
    [InlineData(-20, 2, -1.2, 0, Flick.Side)]
    [InlineData(0, 30, 0, -0.8, Flick.Up)]
    public void Flicks_are_told_from_taps_and_gentle_drags(double dx, double dy, double vx, double vy, Flick expected) =>
        Assert.Equal(expected, FlickRules.Classify(dx, dy, vx, vy));

    [Fact]
    public void Snoozed_moments_come_back_when_due_oldest_first()
    {
        var now = DateTimeOffset.UnixEpoch;
        var snoozed = new SnoozedMoments();
        snoozed.Add(new IslandActivity(ActivityKind.Copied) { Text = "b" }, now.AddMinutes(10));
        snoozed.Add(new IslandActivity(ActivityKind.Copied) { Text = "a" }, now.AddMinutes(5));

        Assert.Equal(now.AddMinutes(5), snoozed.NextDueAt);
        Assert.Empty(snoozed.TakeDue(now.AddMinutes(1)));
        Assert.Equal(["a", "b"], snoozed.TakeDue(now.AddMinutes(11)).Select(m => m.Text));
        Assert.Null(snoozed.NextDueAt);
    }

    [Fact]
    public void Scrubbing_maps_scroll_to_moments_and_resists_past_the_ends()
    {
        var scrub = new ScrubModel(3, step: 60);
        Assert.Equal(0, scrub.Index);

        scrub.Scroll(70);
        Assert.Equal(1, scrub.Index);

        scrub.Scroll(300);
        Assert.Equal(2, scrub.Index);
        Assert.True(scrub.Overscroll > 0 && scrub.Overscroll <= 60);

        scrub.Settle();
        Assert.Equal(0, scrub.Overscroll);

        scrub.Scroll(-1000);
        Assert.Equal(0, scrub.Index);
        Assert.True(scrub.Overscroll < 0);
        Assert.Equal(-1, new ScrubModel(0).Index);
    }

    [Fact]
    public async Task A_snoozed_alert_leaves_and_returns_after_the_delay()
    {
        var time = new FakeTimeProvider();
        var view = new FakeIslandView();
        var clipboard = new FakeClipboard();
        await using var tool = new FluxIslandTool(new FakeSettings(), _ => view, new IslandSources { Clipboard = _ => clipboard }, time, new LoggerConfiguration().CreateLogger());
        await tool.StartAsync(CancellationToken.None);
        clipboard.Copy(ClipboardKind.Text);
        Assert.Equal(ActivityKind.Copied, view.Last.Primary?.Kind);

        tool.SnoozeAlert();
        Assert.Null(view.Last.Primary);

        time.Advance(SnoozedMoments.Delay + TimeSpan.FromSeconds(1));
        Assert.Equal(ActivityKind.Copied, view.Last.Primary?.Kind);
    }
}
