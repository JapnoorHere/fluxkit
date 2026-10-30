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
    private const string BrightnessGlyph = "\xE706";
    private const string DownloadGlyph = "\xE896";
    private const string DoneGlyph = "\xE73E";
    private const string ScreenshotGlyph = "\xE722";
    private const string DesktopGlyph = "\xE7C4";
    private const string HeadphoneGlyph = "\xE7F6";
    private const string MicGlyph = "\xE720";
    private const string MicOffGlyph = "\xEC54";
    private const string KeyboardGlyph = "\xE765";
    private const string LeafGlyph = "\xEC0A";
    private const string BluetoothGlyph = "\xE702";
    private const string StopwatchGlyph = "\xE916";

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
        ActivityKind.Brightness => new(BrightnessGlyph, Strings.Get("Island_Brightness"), null, Percent(activity.Value), IslandPalette.TextPrimary),
        ActivityKind.Download => new(DownloadGlyph, activity.Text ?? Strings.Get("Island_Downloading"), Strings.Get("Island_Downloading"), FormatSize(activity.Value), IslandPalette.Cyan),
        ActivityKind.DownloadDone => new(DoneGlyph, Strings.Get("Island_Downloaded"), activity.Text, null, IslandPalette.Green),
        ActivityKind.Screenshot => new(ScreenshotGlyph, Strings.Get("Island_Screenshot"), activity.Text, null, IslandPalette.Violet),
        ActivityKind.DesktopSwitched => new(DesktopGlyph, activity.Text ?? Strings.Format("Island_DesktopFormat", activity.Value), null, Strings.Format("Island_OfFormat", activity.Value, activity.Total), IslandPalette.Violet),
        ActivityKind.AudioDevice => new(HeadphoneGlyph, activity.Text ?? string.Empty, Strings.Get("Island_AudioDeviceNow"), null, IslandPalette.Cyan),
        ActivityKind.MicMuted => new(activity.Flag ? MicOffGlyph : MicGlyph, Strings.Get(activity.Flag ? "Island_MicMuted" : "Island_MicOn"), null, null, activity.Flag ? IslandPalette.Red : IslandPalette.Green),
        ActivityKind.CapsLock => new(KeyboardGlyph, Strings.Get(activity.Flag ? "Island_CapsOn" : "Island_CapsOff"), null, null, activity.Flag ? IslandPalette.Orange : IslandPalette.TextPrimary),
        ActivityKind.NumLock => new(KeyboardGlyph, Strings.Get(activity.Flag ? "Island_NumOn" : "Island_NumOff"), null, null, IslandPalette.TextPrimary),
        ActivityKind.BatterySaver => new(LeafGlyph, Strings.Get(activity.Flag ? "Island_SaverOn" : "Island_SaverOff"), null, Percent(activity.Value), activity.Flag ? IslandPalette.Green : IslandPalette.TextPrimary),
        ActivityKind.DeviceConnected => new(BluetoothGlyph, activity.Text ?? string.Empty, Strings.Get("Island_DeviceConnected"), activity.Value > 0 ? Percent(activity.Value) : null, IslandPalette.Cyan),
        ActivityKind.DeviceDisconnected => new(BluetoothGlyph, activity.Text ?? string.Empty, Strings.Get("Island_DeviceDisconnected"), null, IslandPalette.TextSecondary),
        ActivityKind.Stopwatch => new(StopwatchGlyph, FormatPosition(TimeSpan.FromMilliseconds(activity.Value)), null, Strings.Get("Island_Stopwatch"), IslandPalette.Orange),
        _ => new(string.Empty, string.Empty, null, null, IslandPalette.TextPrimary),
    };

    /// <summary>The label of the one action a moment offers while peeking, or null when it has none.</summary>
    public static string? ActionLabel(IslandActivity activity) => activity.Kind switch
    {
        ActivityKind.MicMuted => Strings.Get(activity.Flag ? "Island_Action_Unmute" : "Island_Action_Mute"),
        _ => null,
    };

    /// <summary>A file size such as 12 MB or 1.4 GB, for a download that is still growing.</summary>
    public static string? FormatSize(int megabytes) => megabytes switch
    {
        <= 0 => null,
        < 1024 => Strings.Format("Island_SizeMbFormat", megabytes),
        _ => Strings.Format("Island_SizeGbFormat", (megabytes / 1024.0).ToString("0.0", CultureInfo.CurrentCulture)),
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
