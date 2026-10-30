using FluxKit.Core.Settings;

namespace FluxKit.Tools.FluxIsland;

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
    }
}
