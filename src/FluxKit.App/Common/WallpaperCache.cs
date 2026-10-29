using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Win32;

namespace FluxKit.App.Common;

/// <summary>Decoded wallpaper previews, reused across picker sessions and tool previews until the file changes.</summary>
internal sealed class WallpaperCache
{
    private readonly Dictionary<string, BitmapImage> _images = [];

    /// <summary>The image for <paramref name="path"/> decoded at <paramref name="decodePixelWidth"/>, or null if the file is missing.</summary>
    public BitmapImage? Load(string? path, int decodePixelWidth)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return null;
        }

        string key = $"{path}|{File.GetLastWriteTimeUtc(path).Ticks}";
        if (!_images.TryGetValue(key, out BitmapImage? image))
        {
            image = new BitmapImage { DecodePixelWidth = decodePixelWidth, UriSource = new Uri(path) };
            _images[key] = image;
        }

        return image;
    }

    /// <summary>The wallpaper of the current desktop, used when a desktop has none of its own.</summary>
    public static string? CurrentWallpaper()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
        return key?.GetValue("WallPaper") as string;
    }
}
