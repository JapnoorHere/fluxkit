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
        "split", "download", "downloaded", "screenshot", "desktop", "headphones", "micmuted", "brightness", "caps", "saver", "device", "stopwatch", "link", "colour", "numlock", "disconnected", "both", "glance", "command", "notes", "call", "zipped", "rewind", "clips", "glass", "mercury", "ferro", "halo", "scrub", "droplet", "notification", "notifications", "inbox",
    ];

    public static IslandScene Create(string name, DateTimeOffset now)
    {
        // Windows' own wallpaper stands in for album art, so previews show the colour wash.
        string sampleArt = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Web", "Wallpaper", "Windows", "img0.jpg");
        var media = new MediaInfo("Midnight Drive", "The Night Owls", true, TimeSpan.FromSeconds(72), TimeSpan.FromSeconds(214), now, true, true, true)
        {
            Thumbnail = File.Exists(sampleArt) ? File.ReadAllBytes(sampleArt) : ReadOnlyMemory<byte>.Empty,
        };
        var timer = new FocusTimerSnapshot(TimerMode.Focus, TimerState.Running, TimeSpan.FromMinutes(25), TimeSpan.FromMinutes(18), now + TimeSpan.FromMinutes(18) + TimeSpan.FromSeconds(42), 1);
        // FluxKit's own icon stands in for an app logo.
        string sampleLogo = Path.Combine(AppContext.BaseDirectory, "Assets", "FluxKit.png");
        ReadOnlyMemory<byte> logo = File.Exists(sampleLogo) ? File.ReadAllBytes(sampleLogo) : ReadOnlyMemory<byte>.Empty;
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
            "glass" or "mercury" or "ferro" or "halo" => new IslandActivity(ActivityKind.Media) { Media = media },
            "download" => new IslandActivity(ActivityKind.Download) { Text = "Holiday photos.zip", Value = 148 },
            "downloaded" => new IslandActivity(ActivityKind.DownloadDone) { Text = "Holiday photos.zip", Path = @"C:\Windows\Web\Wallpaper\Windows\img0.jpg" },
            "screenshot" => new IslandActivity(ActivityKind.Screenshot) { Text = "Screenshot 2026-10-30 101500.png", Path = @"C:\Windows\Web\Wallpaper\Windows\img0.jpg" },
            "desktop" => new IslandActivity(ActivityKind.DesktopSwitched) { Text = "Work", Value = 2, Total = 4 },
            "headphones" => new IslandActivity(ActivityKind.AudioDevice) { Text = "WH-1000XM5" },
            "micmuted" => new IslandActivity(ActivityKind.MicMuted) { Flag = true },
            "brightness" => new IslandActivity(ActivityKind.Brightness) { Value = 70 },
            "caps" => new IslandActivity(ActivityKind.CapsLock) { Flag = true },
            "numlock" => new IslandActivity(ActivityKind.NumLock) { Flag = false },
            "zipped" => new IslandActivity(ActivityKind.FileReady) { Value = (int)FileResult.Zipped, Total = 3, Text = "Archive.zip", Path = @"C:\Windows\Web\Wallpaper\Windows\img0.jpg" },
            "call" => new IslandActivity(ActivityKind.Call) { Since = now - TimeSpan.FromSeconds(754), Flag = true, Value = 1 },
            "disconnected" => new IslandActivity(ActivityKind.DeviceDisconnected) { Text = "Galaxy Buds3 Pro" },
            "both" => new IslandActivity(ActivityKind.Timer) { Timer = timer },
            "saver" => new IslandActivity(ActivityKind.BatterySaver) { Flag = true, Value = 18 },
            "device" => new IslandActivity(ActivityKind.DeviceConnected) { Text = "Galaxy Buds3 Pro", Value = 80 },
            "stopwatch" => new IslandActivity(ActivityKind.Stopwatch) { Stopwatch = stopwatch },
            "link" => new IslandActivity(ActivityKind.Copied) { Clipboard = ClipboardKind.Text, Smart = SmartText.Link, Text = "github.com", Path = "https://github.com/" },
            "colour" => new IslandActivity(ActivityKind.Copied) { Clipboard = ClipboardKind.Text, Smart = SmartText.Color, Text = "#3A7BFF", Value = 0x3A7BFF },
            "notification" => new IslandActivity(ActivityKind.Notification) { Text = "Priya Sharma", Detail = "Are we still on for lunch at 1?", Source = "Teams", Image = logo, Value = 1 },
            "notifications" => new IslandActivity(ActivityKind.Notification) { Text = "Design review", Detail = "Sam: the new icons look great", Source = "Teams", Image = logo, Value = 3 },
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
            Media = name is "music" or "split" or "glass" or "mercury" or "ferro" or "halo" ? media : null,
            Timer = name is "timer" or "split" or "both" or "droplet" ? timer : FocusTimerSnapshot.Idle,
            Droplets = name == "droplet" ? [new DropletPlace(ActivityKind.Timer, 1180, 120)] : [],
            Stopwatch = name is "stopwatch" or "both" ? stopwatch : StopwatchSnapshot.Idle,
            Rewind = name is "rewind" or "scrub"
                ?
                [
                    new RewindEntry(new IslandActivity(ActivityKind.Screenshot) { Text = "Screenshot 2026-11-14.png", Path = @"C:\Windows\Web\Wallpaper\Windows\img0.jpg" }, now - TimeSpan.FromMinutes(2)),
                    new RewindEntry(new IslandActivity(ActivityKind.Copied) { Clipboard = ClipboardKind.Text, Smart = SmartText.Link, Text = "github.com", Path = "https://github.com/" }, now - TimeSpan.FromMinutes(18)),
                    new RewindEntry(new IslandActivity(ActivityKind.DownloadDone) { Text = "Holiday photos.zip", Path = @"C:\Windows\notepad.exe" }, now - TimeSpan.FromHours(1)),
                ]
                : [],
            Clips = name == "clips"
                ? [new ClipEntry("Meet at 6 at the usual place", now - TimeSpan.FromMinutes(1), true), new ClipEntry("https://github.com/fluxkit", now - TimeSpan.FromMinutes(9), false), new ClipEntry("#3A7BFF", now - TimeSpan.FromHours(3), false)]
                : [],
            Settings = new FluxKit.Tools.FluxIsland.FluxIslandSettings
            {
                ClipboardHistory = name == "clips",
                ShowNotifications = name is "notification" or "notifications" or "inbox",
                LightHalo = name == "halo",
                Look = name switch { "glass" => FluxKit.Tools.FluxIsland.IslandLook.Glass, "mercury" => FluxKit.Tools.FluxIsland.IslandLook.Mercury, _ => FluxKit.Tools.FluxIsland.IslandLook.Onyx },
            },
            Inbox = name == "inbox"
                ?
                [
                    new ToastInfo(3, "Teams!App", "Teams", logo, "Priya Sharma", "Are we still on for lunch at 1?", now - TimeSpan.FromSeconds(20)),
                    new ToastInfo(2, "Mail!App", "Mail", ReadOnlyMemory<byte>.Empty, "Invoice for November", "Your invoice is ready to view", now - TimeSpan.FromMinutes(12)),
                    new ToastInfo(1, "Teams!App", "Teams", logo, "Design review", "Sam: the new icons look great", now - TimeSpan.FromMinutes(40)),
                ]
                : [],
            Notes = name == "notes"
                ? [new IslandNote("Call Sam about the launch", now - TimeSpan.FromMinutes(5)), new IslandNote("Buy milk", now - TimeSpan.FromHours(2)), new IslandNote("#3A7BFF for the new icon", now - TimeSpan.FromDays(1))]
                : [],
            Shelf = name == "shelf" ? [@"C:\Windows\Web\Wallpaper\Windows\img0.jpg", @"C:\Windows\Media\Alarm01.wav", @"C:\Windows\Web\Screen", @"C:\Windows\notepad.exe"] : [],
            Power = power,
            Privacy = new PrivacySnapshot(name == "music", false),
        };
    }
}
