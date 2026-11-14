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

    public bool ScrollForVolume { get; set; } = true;

    /// <summary>Shows the volume on the island whenever it changes, from the keyboard keys or anywhere else.</summary>
    public bool ShowVolumeChanges { get; set; } = true;

    /// <summary>Says which device sound moved to, such as headphones that just connected.</summary>
    public bool ShowAudioDevices { get; set; } = true;

    /// <summary>Shows microphone mute changes, and a reminder while an app listens to a muted microphone.</summary>
    public bool ShowMicMute { get; set; } = true;

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

    public IslandEffects Effects { get; set; } = IslandEffects.Full;

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
