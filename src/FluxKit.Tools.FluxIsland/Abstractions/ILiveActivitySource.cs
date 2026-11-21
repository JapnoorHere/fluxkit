namespace FluxKit.Tools.FluxIsland.Abstractions;

/// <summary>Raw Live Activity lines from scripts and apps on this PC (plan B). The tool parses and checks them.</summary>
public interface ILiveActivitySource : IIslandSource
{
    event EventHandler<string>? LineReceived;
}
