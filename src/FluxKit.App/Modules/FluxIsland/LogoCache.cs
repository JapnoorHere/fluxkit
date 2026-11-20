using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>
/// App logos for notification rows, decoded once per app at row size. Small (a few apps), and cleared with the inbox.
/// </summary>
internal sealed class LogoCache
{
    private const int DecodeSize = 28;

    private readonly Dictionary<string, ImageSource> _logos = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The app's logo, or null when it has none. A new logo shows as soon as it has decoded.</summary>
    public ImageSource? Get(string appId, ReadOnlyMemory<byte> bytes)
    {
        if (_logos.TryGetValue(appId, out ImageSource? known))
        {
            return known;
        }

        if (bytes.IsEmpty)
        {
            return null;
        }

        var bitmap = new BitmapImage { DecodePixelWidth = DecodeSize, DecodePixelType = DecodePixelType.Logical };
        _logos[appId] = bitmap;
        _ = LoadAsync(bitmap, bytes);
        return bitmap;
    }

    public void Clear() => _logos.Clear();

    private static async Task LoadAsync(BitmapImage bitmap, ReadOnlyMemory<byte> bytes)
    {
        try
        {
            using var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(bytes.ToArray().AsBuffer());
            stream.Seek(0);
            await bitmap.SetSourceAsync(stream);
        }
        catch (Exception)
        {
            // A logo Windows can't decode just leaves the row's glyph badge empty.
        }
    }
}
