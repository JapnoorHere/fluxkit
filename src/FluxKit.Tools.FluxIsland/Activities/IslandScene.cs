using FluxKit.Tools.FluxIsland.Abstractions;

namespace FluxKit.Tools.FluxIsland.Activities;

/// <summary>
/// Everything the view needs for one frame of the island: what the compact pill shows (<see cref="Primary"/>, with an
/// optional small <see cref="Secondary"/> on the right), and the full state for the open panel.
/// </summary>
public sealed record IslandScene
{
    public static IslandScene Empty { get; } = new();

    public IslandActivity? Primary { get; init; }

    public IslandActivity? Secondary { get; init; }

    public MediaInfo? Media { get; init; }

    public FocusTimerSnapshot? Timer { get; init; }

    public StopwatchSnapshot? Stopwatch { get; init; }

    public IReadOnlyList<string> Shelf { get; init; } = [];

    /// <summary>Quick notes from the command bar, newest first.</summary>
    public IReadOnlyList<IslandNote> Notes { get; init; } = [];

    /// <summary>Recent moments worth looking back at, newest first.</summary>
    public IReadOnlyList<RewindEntry> Rewind { get; init; } = [];

    /// <summary>Recently copied text, when clipboard history is on; pinned items first.</summary>
    public IReadOnlyList<ClipEntry> Clips { get; init; } = [];

    public PowerSnapshot Power { get; init; }

    public PrivacySnapshot Privacy { get; init; }

    /// <summary>True when the default microphone is muted.</summary>
    public bool MicMuted { get; init; }

    /// <summary>True while a full-screen app is in front and the island should stay out of the way.</summary>
    public bool Hidden { get; init; }

    /// <summary>The settings that change how the island looks or what it offers.</summary>
    public FluxIslandSettings Settings { get; init; } = new();
}
