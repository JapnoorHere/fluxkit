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

    public Func<ILockKeysSource>? LockKeys { get; init; }

    public Func<IDeviceSource>? Devices { get; init; }

    /// <summary>Registers the given shortcut; the source reports each press.</summary>
    public Func<Activities.HotkeyGesture, IHotkeySource>? Hotkey { get; init; }

    /// <summary>Windows notifications from other apps (needs FluxKit's identity and the user's permission).</summary>
    public Func<INotificationSource>? Notifications { get; init; }

    /// <summary>Windows' per-app banner switch; null leaves banners alone.</summary>
    public IBannerSwitch? Banners { get; init; }

    /// <summary>Where the banner values from before FluxKit changed them are kept; null keeps them in memory only.</summary>
    public IBannerSnapshotStore? BannerStore { get; init; }

    /// <summary>Where the shelf is kept between runs; null keeps it in memory only.</summary>
    public IShelfStore? ShelfStore { get; init; }

    /// <summary>Where quick notes are kept between runs; null keeps them in memory only.</summary>
    public INotesStore? NotesStore { get; init; }
}
