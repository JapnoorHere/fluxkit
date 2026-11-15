using FluxKit.Tools.FluxIsland.Abstractions;
using FluxKit.Tools.FluxIsland.Activities;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>
/// Made-up scenes for previews: the settings page and the <c>--preview-island</c> switch. They show how each state
/// looks without real music, a charger or a timer.
/// </summary>
internal static class IslandSamples
{
    /// <summary>An empty shelf with a file being dragged over the island.</summary>
    public const string Drop = "drop";

    public static readonly string[] Names =
    [
        "clock", "music", "timer", "done", "charging", "copied", "offline", "volume", "shelf", Drop,
        "split", "download", "downloaded", "screenshot", "desktop", "headphones", "micmuted", "brightness", "caps", "saver", "device", "stopwatch", "link", "colour", "numlock", "disconnected", "both", "glance", "command", "notes",
    ];

    public static IslandScene Create(string name, DateTimeOffset now)
    {
        var media = new MediaInfo("Midnight Drive", "The Night Owls", true, TimeSpan.FromSeconds(72), TimeSpan.FromSeconds(214), now, true, true, true);
        var timer = new FocusTimerSnapshot(TimerMode.Focus, TimerState.Running, TimeSpan.FromMinutes(25), TimeSpan.FromMinutes(18), now + TimeSpan.FromMinutes(18) + TimeSpan.FromSeconds(42), 1);
        var stopwatch = new StopwatchSnapshot(TimerState.Running, TimeSpan.Zero, now - TimeSpan.FromSeconds(83), [TimeSpan.FromSeconds(41), TimeSpan.FromSeconds(38)]);
        var power = new PowerSnapshot(true, 76, true);
        IslandActivity? primary = name switch
        {
            "music" => new IslandActivity(ActivityKind.Media) { Media = media },
            "timer" => new IslandActivity(ActivityKind.Timer) { Timer = timer },
            "done" => new IslandActivity(ActivityKind.TimerDone) { Timer = timer with { State = TimerState.Idle } },
            "charging" => new IslandActivity(ActivityKind.PluggedIn) { Value = 76 },
            "copied" => new IslandActivity(ActivityKind.Copied) { Clipboard = ClipboardKind.Text },
            "offline" => new IslandActivity(ActivityKind.Offline),
            "volume" => new IslandActivity(ActivityKind.Volume) { Value = 64 },
            "shelf" => new IslandActivity(ActivityKind.Shelf) { Value = 4 },
            "split" => new IslandActivity(ActivityKind.Timer) { Timer = timer },
            "download" => new IslandActivity(ActivityKind.Download) { Text = "Holiday photos.zip", Value = 148 },
            "downloaded" => new IslandActivity(ActivityKind.DownloadDone) { Text = "Holiday photos.zip", Path = @"C:\Windows\Web\Wallpaper\Windows\img0.jpg" },
            "screenshot" => new IslandActivity(ActivityKind.Screenshot) { Text = "Screenshot 2026-10-30 101500.png", Path = @"C:\Windows\Web\Wallpaper\Windows\img0.jpg" },
            "desktop" => new IslandActivity(ActivityKind.DesktopSwitched) { Text = "Work", Value = 2, Total = 4 },
            "headphones" => new IslandActivity(ActivityKind.AudioDevice) { Text = "WH-1000XM5" },
            "micmuted" => new IslandActivity(ActivityKind.MicMuted) { Flag = true },
            "brightness" => new IslandActivity(ActivityKind.Brightness) { Value = 70 },
            "caps" => new IslandActivity(ActivityKind.CapsLock) { Flag = true },
            "numlock" => new IslandActivity(ActivityKind.NumLock) { Flag = false },
            "disconnected" => new IslandActivity(ActivityKind.DeviceDisconnected) { Text = "Galaxy Buds3 Pro" },
            "both" => new IslandActivity(ActivityKind.Timer) { Timer = timer },
            "saver" => new IslandActivity(ActivityKind.BatterySaver) { Flag = true, Value = 18 },
            "device" => new IslandActivity(ActivityKind.DeviceConnected) { Text = "Galaxy Buds3 Pro", Value = 80 },
            "stopwatch" => new IslandActivity(ActivityKind.Stopwatch) { Stopwatch = stopwatch },
            "link" => new IslandActivity(ActivityKind.Copied) { Clipboard = ClipboardKind.Text, Smart = SmartText.Link, Text = "github.com", Path = "https://github.com/" },
            "colour" => new IslandActivity(ActivityKind.Copied) { Clipboard = ClipboardKind.Text, Smart = SmartText.Color, Text = "#3A7BFF", Value = 0x3A7BFF },
            _ => null,
        };
        return new IslandScene
        {
            Primary = primary,
            Secondary = name switch
            {
                "split" => new IslandActivity(ActivityKind.Media) { Media = media },
                "both" => new IslandActivity(ActivityKind.Stopwatch) { Stopwatch = stopwatch },
                _ => null,
            },
            Media = name is "music" or "split" ? media : null,
            Timer = name is "timer" or "split" or "both" ? timer : FocusTimerSnapshot.Idle,
            Stopwatch = name is "stopwatch" or "both" ? stopwatch : StopwatchSnapshot.Idle,
            Notes = name == "notes"
                ? [new IslandNote("Call Sam about the launch", now - TimeSpan.FromMinutes(5)), new IslandNote("Buy milk", now - TimeSpan.FromHours(2)), new IslandNote("#3A7BFF for the new icon", now - TimeSpan.FromDays(1))]
                : [],
            Shelf = name == "shelf" ? [@"C:\Windows\Web\Wallpaper\Windows\img0.jpg", @"C:\Windows\Media\Alarm01.wav", @"C:\Windows\Web\Screen", @"C:\Windows\notepad.exe"] : [],
            Power = power,
            Privacy = new PrivacySnapshot(name == "music", false),
        };
    }
}
