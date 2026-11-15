namespace FluxKit.Tools.FluxIsland.Activities;

/// <summary>What a line typed into the command bar asks for.</summary>
public enum CommandKind
{
    StartTimer,
    StartFocus,
    StartStopwatch,
    Lap,
    StopTimer,
    Calculate,
    ShowColor,
    AddNote,
    PlayPause,
    NextTrack,
    PreviousTrack,
    ToggleMic,
    Lock,
    Screenshot,
    ScreenOff,
    OpenPage,
    ClearShelf,
}

/// <summary>
/// One command from the command bar, with what it needs: a <see cref="Duration"/> for timers, a <see cref="Number"/>
/// for sums, a <see cref="Color"/> (0xRRGGBB) for colours and <see cref="Text"/> for notes and page names.
/// </summary>
public sealed record IslandCommand(CommandKind Kind)
{
    public TimeSpan Duration { get; init; }

    public double Number { get; init; }

    public int Color { get; init; }

    public string? Text { get; init; }
}
