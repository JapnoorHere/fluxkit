namespace FluxKit.Tools.FluxIsland.Abstractions;

/// <summary>Tells the island when the user lands on another virtual desktop, however they got there.</summary>
public interface IDesktopSource : IIslandSource
{
    /// <summary>Raised once per switch, on any thread.</summary>
    event EventHandler<DesktopLanding>? Switched;
}

/// <summary>The desktop the user is on now: its 1-based position, how many there are, and its name.</summary>
public readonly record struct DesktopLanding(int Number, int Count, string Name);
