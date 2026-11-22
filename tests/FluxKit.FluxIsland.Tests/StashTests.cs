using FluxKit.FluxIsland.Tests.Fakes;
using FluxKit.Tools.FluxIsland;
using FluxKit.Tools.FluxIsland.Abstractions;
using FluxKit.Tools.FluxIsland.Activities;
using Microsoft.Extensions.Time.Testing;
using Serilog;

namespace FluxKit.FluxIsland.Tests;

public sealed class StashTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    [Fact]
    public void Newest_first_each_window_once_and_capped()
    {
        var stash = new StashModel();
        for (int i = 0; i < StashModel.MaxItems + 3; i++)
        {
            stash.Add(new StashedWindow(i, $"Window {i}", Now));
        }

        stash.Add(new StashedWindow(5, "Again", Now));

        Assert.Equal(StashModel.MaxItems, stash.Items.Count);
        Assert.Equal("Again", stash.Items[0].Title);
        Assert.Single(stash.Items, w => w.Handle == 5);
    }

    [Fact]
    public void Closed_windows_are_pruned_and_removal_reports_it()
    {
        var stash = new StashModel();
        stash.Add(new StashedWindow(1, "a", Now));
        stash.Add(new StashedWindow(2, "b", Now));

        Assert.True(stash.Prune(handle => handle == 2));
        Assert.Equal([2L], stash.Items.Select(w => w.Handle));
        Assert.True(stash.Remove(2));
        Assert.False(stash.Remove(2));
    }

    [Fact]
    public async Task Stashing_shows_a_moment_and_lists_the_window_only_while_the_setting_is_on()
    {
        var settings = new FakeSettings();
        var view = new FakeIslandView();
        await settings.UpdateAsync<FluxIslandSettings>(FluxIslandSettings.Section, s =>
        {
            s.WindowStash = true;
            return s;
        });
        await using var tool = new FluxIslandTool(settings, _ => view, new IslandSources(), new FakeTimeProvider(), new LoggerConfiguration().CreateLogger());
        await tool.StartAsync(CancellationToken.None);

        tool.StashWindow(42, "Report.docx - Word");

        Assert.Equal(ActivityKind.Stashed, view.Last.Primary?.Kind);
        Assert.Equal("Report.docx - Word", Assert.Single(view.Last.Stash).Title);

        tool.UnstashWindow(42);
        Assert.Empty(view.Last.Stash);
    }
}
