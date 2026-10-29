using FluxKit.Tools.FluxIsland.Activities;

namespace FluxKit.Tools.FluxIsland.Abstractions;

/// <summary>The island on screen. Implemented by the App; both methods are thread-safe and return at once.</summary>
public interface IIslandView
{
    void Render(IslandScene scene);

    /// <summary>Hides the island completely, e.g. when the tool is turned off.</summary>
    void Close();
}
