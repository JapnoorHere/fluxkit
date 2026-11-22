using FluxKit.Core.Settings;
using FluxKit.Tools.FluxIsland.Activities;

namespace FluxKit.Tools.FluxIsland;

/// <summary>Which screen the island sits on.</summary>
public enum IslandMonitor
{
    /// <summary>The main display.</summary>
    Main,

    /// <summary>The display the mouse pointer is on; the island moves there when something new shows.</summary>
    FollowMouse,
}

/// <summary>Which moments may take over the island.</summary>
public enum IslandInterruptions
{
    /// <summary>Every moment shows.</summary>
    Everything,

    /// <summary>Only what needs you: time's up, low battery, going offline, talking while muted, and your own file jobs.</summary>
    Important,

    /// <summary>No quick moments; only lasting things (music, timers, calls, the shelf).</summary>
    Nothing,
}

/// <summary>One-time tips the island has already shown, so each appears only once.</summary>
[Flags]
public enum IslandHints
{
    None = 0,

    /// <summary>"Click the bubble to swap."</summary>
    Bubble = 1,

    /// <summary>"Right-click to wave it away."</summary>
    Dismiss = 2,

    /// <summary>"Scroll over the island to change the volume."</summary>
    Volume = 4,

    /// <summary>"Swipe sideways for more pages."</summary>
    Pages = 8,
}

/// <summary>The island's material.</summary>
public enum IslandLook
{
    /// <summary>True black, like a phone's island blending into the screen edge.</summary>
    Onyx,

    /// <summary>Labs: dark smoked glass that lets a hint of the desktop through, with a brighter rim.</summary>
    Glass,

    /// <summary>Liquid metal: dark chrome with a highlight that follows the pointer (ADR-015).</summary>
    Mercury,
}

public enum IslandEffects
{
    /// <summary>Springs, glow and the playing indicator.</summary>
    Full,

    /// <summary>Short fades only. Also used whenever Windows animations are turned off.</summary>
    Light,
}

/// <summary>Persisted settings for Flux Island ("utilities/flux-island").</summary>
public sealed class FluxIslandSettings : ISettingsSection
{
    public const string Section = SettingsKeys.ToolPrefix + FluxIslandTool.Id;
    public const int MinFocusMinutes = 5;
    public const int MaxFocusMinutes = 90;
    public const int MinBreakMinutes = 1;
    public const int MaxBreakMinutes = 30;

    public bool ShowMedia { get; set; } = true;

    public bool ShowPower { get; set; } = true;

    public bool ShowNetwork { get; set; } = true;

    public bool ShowPrivacy { get; set; } = true;

    public bool ShowClipboard { get; set; } = true;

    public bool ShelfEnabled { get; set; } = true;

    /// <summary>Keeps the files on the shelf after FluxKit or Windows restarts. Only their paths are saved, on this PC.</summary>
    public bool KeepShelf { get; set; } = true;

    /// <summary>Keeps quick notes after a restart, in a file on this PC. Turning it off deletes the file.</summary>
    public bool KeepNotes { get; set; } = true;

    public bool ScrollForVolume { get; set; } = true;

    /// <summary>Shows the volume on the island whenever it changes, from the keyboard keys or anywhere else.</summary>
    public bool ShowVolumeChanges { get; set; } = true;

    /// <summary>Says which device sound moved to, such as headphones that just connected.</summary>
    public bool ShowAudioDevices { get; set; } = true;

    /// <summary>Shows microphone mute changes, and a reminder while an app listens to a muted microphone.</summary>
    public bool ShowMicMute { get; set; } = true;

    /// <summary>Shows a call while one is going on, with its length and a mute button, and warns when you talk while muted.</summary>
    public bool ShowCalls { get; set; } = true;

    /// <summary>Names the desktop you land on when you switch virtual desktops.</summary>
    public bool ShowDesktopSwitches { get; set; } = true;

    /// <summary>Shows downloads while they arrive and when they finish, with a button to find the file.</summary>
    public bool ShowDownloads { get; set; } = true;

    /// <summary>Shows each new screenshot with a preview and a button to open it.</summary>
    public bool ShowScreenshots { get; set; } = true;

    /// <summary>
    /// Off unless the user turns it on: reads copied text to spot links, email addresses, phone numbers and colours and
    /// offer what to do with them. The text is looked at on this PC once and never kept or logged.
    /// </summary>
    public bool SmartCopy { get; set; }

    /// <summary>
    /// Off unless the user turns it on: keeps the last 25 pieces of copied text, in memory only, for the Clipboard page.
    /// Copies that apps mark as private are skipped.
    /// </summary>
    public bool ClipboardHistory { get; set; }

    /// <summary>Shows the screen brightness when it changes, on PCs with a built-in screen.</summary>
    public bool ShowBrightness { get; set; } = true;

    /// <summary>Says when battery saver turns on or off.</summary>
    public bool ShowBatterySaver { get; set; } = true;

    /// <summary>Shows Caps Lock and Num Lock turning on or off.</summary>
    public bool ShowLockKeys { get; set; } = true;

    /// <summary>Says when a paired Bluetooth device, such as earbuds, connects or disconnects.</summary>
    public bool ShowDevices { get; set; } = true;

    /// <summary>Adds the Glance page to the panel: live CPU, memory, disk and network, and quick actions.</summary>
    public bool ShowGlance { get; set; } = true;

    /// <summary>The shortcut that opens the panel, such as "Ctrl+Alt+I". Empty turns the shortcut off.</summary>
    public string Hotkey { get; set; } = HotkeyGesture.Default;

    public IslandMonitor Monitor { get; set; } = IslandMonitor.Main;

    public bool HideInFullScreen { get; set; } = true;

    public bool ShowClockWhenIdle { get; set; } = true;

    /// <summary>The ferrofluid edge: the underside ripples with the music, and when the pointer touches it (ADR-015).</summary>
    public bool SoundRipples { get; set; } = true;

    /// <summary>Labs: the island's colour spills onto the desktop under it like a lamp (ADR-015).</summary>
    public bool LightHalo { get; set; }

    /// <summary>Labs: scripts, terminals, builds and AI agents can show live progress on the island (ADR-016). Off by default.</summary>
    public bool LiveActivities { get; set; }

    /// <summary>Labs: a Peek page that shows a live view of any window you pick (plan C).</summary>
    public bool WindowPeek { get; set; }

    /// <summary>Labs: drag a window onto the island to tuck it away, and bring it back from the Stash page (plan D).</summary>
    public bool WindowStash { get; set; }

    /// <summary>Activities torn off into floating droplets, and where each sits (screen pixels).</summary>
    public List<DropletPlace> Droplets { get; set; } = [];

    public IslandEffects Effects { get; set; } = IslandEffects.Full;

    public IslandLook Look { get; set; } = IslandLook.Onyx;

    /// <summary>Shows Windows notifications on the island (ADR-014). Off until the user turns it on and Windows allows it.</summary>
    public bool ShowNotifications { get; set; }

    public NotificationPrivacy NotificationPrivacy { get; set; } = NotificationPrivacy.Full;

    /// <summary>Shows only the app's name while a full-screen presentation or video is on screen.</summary>
    public bool HideNotificationsWhilePresenting { get; set; } = true;

    /// <summary>Per-app choices, keyed by the app's AppUserModelID. Apps are added as their first notification arrives.</summary>
    public Dictionary<string, NotificationAppSettings> NotificationApps { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public IslandInterruptions Interruptions { get; set; } = IslandInterruptions.Everything;

    /// <summary>Plays a soft Windows sound when a timer ends or the battery runs low. Off by default.</summary>
    public bool Sounds { get; set; }

    /// <summary>True once the first-run tour has played (it can be replayed from settings).</summary>
    public bool TourSeen { get; set; }

    public IslandHints SeenHints { get; set; }

    public int FocusMinutes { get; set; } = 25;

    public int BreakMinutes { get; set; } = 5;

    public IEnumerable<string> Normalize()
    {
        int focus = Math.Clamp(FocusMinutes, MinFocusMinutes, MaxFocusMinutes);
        if (focus != FocusMinutes)
        {
            FocusMinutes = focus;
            yield return $"focusMinutes clamped to {focus}";
        }

        int breakLength = Math.Clamp(BreakMinutes, MinBreakMinutes, MaxBreakMinutes);
        if (breakLength != BreakMinutes)
        {
            BreakMinutes = breakLength;
            yield return $"breakMinutes clamped to {breakLength}";
        }

        if (!Enum.IsDefined(Effects))
        {
            Effects = IslandEffects.Full;
            yield return "effects reset to Full";
        }

        if (!Enum.IsDefined(Interruptions))
        {
            Interruptions = IslandInterruptions.Everything;
            yield return "interruptions reset to Everything";
        }

        if (!Enum.IsDefined(NotificationPrivacy))
        {
            NotificationPrivacy = NotificationPrivacy.Full;
            yield return "notificationPrivacy reset to Full";
        }

        if (!Enum.IsDefined(Look))
        {
            Look = IslandLook.Onyx;
            yield return "look reset to Onyx";
        }

        if (!Enum.IsDefined(Monitor))
        {
            Monitor = IslandMonitor.Main;
            yield return "monitor reset to Main";
        }

        if (Hotkey is null || (Hotkey.Length > 0 && !HotkeyGesture.TryParse(Hotkey, out _)))
        {
            Hotkey = HotkeyGesture.Default;
            yield return "hotkey reset to the default";
        }
    }
}
