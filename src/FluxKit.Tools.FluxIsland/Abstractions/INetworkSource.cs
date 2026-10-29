namespace FluxKit.Tools.FluxIsland.Abstractions;

/// <summary>Whether the PC has internet access.</summary>
public interface INetworkSource : IIslandSource
{
    bool IsOnline { get; }

    event EventHandler? Changed;
}
