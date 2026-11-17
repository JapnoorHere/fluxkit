using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.UI;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>
/// The current track's art, decoded once per track at the size the panel needs, and its glow colour: the average
/// colour of an 8 × 8 thumbnail, made bright enough to glow on the dark pill. Only the current track is kept.
/// </summary>
internal sealed class AlbumArt
{
    private const int DecodeSize = 112;

    private ReadOnlyMemory<byte> _bytes;

    public ImageSource? Image { get; private set; }

    /// <summary>
    /// The art decoded at 6 pixels wide: stretched to fill the music panel, smooth scaling turns it into a soft blur of
    /// the album's colours, with no blur effect at all.
    /// </summary>
    public ImageSource? Wash { get; private set; }

    public Color Accent { get; private set; } = IslandPalette.Violet;

    /// <summary>Raised on the UI thread when <see cref="Image"/> and <see cref="Accent"/> change.</summary>
    public event EventHandler? Changed;

    public void Set(ReadOnlyMemory<byte> bytes)
    {
        if (bytes.Equals(_bytes))
        {
            return;
        }

        _bytes = bytes;
        if (bytes.IsEmpty)
        {
            Apply(null, null, IslandPalette.Violet);
            return;
        }

        _ = LoadAsync(bytes);
    }

    private async Task LoadAsync(ReadOnlyMemory<byte> bytes)
    {
        ImageSource? image = null;
        ImageSource? wash = null;
        Color accent = IslandPalette.Violet;
        try
        {
            using var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(bytes.ToArray().AsBuffer());
            stream.Seek(0);
            var bitmap = new BitmapImage { DecodePixelWidth = DecodeSize, DecodePixelType = DecodePixelType.Logical };
            await bitmap.SetSourceAsync(stream);
            image = bitmap;

            stream.Seek(0);
            var tiny = new BitmapImage { DecodePixelWidth = 6, DecodePixelType = DecodePixelType.Physical };
            await tiny.SetSourceAsync(stream);
            wash = tiny;

            stream.Seek(0);
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
            var transform = new BitmapTransform { ScaledWidth = 8, ScaledHeight = 8, InterpolationMode = BitmapInterpolationMode.Fant };
            PixelDataProvider pixels = await decoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, transform, ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage);
            accent = Glow(pixels.DetachPixelData());
        }
        catch (Exception)
        {
            // Art in a format Windows can't decode just means no art; the music still shows.
        }

        if (bytes.Equals(_bytes))
        {
            Apply(image, wash, accent);
        }
    }

    private void Apply(ImageSource? image, ImageSource? wash, Color accent)
    {
        Image = image;
        Wash = wash;
        Accent = accent;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The average colour, with saturation and brightness lifted so it glows. Grey art glows soft white.</summary>
    internal static Color Glow(byte[] bgra)
    {
        double b = 0, g = 0, r = 0;
        int count = bgra.Length / 4;
        if (count == 0)
        {
            return IslandPalette.Violet;
        }

        for (int i = 0; i < bgra.Length; i += 4)
        {
            b += bgra[i];
            g += bgra[i + 1];
            r += bgra[i + 2];
        }

        (double hue, double saturation, double value) = ToHsv(r / count / 255, g / count / 255, b / count / 255);
        saturation = saturation < 0.08 ? saturation : Math.Max(saturation, 0.5);
        value = Math.Max(value, 0.85);
        return FromHsv(hue, saturation, value);
    }

    private static (double H, double S, double V) ToHsv(double r, double g, double b)
    {
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;
        double hue = delta == 0 ? 0
            : max == r ? 60 * (((g - b) / delta) % 6)
            : max == g ? 60 * (((b - r) / delta) + 2)
            : 60 * (((r - g) / delta) + 4);
        return (hue < 0 ? hue + 360 : hue, max == 0 ? 0 : delta / max, max);
    }

    private static Color FromHsv(double h, double s, double v)
    {
        double c = v * s;
        double x = c * (1 - Math.Abs((h / 60 % 2) - 1));
        double m = v - c;
        (double r, double g, double b) = (int)(h / 60) switch
        {
            0 => (c, x, 0d),
            1 => (x, c, 0d),
            2 => (0d, c, x),
            3 => (0d, x, c),
            4 => (x, 0d, c),
            _ => (c, 0d, x),
        };
        return Color.FromArgb(0xFF, (byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
    }
}
