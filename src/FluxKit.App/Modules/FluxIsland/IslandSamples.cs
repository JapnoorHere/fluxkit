using FluxKit.Tools.FluxIsland.Abstractions;
using FluxKit.Tools.FluxIsland.Activities;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>
/// Made-up scenes for previews: the settings page and the <c>--preview-island</c> switch. They show how each state
/// looks without real music, a charger or a timer.
/// </summary>
internal static class IslandSamples
{
    public static readonly string[] Names = ["clock", "music", "timer", "done", "charging", "copied", "offline", "volume", "shelf"];

    public static IslandScene Create(string name, DateTimeOffset now)
    {
        var media = new MediaInfo("Midnight Drive", "The Night Owls", true, TimeSpan.FromSeconds(72), TimeSpan.FromSeconds(214), now, true, true, true);
        var timer = new FocusTimerSnapshot(TimerMode.Focus, TimerState.Running, TimeSpan.FromMinutes(25), TimeSpan.FromMinutes(18), now + TimeSpan.FromMinutes(18) + TimeSpan.FromSeconds(42), 1);
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
            "shelf" => new IslandActivity(ActivityKind.Shelf) { Value = 3 },
            _ => null,
        };
        return new IslandScene
        {
            Primary = primary,
            Media = name == "music" ? media : null,
            Timer = name == "timer" ? timer : FocusTimerSnapshot.Idle,
            Shelf = name == "shelf" ? [@"C:\Users\Public\Documents\Report.pdf", @"C:\Users\Public\Pictures\Beach.jpg", @"C:\Users\Public\Music\Demo.mp3"] : [],
            Power = power,
            Privacy = new PrivacySnapshot(name == "music", false),
        };
    }
}
