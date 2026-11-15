using FluxKit.Tools.FluxIsland.Activities;

namespace FluxKit.FluxIsland.Tests;

public sealed class NotesModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 14, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Newest_note_comes_first_and_repeats_move_to_the_top()
    {
        var notes = new NotesModel();
        notes.Add("a", Now);
        notes.Add("b", Now);
        notes.Add("a", Now);

        Assert.Equal(["a", "b"], notes.Items.Select(n => n.Text));
    }

    [Fact]
    public void Keeps_at_most_the_limit_and_ignores_empty_text()
    {
        var notes = new NotesModel();
        Assert.False(notes.Add("   ", Now));
        for (int i = 0; i < NotesModel.MaxItems + 5; i++)
        {
            notes.Add($"note {i}", Now);
        }

        Assert.Equal(NotesModel.MaxItems, notes.Count);
        Assert.Equal($"note {NotesModel.MaxItems + 4}", notes.Items[0].Text);
    }

    [Fact]
    public void Remove_and_clear_report_whether_anything_changed()
    {
        var notes = new NotesModel();
        notes.Add("a", Now);

        Assert.True(notes.Remove("a"));
        Assert.False(notes.Remove("a"));
        Assert.False(notes.Clear());
    }
}
