namespace FluxKit.Tools.FluxIsland.Abstractions;

/// <summary>The shortcut that opens the island's panel was pressed. Events may arrive on any thread.</summary>
public interface IHotkeySource : IIslandSource
{
    event EventHandler? Pressed;
}
