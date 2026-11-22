using FluxKit.Tools.FluxIsland.Abstractions;
using FluxKit.Tools.FluxIsland.Activities;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>
/// The list pages: Notes (from the command bar), Rewind (recent moments worth a second look), Notifications and Clipboard
/// (opt-in history of copied text). They share <see cref="ListPanel"/>; this only says what each one lists.
/// </summary>
public sealed partial class IslandPill
{
    private const string NoteGlyph = "\xE70B";
    private const string RewindGlyph = "\xE81C";
    private const string ClipboardGlyph = "\xE77F";
    private const string RemoveGlyph = "\xE711";
    private const string PinGlyph = "\xE718";
    private const string InboxGlyph = "\xE8BD";
    private const string MuteGlyph = "\xE7ED";

    private void ConfigureLists()
    {
        NotesView.Configure(new ListPageSpec(
            NoteGlyph,
            IslandPalette.Yellow,
            "Island_NotesTitle",
            "Island_NotesNameFormat",
            "Island_NotesEmpty",
            (scene, now) => [.. scene.Notes.Select(note => new ListItem(
                note.Text,
                note.Text,
                ListPanel.Age(note.Created, now),
                null,
                IslandPalette.Yellow,
                () => Copy(note.Text),
                Strings.Get("Island_CopyNote"),
                [new ListAction(RemoveGlyph, Strings.Get("Island_RemoveNote"), () => Commands?.RemoveNote(note.Text), Removes: true)]))],
            () => Commands?.ClearNotes(),
            () => Commands?.UndoClear()));

        RewindView.Configure(new ListPageSpec(
            RewindGlyph,
            IslandPalette.Blue,
            "Island_RewindTitle",
            "Island_RewindNameFormat",
            "Island_RewindEmpty",
            (scene, now) => [.. scene.Rewind.Select(entry => RewindItem(entry, now))],
            () => Commands?.ClearRewind(),
            () => Commands?.UndoClear()));

        ClipsView.Configure(new ListPageSpec(
            ClipboardGlyph,
            IslandPalette.Cyan,
            "Island_ClipsTitle",
            "Island_ClipsNameFormat",
            "Island_ClipsEmpty",
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
                    new ListAction(RemoveGlyph, Strings.Get("Island_RemoveClip"), () => Commands?.RemoveClip(clip.Text), Removes: true),
                ]))],
            () => Commands?.ClearClips(),
            () => Commands?.UndoClear()));

        InboxView.Configure(new ListPageSpec(
            InboxGlyph,
            IslandPalette.Blue,
            "Island_InboxTitle",
            "Island_InboxNameFormat",
            "Island_InboxEmpty",
            (scene, now) => [.. Grouped(scene.Inbox).Select(toast => InboxItem(toast, now))],
            () => Commands?.ClearInbox(),
            () => Commands?.UndoClear()));
    }

    /// <summary>
    /// The Stash page (plan D): windows dragged onto the island, newest first. Click one to bring it back where it was;
    /// "Clear" brings them all back. Windows closed in the meantime drop off by themselves.
    /// </summary>
    private void ConfigureStash() =>
        StashView.Configure(new ListPageSpec(
            IslandPresenter.StashGlyph,
            IslandPalette.Cyan,
            "Island_StashTitle",
            "Island_StashNameFormat",
            "Island_StashEmpty",
            (scene, now) =>
            {
                if (scene.Stash.Any(w => !FluxKit.Platform.Windows.WindowThumbnail.Exists((nint)w.Handle)))
                {
                    DispatcherQueue.TryEnqueue(() => Commands?.PruneStash(h => FluxKit.Platform.Windows.WindowThumbnail.Exists((nint)h)));
                }

                return [.. scene.Stash.Where(w => FluxKit.Platform.Windows.WindowThumbnail.Exists((nint)w.Handle)).Select(w => new ListItem(
                    w.Handle.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    w.Title,
                    ListPanel.Age(w.At, now),
                    null,
                    IslandPalette.Cyan,
                    () => BringBack(w.Handle),
                    Strings.Get("Island_StashBringBack"),
                    []))];
            },
            () =>
            {
                foreach (StashedWindow window in _scene.Stash)
                {
                    BringBack(window.Handle);
                }
            },
            () => { }));

    /// <summary>Brings a tucked-away window back where it was, and takes it off the Stash page.</summary>
    private void BringBack(long handle)
    {
        if (FluxKit.Platform.Windows.WindowThumbnail.Exists((nint)handle))
        {
            FluxKit.Platform.Windows.WindowThumbnail.Activate((nint)handle);
        }

        Commands?.UnstashWindow(handle);
    }

    /// <summary>
    /// Notifications grouped by app, as in Notification Center: the app with the newest one first, each app's own
    /// notifications newest first.
    /// </summary>
    private static IEnumerable<ToastInfo> Grouped(IReadOnlyList<ToastInfo> inbox) =>
        inbox.GroupBy(t => t.AppId, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Max(t => t.Arrived))
            .SelectMany(g => g.OrderByDescending(t => t.Arrived));

    /// <summary>A notification as a row: the app logo, "Sender · message", how long ago, and Mute and Dismiss.</summary>
    private ListItem InboxItem(ToastInfo toast, DateTimeOffset now)
    {
        string text = toast.Body.Length > 0 ? $"{toast.Title} · {toast.Body.ReplaceLineEndings(" ")}" : toast.Title;
        return new ListItem(
            toast.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            text,
            ListPanel.Age(toast.Arrived, now),
            InboxGlyph,
            IslandPalette.Blue,
            () => Commands?.OpenNotification(toast.Id),
            Strings.Format("Island_OpenAppFormat", toast.AppName),
            [
                new ListAction(MuteGlyph, Strings.Format("Island_MuteAppFormat", toast.AppName), () => Commands?.MuteNotificationApp(toast.AppId, TimeSpan.FromHours(1))),
                new ListAction(RemoveGlyph, Strings.Get("Island_DismissNotification"), () => Commands?.DismissNotification(toast.Id), Removes: true),
            ])
        {
            Image = _logos.Get(toast.AppId, toast.Logo),
        };
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
