using FluxKit.FluxIsland.Tests.Fakes;
using FluxKit.Tools.FluxIsland;
using FluxKit.Tools.FluxIsland.Abstractions;
using FluxKit.Tools.FluxIsland.Activities;
using Microsoft.Extensions.Time.Testing;
using Serilog;

namespace FluxKit.FluxIsland.Tests;

public sealed class LiveActivityTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("{\"title\":\"no id\"}")]
    [InlineData("{\"id\":\"a\"}")]
    [InlineData("{\"id\":\"a\",\"title\":\"x\",\"state\":\"explode\"}")]
    public void Bad_lines_are_dropped(string line) => Assert.False(LiveProtocol.TryParse(line, out _));

    [Fact]
    public void Oversize_lines_are_dropped() =>
        Assert.False(LiveProtocol.TryParse("{\"id\":\"a\",\"title\":\"" + new string('x', LiveProtocol.MaxLineLength) + "\"}", out _));

    [Fact]
    public void A_good_line_is_cleaned_and_clamped()
    {
        Assert.True(LiveProtocol.TryParse("{\"id\":\"build\",\"title\":\" Build\\u0007 \",\"progress\":140.6,\"glyph\":\"BUILD\",\"ttl\":999999}", out LiveUpdate? update));

        Assert.Equal("build", update!.Id);
        Assert.Equal("Build", update.Title);
        Assert.Equal(100, update.Progress);
        Assert.Equal("build", update.Glyph);
        Assert.Equal(LiveActivityModel.MaxTtl, update.Ttl);
        Assert.Equal(LiveState.Running, update.State);
    }

    [Fact]
    public void Unknown_glyphs_and_long_text_are_tamed()
    {
        Assert.True(LiveProtocol.TryParse("{\"id\":\"a\",\"title\":\"" + new string('t', 200) + "\",\"glyph\":\"../../evil\",\"state\":\"done\"}", out LiveUpdate? update));

        Assert.Null(update!.Glyph);
        Assert.Equal(LiveProtocol.MaxTitle, update.Title!.Length);
        Assert.EndsWith("…", update.Title, StringComparison.Ordinal);
    }

    [Fact]
    public void Updates_merge_by_id_and_done_reports_the_finish()
    {
        var model = new LiveActivityModel();
        model.Apply(new LiveUpdate("b", LiveState.Running, "Build", "Compiling", 10, "build", null), Now);
        model.Apply(new LiveUpdate("b", LiveState.Running, null, null, 60, null, null), Now.AddSeconds(5));

        LiveActivity running = Assert.Single(model.Items);
        Assert.Equal(("Build", "Compiling", 60, "build"), (running.Title, running.Detail, running.Progress, running.Glyph));

        LiveActivity? done = model.Apply(new LiveUpdate("b", LiveState.Done, null, null, null, null, null), Now.AddSeconds(9));
        Assert.Equal("Build", done?.Title);
        Assert.Empty(model.Items);
    }

    [Fact]
    public void At_most_five_live_and_each_expires_after_its_time()
    {
        var model = new LiveActivityModel();
        for (int i = 0; i < 7; i++)
        {
            model.Apply(new LiveUpdate($"j{i}", LiveState.Running, $"Job {i}", null, null, null, TimeSpan.FromMinutes(1)), Now.AddSeconds(i));
        }

        Assert.Equal(LiveActivityModel.MaxItems, model.Items.Count);
        Assert.Equal("j6", model.Items[0].Id);
        Assert.True(model.Expire(Now.AddMinutes(2)));
        Assert.Empty(model.Items);
    }

    [Fact]
    public async Task The_tool_shows_live_progress_then_a_finish_moment_and_ignores_it_when_off()
    {
        var time = new FakeTimeProvider();
        var settings = new FakeSettings();
        var view = new FakeIslandView();
        var live = new FakeLive();
        await settings.UpdateAsync<FluxIslandSettings>(FluxIslandSettings.Section, s =>
        {
            s.LiveActivities = true;
            return s;
        });
        await using var tool = new FluxIslandTool(settings, _ => view, new IslandSources { Live = () => live }, time, new LoggerConfiguration().CreateLogger());
        await tool.StartAsync(CancellationToken.None);

        live.Send("{\"id\":\"b\",\"title\":\"Build\",\"progress\":40}");
        Assert.Equal(ActivityKind.Live, view.Last.Primary?.Kind);
        Assert.Equal(40, view.Last.Primary?.Value);

        live.Send("{\"id\":\"b\",\"progress\":85}");
        Assert.Equal(85, view.Last.Primary?.Value);
        Assert.Equal("Build", view.Last.Primary?.Text);

        live.Send("{\"id\":\"b\",\"progress\":85}");
        Assert.Equal(85, view.Last.Primary?.Value);
        Assert.Equal("Build", view.Last.Primary?.Text);

        live.Send("{\"id\":\"b\",\"state\":\"failed\"}");
        Assert.Equal(ActivityKind.LiveDone, view.Last.Primary?.Kind);
        Assert.True(view.Last.Primary?.Flag);

        await settings.UpdateAsync<FluxIslandSettings>(FluxIslandSettings.Section, s =>
        {
            s.LiveActivities = false;
            return s;
        });
        live.Send("{\"id\":\"c\",\"title\":\"Other\"}");
        Assert.NotEqual(ActivityKind.Live, view.Last.Primary?.Kind);
    }

    private sealed class FakeLive : ILiveActivitySource
    {
        public event EventHandler<string>? LineReceived;

        public void Send(string line) => LineReceived?.Invoke(this, line);

        public void Start()
        {
        }

        public void Dispose()
        {
        }
    }
}
