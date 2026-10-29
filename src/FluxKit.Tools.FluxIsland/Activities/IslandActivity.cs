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
}

/// <summary>One thing to show. Carries data only; the App turns it into text, so every string stays in resources.</summary>
public sealed record IslandActivity(ActivityKind Kind)
{
    /// <summary>Battery percent, volume percent or the number of files on the shelf, depending on the kind.</summary>
    public int Value { get; init; }

    public MediaInfo? Media { get; init; }

    public FocusTimerSnapshot? Timer { get; init; }

    public ClipboardKind Clipboard { get; init; }

    public bool IsAlert => Kind is not (ActivityKind.Media or ActivityKind.Timer or ActivityKind.Shelf);
}
