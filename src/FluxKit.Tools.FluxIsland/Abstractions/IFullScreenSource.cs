namespace FluxKit.Tools.FluxIsland.Abstractions;

/// <summary>Whether a full-screen app, game or presentation is in front.</summary>
public interface IFullScreenSource : IIslandSource
{
    bool IsFullScreen { get; }

    event EventHandler? Changed;
}
