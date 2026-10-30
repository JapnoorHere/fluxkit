namespace FluxKit.Tools.FluxIsland.Abstractions;

/// <summary>The built-in screen's brightness changed (0–100). Events may arrive on any thread.</summary>
public interface IBrightnessSource : IIslandSource
{
    event EventHandler<int>? Changed;
}
