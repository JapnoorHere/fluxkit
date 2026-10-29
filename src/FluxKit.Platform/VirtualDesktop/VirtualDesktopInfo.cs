namespace FluxKit.Platform.VirtualDesktop;

/// <summary>Immutable snapshot of one Windows virtual desktop.</summary>
/// <param name="Id">Stable desktop GUID assigned by Windows.</param>
/// <param name="Index">Zero-based position in the Task View order.</param>
/// <param name="Name">User-visible name; "Desktop N" when the user never renamed it.</param>
/// <param name="WallpaperPath">Per-desktop wallpaper, if Windows stores one.</param>
/// <param name="IsCurrent">True for the desktop currently shown.</param>
public sealed record VirtualDesktopInfo(Guid Id, int Index, string Name, string? WallpaperPath, bool IsCurrent)
{
    public static string DefaultName(int index) => $"Desktop {index + 1}";
}
