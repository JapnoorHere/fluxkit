using System.Globalization;
using FluxKit.Tools.FluxIsland.Abstractions;
using FluxKit.Tools.FluxIsland.Activities;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>Turns an activity into what the compact pill shows. All text comes from the string resources.</summary>
internal static class IslandPresenter
{
    // Segoe Fluent Icons.
    private const string MusicGlyph = "\xE8D6";
    private const string TimerGlyph = "\xE916";
    private const string BellGlyph = "\xEA8F";
    private const string ShelfGlyph = "\xE7B8";
    private const string BoltGlyph = "\xE945";
    private const string OfflineGlyph = "\xF384";
    private const string OnlineGlyph = "\xE774";
    private const string CopyGlyph = "\xE8C8";
    private const string VolumeGlyph = "\xE767";
    private const string MuteGlyph = "\xE74F";

    public static CompactContent Describe(IslandActivity activity, DateTimeOffset now) => activity.Kind switch
    {
        ActivityKind.Media => new(
            MusicGlyph,
            string.IsNullOrWhiteSpace(activity.Media?.Title) ? Strings.Get("Island_Media_Untitled") : activity.Media.Title,
            NullIfEmpty(activity.Media?.Artist),
            null,
            IslandPalette.Violet),
        ActivityKind.Timer => DescribeTimer(activity.Timer ?? FocusTimerSnapshot.Idle, now),
        ActivityKind.Shelf => new(ShelfGlyph, Strings.Format("Island_Shelf_CountFormat", activity.Value), null, null, IslandPalette.Cyan),
        ActivityKind.TimerDone => new(BellGlyph, Strings.Get(activity.Timer?.Mode switch
        {
            TimerMode.Focus => "Island_TimerDone_Focus",
            TimerMode.Break => "Island_TimerDone_Break",
            _ => "Island_TimerDone_Countdown",
        }), null, null, IslandPalette.Orange),
        ActivityKind.PluggedIn => new(BoltGlyph, Strings.Get("Island_Charging"), null, Percent(activity.Value), IslandPalette.Green),
        ActivityKind.Unplugged => new(BatteryGlyph(activity.Value), Strings.Get("Island_OnBattery"), null, Percent(activity.Value), IslandPalette.TextPrimary),
        ActivityKind.LowBattery => new(BatteryGlyph(activity.Value), Strings.Get("Island_BatteryLow"), null, Percent(activity.Value), IslandPalette.Red),
        ActivityKind.Offline => new(OfflineGlyph, Strings.Get("Island_Offline"), null, null, IslandPalette.Orange),
        ActivityKind.Online => new(OnlineGlyph, Strings.Get("Island_Online"), null, null, IslandPalette.Green),
        ActivityKind.Copied => new(CopyGlyph, Strings.Get("Island_Copied"), null, activity.Clipboard switch
        {
            ClipboardKind.Text => Strings.Get("Island_Copied_Text"),
            ClipboardKind.Image => Strings.Get("Island_Copied_Image"),
            ClipboardKind.Files => Strings.Get("Island_Copied_Files"),
            _ => null,
        }, IslandPalette.Cyan),
        ActivityKind.Volume => new(activity.Value == 0 ? MuteGlyph : VolumeGlyph, Strings.Get("Island_Volume"), null, Percent(activity.Value), IslandPalette.TextPrimary),
        _ => new(string.Empty, string.Empty, null, null, IslandPalette.TextPrimary),
    };

    /// <summary>Time left, rounded up so a fresh 25-minute timer reads 25:00 and the last second reads 0:01.</summary>
    public static string FormatRemaining(TimeSpan left)
    {
        var whole = TimeSpan.FromSeconds(Math.Ceiling(left.TotalSeconds));
        return whole.TotalHours >= 1
            ? whole.ToString(@"h\:mm\:ss", CultureInfo.CurrentCulture)
            : whole.ToString(@"m\:ss", CultureInfo.CurrentCulture);
    }

    /// <summary>A position in a track, like 1:05 or 1:02:05.</summary>
    public static string FormatPosition(TimeSpan position)
    {
        var whole = TimeSpan.FromSeconds(Math.Floor(Math.Max(0, position.TotalSeconds)));
        return whole.TotalHours >= 1
            ? whole.ToString(@"h\:mm\:ss", CultureInfo.CurrentCulture)
            : whole.ToString(@"m\:ss", CultureInfo.CurrentCulture);
    }

    /// <summary>The clock as the user has set it in Windows (12 or 24 hours), without seconds.</summary>
    public static string FormatClock(DateTimeOffset now) => now.ToLocalTime().ToString("t", CultureInfo.CurrentCulture);

    public static string FormatDate(DateTimeOffset now) => now.ToLocalTime().ToString("ddd d MMM", CultureInfo.CurrentCulture);

    private static CompactContent DescribeTimer(FocusTimerSnapshot timer, DateTimeOffset now)
    {
        string? label = timer.State == TimerState.Paused
            ? Strings.Get("Island_Timer_Paused")
            : timer.Mode switch
            {
                TimerMode.Focus => Strings.Get("Island_Timer_Focus"),
                TimerMode.Break => Strings.Get("Island_Timer_Break"),
                _ => null,
            };
        return new(TimerGlyph, FormatRemaining(timer.RemainingAt(now)), null, label, IslandPalette.Orange);
    }

    /// <summary>The Segoe Fluent battery glyph for a level (Battery0 to Battery10).</summary>
    private static string BatteryGlyph(int percent) => ((char)(0xEBA0 + Math.Clamp(percent / 10, 0, 10))).ToString();

    private static string Percent(int value) => Strings.Format("Island_PercentFormat", value);

    private static string? NullIfEmpty(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;
}
