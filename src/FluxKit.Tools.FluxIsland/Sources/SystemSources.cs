using FluxKit.Platform.VirtualDesktop;
using FluxKit.Tools.FluxIsland.Abstractions;
using Serilog;

namespace FluxKit.Tools.FluxIsland.Sources;

/// <summary>The real Windows event sources for the island. Tests pass fakes through <see cref="IslandSources"/> instead.</summary>
public static class SystemSources
{
    public static IslandSources Create(ILogger log, IVirtualDesktopService? desktops = null) => new()
    {
        Media = () => new MediaSource(log),
        Power = () => new PowerSource(),
        Network = () => new NetworkSource(),
        Privacy = () => new PrivacySource(log),
        Clipboard = () => new ClipboardSource(log),
        FullScreen = () => new FullScreenSource(log),
        Volume = () => new VolumeSource(),
        Audio = () => new AudioSource(log),
        Desktops = desktops is null ? null : () => new DesktopSource(desktops),
        Files = (downloads, screenshots) => new FileMomentsSource(downloads, screenshots, TimeProvider.System, log),
    };
}
