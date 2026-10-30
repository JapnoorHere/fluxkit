namespace FluxKit.Tools.FluxIsland.Abstractions;

/// <summary>Factories for the island's event sources. A missing factory simply leaves that feature out.</summary>
public sealed class IslandSources
{
    public Func<IMediaSource>? Media { get; init; }

    public Func<IPowerSource>? Power { get; init; }

    public Func<INetworkSource>? Network { get; init; }

    public Func<IPrivacySource>? Privacy { get; init; }

    /// <summary>Creates the clipboard watcher; the flag says whether it may read copied text (smart copy).</summary>
    public Func<bool, IClipboardSource>? Clipboard { get; init; }

    public Func<IFullScreenSource>? FullScreen { get; init; }

    public Func<IVolumeControl>? Volume { get; init; }

    public Func<IAudioSource>? Audio { get; init; }

    public Func<IDesktopSource>? Desktops { get; init; }

    /// <summary>Creates the downloads and screenshots watcher; the flags say which of the two folders to watch.</summary>
    public Func<bool, bool, IFileMomentsSource>? Files { get; init; }

    public Func<IBrightnessSource>? Brightness { get; init; }

    /// <summary>Where the shelf is kept between runs; null keeps it in memory only.</summary>
    public IShelfStore? ShelfStore { get; init; }
}
