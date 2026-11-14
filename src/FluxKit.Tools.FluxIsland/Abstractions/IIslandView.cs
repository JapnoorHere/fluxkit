using FluxKit.Tools.FluxIsland.Activities;

namespace FluxKit.Tools.FluxIsland.Abstractions;

/// <summary>The island on screen. Implemented by the App; both methods are thread-safe and return at once.</summary>
public interface IIslandView
{
    void Render(IslandScene scene);

    /// <summary>Opens the panel and gives it keyboard focus, or closes it if it is already open (the shortcut).</summary>
    void TogglePanel();

    /// <summary>Hides the island completely, e.g. when the tool is turned off.</summary>
    void Close();
}
