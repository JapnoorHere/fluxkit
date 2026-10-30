namespace FluxKit.Tools.FluxIsland.Abstractions;

/// <summary>Factories for the island's event sources. A missing factory simply leaves that feature out.</summary>
public sealed class IslandSources
{
    public Func<IMediaSource>? Media { get; init; }

    public Func<IPowerSource>? Power { get; init; }

    public Func<INetworkSource>? Network { get; init; }

    public Func<IPrivacySource>? Privacy { get; init; }

    public Func<IClipboardSource>? Clipboard { get; init; }

    public Func<IFullScreenSource>? FullScreen { get; init; }

    public Func<IVolumeControl>? Volume { get; init; }

    public Func<IAudioSource>? Audio { get; init; }
}
