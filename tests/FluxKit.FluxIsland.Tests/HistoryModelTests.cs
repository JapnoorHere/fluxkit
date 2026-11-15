using FluxKit.Tools.FluxIsland.Activities;

namespace FluxKit.FluxIsland.Tests;

public sealed class HistoryModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 14, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Rewind_keeps_moments_worth_looking_back_at_newest_first()
    {
        var history = new MomentHistory();

        Assert.True(history.Record(new IslandActivity(ActivityKind.DownloadDone) { Text = "a.zip" }, Now));
        Assert.False(history.Record(new IslandActivity(ActivityKind.Volume) { Value = 40 }, Now));
        Assert.False(history.Record(new IslandActivity(ActivityKind.Copied), Now));
        Assert.True(history.Record(new IslandActivity(ActivityKind.Copied) { Smart = SmartText.Link, Text = "github.com" }, Now));

        Assert.Equal([ActivityKind.Copied, ActivityKind.DownloadDone], history.Items.Select(e => e.Activity.Kind));
    }

    [Fact]
    public void Rewind_keeps_at_most_its_limit()
    {
        var history = new MomentHistory();
        for (int i = 0; i < MomentHistory.MaxItems + 10; i++)
        {
            history.Record(new IslandActivity(ActivityKind.Screenshot) { Value = i }, Now);
        }

        Assert.Equal(MomentHistory.MaxItems, history.Count);
        Assert.Equal(MomentHistory.MaxItems + 9, history.Items[0].Activity.Value);
    }

    [Fact]
    public void Clipboard_history_moves_repeats_to_the_top_and_keeps_pins_first_and_through_clear()
    {
        var clips = new ClipboardHistoryModel();
        clips.Add("one", Now);
        clips.Add("two", Now);
        clips.TogglePin("one");
        clips.Add("three", Now);
        clips.Add("two", Now);

        Assert.Equal(["one", "two", "three"], clips.Items.Select(c => c.Text));

        clips.Clear();

        Assert.Equal(["one"], clips.Items.Select(c => c.Text));
        Assert.True(clips.Items[0].Pinned);
    }

    [Fact]
    public void Clipboard_history_drops_the_oldest_unpinned_items_and_cuts_long_text()
    {
        var clips = new ClipboardHistoryModel();
        clips.Add("pinned", Now);
        clips.TogglePin("pinned");
        for (int i = 0; i < ClipboardHistoryModel.MaxItems + 3; i++)
        {
            clips.Add($"item {i}", Now);
        }

        clips.Add(new string('x', 5000), Now);

        Assert.Equal(ClipboardHistoryModel.MaxItems + 1, clips.Count);
        Assert.Contains(clips.Items, c => c.Text == "pinned");
        Assert.Equal(ClipboardHistoryModel.MaxLength, clips.Items[1].Text.Length);
    }
}
