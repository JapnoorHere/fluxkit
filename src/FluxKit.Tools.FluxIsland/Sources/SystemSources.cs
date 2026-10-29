using FluxKit.Tools.FluxIsland.Abstractions;
using Serilog;

namespace FluxKit.Tools.FluxIsland.Sources;

/// <summary>The real Windows event sources for the island. Tests pass fakes through <see cref="IslandSources"/> instead.</summary>
public static class SystemSources
{
    public static IslandSources Create(ILogger log) => new()
    {
        Media = () => new MediaSource(log),
        Power = () => new PowerSource(),
        Network = () => new NetworkSource(),
        Privacy = () => new PrivacySource(log),
        Clipboard = () => new ClipboardSource(log),
        FullScreen = () => new FullScreenSource(log),
    };
}
