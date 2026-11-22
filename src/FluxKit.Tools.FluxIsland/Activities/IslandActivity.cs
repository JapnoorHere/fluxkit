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

    /// <summary>A file job from a drop finished (zipped, converted, resized, path copied) or failed.</summary>
    FileReady,

    /// <summary>A notification from another app (WhatsApp, Teams, mail…), shown in place of Windows' banner.</summary>
    Notification,

    /// <summary>A Live Activity from a script, terminal, build or AI agent: still going (plan B).</summary>
    Live,

    /// <summary>A Live Activity that just finished (Flag: it failed).</summary>
    LiveDone,

    /// <summary>A window was dragged onto the island and tucked away (Value: how many are tucked away now).</summary>
    Stashed,

    /// <summary>A call is going on (a call app is using the microphone): its length and the mute state.</summary>
    Call,
}

/// <summary>One thing to show. Carries data only; the App turns it into text, so every string stays in resources.</summary>
public sealed record IslandActivity(ActivityKind Kind)
{
    /// <summary>Battery percent, volume percent or the number of files on the shelf, depending on the kind.</summary>
    public int Value { get; init; }

    public MediaInfo? Media { get; init; }

    public FocusTimerSnapshot? Timer { get; init; }

    public StopwatchSnapshot? Stopwatch { get; init; }

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

    /// <summary>When something that is still going on began, such as a call.</summary>
    public DateTimeOffset? Since { get; init; }

    /// <summary>A second line of text, such as a notification's message. Shown on screen only, never logged.</summary>
    public string? Detail { get; init; }

    /// <summary>Where a moment came from, such as the app that sent a notification.</summary>
    public string? Source { get; init; }

    /// <summary>An image to show with the moment, such as the sending app's logo.</summary>
    public ReadOnlyMemory<byte> Image { get; init; }

    public bool IsAlert => Kind is not (ActivityKind.Media or ActivityKind.Timer or ActivityKind.Shelf or ActivityKind.Download or ActivityKind.MicMuted or ActivityKind.Stopwatch or ActivityKind.Call or ActivityKind.Live);
}
