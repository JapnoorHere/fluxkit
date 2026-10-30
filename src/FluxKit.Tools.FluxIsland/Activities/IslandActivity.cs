using FluxKit.Tools.FluxIsland.Abstractions;

namespace FluxKit.Tools.FluxIsland.Activities;

/// <summary>
/// Everything the island can show. The first group stays while it lasts (music, a running timer, files on the shelf);
/// the rest are short alerts that take over the pill for a moment.
/// </summary>
public enum ActivityKind
{
    Media,
    Timer,
    Shelf,
    TimerDone,
    PluggedIn,
    Unplugged,
    LowBattery,
    Offline,
    Online,
    Copied,
    Volume,
    Download,
    DownloadDone,
    Screenshot,
    DesktopSwitched,
    AudioDevice,
    MicMuted,
    Brightness,
    CapsLock,
    NumLock,
    BatterySaver,
    DeviceConnected,
    DeviceDisconnected,
    Stopwatch,
}

/// <summary>One thing to show. Carries data only; the App turns it into text, so every string stays in resources.</summary>
public sealed record IslandActivity(ActivityKind Kind)
{
    /// <summary>Battery percent, volume percent or the number of files on the shelf, depending on the kind.</summary>
    public int Value { get; init; }

    public MediaInfo? Media { get; init; }

    public FocusTimerSnapshot? Timer { get; init; }

    public ClipboardKind Clipboard { get; init; }

    /// <summary>What copied text turned out to be, when smart copy is on.</summary>
    public SmartText Smart { get; init; }

    /// <summary>A name to show: a file, a device or a desktop. Shown on screen only, never logged.</summary>
    public string? Text { get; init; }

    /// <summary>The file a moment is about (a download or a screenshot), for its actions. Never logged.</summary>
    public string? Path { get; init; }

    /// <summary>Progress from 0 to 1, or null when it isn't known.</summary>
    public double? Progress { get; init; }

    /// <summary>On or off, for toggles such as Caps Lock, mic mute or battery saver.</summary>
    public bool Flag { get; init; }

    /// <summary>Extra numbers some moments carry, such as the desktop count beside the desktop index in <see cref="Value"/>.</summary>
    public int Total { get; init; }

    public bool IsAlert => Kind is not (ActivityKind.Media or ActivityKind.Timer or ActivityKind.Shelf or ActivityKind.Download or ActivityKind.MicMuted or ActivityKind.Stopwatch);
}
