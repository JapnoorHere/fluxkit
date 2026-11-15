using FluxKit.Tools.FluxIsland.Activities;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>
/// The three list pages: Notes (from the command bar), Rewind (recent moments worth a second look) and Clipboard
/// (opt-in history of copied text). They share <see cref="ListPanel"/>; this only says what each one lists.
/// </summary>
public sealed partial class IslandPill
{
    private const string NoteGlyph = "\xE70B";
    private const string RewindGlyph = "\xE81C";
    private const string ClipboardGlyph = "\xE77F";
    private const string RemoveGlyph = "\xE711";
    private const string PinGlyph = "\xE718";

    private void ConfigureLists()
    {
        NotesView.Configure(new ListPageSpec(
            NoteGlyph,
            IslandPalette.Yellow,
            "Island_NotesTitle",
            "Island_NotesNameFormat",
            (scene, now) => [.. scene.Notes.Select(note => new ListItem(
                note.Text,
                note.Text,
                ListPanel.Age(note.Created, now),
                null,
                IslandPalette.Yellow,
                () => Copy(note.Text),
                Strings.Get("Island_CopyNote"),
                [new ListAction(RemoveGlyph, Strings.Get("Island_RemoveNote"), () => Commands?.RemoveNote(note.Text))]))],
            () => Commands?.ClearNotes()));

        RewindView.Configure(new ListPageSpec(
            RewindGlyph,
            IslandPalette.Blue,
            "Island_RewindTitle",
            "Island_RewindNameFormat",
            (scene, now) => [.. scene.Rewind.Select(entry => RewindItem(entry, now))],
            () => Commands?.ClearRewind()));

        ClipsView.Configure(new ListPageSpec(
            ClipboardGlyph,
            IslandPalette.Cyan,
            "Island_ClipsTitle",
            "Island_ClipsNameFormat",
            (scene, now) => [.. scene.Clips.Select(clip => new ListItem(
                clip.Text,
                clip.Text.ReplaceLineEndings(" "),
                ListPanel.Age(clip.At, now),
                null,
                IslandPalette.Cyan,
                () => Copy(clip.Text),
                Strings.Get("Island_CopyClip"),
                [
                    new ListAction(PinGlyph, Strings.Get(clip.Pinned ? "Island_UnpinClip" : "Island_PinClip"), () => Commands?.PinClip(clip.Text), clip.Pinned),
                    new ListAction(RemoveGlyph, Strings.Get("Island_RemoveClip"), () => Commands?.RemoveClip(clip.Text)),
                ]))],
            () => Commands?.ClearClips()));
    }

    /// <summary>A recent moment as a row: how it looked on the pill, when, and its action (open, show in folder…).</summary>
    private ListItem RewindItem(RewindEntry entry, DateTimeOffset now)
    {
        CompactContent content = IslandPresenter.Describe(entry.Activity, now);
        string text = content.Subtitle is { } detail ? $"{content.Title} · {detail}" : content.Title;
        string? action = IslandPresenter.ActionLabel(entry.Activity);
        return new ListItem(
            $"{entry.At.UtcTicks}-{entry.Activity.Kind}",
            text,
            ListPanel.Age(entry.At, now),
            content.Glyph,
            content.Accent,
            action is null ? null : () => RunAction(entry.Activity),
            action,
            []);
    }
}
