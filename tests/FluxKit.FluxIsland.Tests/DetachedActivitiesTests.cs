using FluxKit.FluxIsland.Tests.Fakes;
using FluxKit.Tools.FluxIsland;
using FluxKit.Tools.FluxIsland.Abstractions;
using FluxKit.Tools.FluxIsland.Activities;
using Microsoft.Extensions.Time.Testing;
using Serilog;

namespace FluxKit.FluxIsland.Tests;

public sealed class DetachedActivitiesTests
{
    [Fact]
    public void Only_lasting_activities_tear_off_and_moves_and_docks_report_changes()
    {
        var detached = new DetachedActivities();

        Assert.False(detached.Detach(ActivityKind.Copied, 1, 2));
        Assert.True(detached.Detach(ActivityKind.Timer, 100, 200));
        Assert.False(detached.Move(ActivityKind.Timer, 100, 200));
        Assert.True(detached.Move(ActivityKind.Timer, 140, 210));
        Assert.Equal(new DropletPlace(ActivityKind.Timer, 140, 210), Assert.Single(detached.Places));
        Assert.False(detached.Move(ActivityKind.Media, 1, 1));
        Assert.True(detached.Dock(ActivityKind.Timer));
        Assert.False(detached.Dock(ActivityKind.Timer));
    }

    [Fact]
    public void Restore_ignores_kinds_that_cannot_be_detached()
    {
        var detached = new DetachedActivities();
        detached.Restore([new DropletPlace(ActivityKind.Media, 5, 5), new DropletPlace(ActivityKind.Volume, 1, 1)]);

        Assert.Equal([ActivityKind.Media], detached.Places.Select(p => p.Kind));
    }

    [Fact]
    public async Task A_torn_off_timer_leaves_the_pill_is_saved_and_docks_itself_when_it_ends()
    {
        var time = new FakeTimeProvider();
        var settings = new FakeSettings();
        var view = new FakeIslandView();
        await using var tool = new FluxIslandTool(settings, _ => view, new IslandSources(), time, new LoggerConfiguration().CreateLogger());
        await tool.StartAsync(CancellationToken.None);
        tool.StartTimer(TimeSpan.FromMinutes(5));
        Assert.Equal(ActivityKind.Timer, view.Last.Primary?.Kind);

        tool.DetachActivity(ActivityKind.Timer, 300, 400);

        Assert.NotEqual(ActivityKind.Timer, view.Last.Primary?.Kind);
        Assert.Equal(new DropletPlace(ActivityKind.Timer, 300, 400), Assert.Single(view.Last.Droplets));
        Assert.True(view.Last.Timer?.IsActive);
        Assert.Single(settings.Read<FluxIslandSettings>(FluxIslandSettings.Section).Droplets);

        tool.CancelTimer();

        Assert.Empty(view.Last.Droplets);
        Assert.Empty(settings.Read<FluxIslandSettings>(FluxIslandSettings.Section).Droplets);
    }

    [Fact]
    public async Task Docking_puts_the_activity_back_on_the_pill()
    {
        var time = new FakeTimeProvider();
        var view = new FakeIslandView();
        await using var tool = new FluxIslandTool(new FakeSettings(), _ => view, new IslandSources(), time, new LoggerConfiguration().CreateLogger());
        await tool.StartAsync(CancellationToken.None);
        tool.StartTimer(TimeSpan.FromMinutes(5));
        tool.DetachActivity(ActivityKind.Timer, 1, 1);

        tool.DockActivity(ActivityKind.Timer);

        Assert.Equal(ActivityKind.Timer, view.Last.Primary?.Kind);
        Assert.Empty(view.Last.Droplets);
    }
}
