using System.IO.Compression;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace FluxKit.Platform.Shell;

/// <summary>
/// Small file jobs for files dropped on the island: zip them, convert an image, or make a smaller copy. Every job
/// writes a new file next to the original with a name that is free, and never changes or replaces the original.
/// Uses only what Windows and .NET already have (no extra libraries). Jobs run off the UI thread.
/// </summary>
public static class FileTransforms
{
    private static readonly string[] ImageTypes = [".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff", ".heic", ".webp", ".jxr"];

    public static bool IsImage(string path) => ImageTypes.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// A free file name in <paramref name="folder"/>: <paramref name="name"/> itself, or "name (2).ext", "name (3).ext"
    /// and so on.
    /// </summary>
    public static string FreeName(string folder, string name)
    {
        string path = Path.Combine(folder, name);
        string stem = Path.GetFileNameWithoutExtension(name);
        string extension = Path.GetExtension(name);
        for (int i = 2; File.Exists(path) || Directory.Exists(path); i++)
        {
            path = Path.Combine(folder, $"{stem} ({i}){extension}");
        }

        return path;
    }

    /// <summary>
    /// Zips files and folders into one archive next to the first item: "Report.zip" for one item, "Archive.zip" for
    /// several. Returns the archive's path.
    /// </summary>
    public static Task<string> ZipAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken = default) => Task.Run(
        () =>
        {
            string folder = Path.GetDirectoryName(paths[0]) ?? throw new IOException("The file has no folder.");
            string name = paths.Count == 1 ? Path.GetFileNameWithoutExtension(paths[0].TrimEnd(Path.DirectorySeparatorChar)) + ".zip" : "Archive.zip";
            string target = FreeName(folder, name);
            using ZipArchive zip = ZipFile.Open(target, ZipArchiveMode.Create);
            foreach (string path in paths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Directory.Exists(path))
                {
                    string root = Path.GetDirectoryName(path.TrimEnd(Path.DirectorySeparatorChar)) ?? path;
                    foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        zip.CreateEntryFromFile(file, Path.GetRelativePath(root, file), CompressionLevel.Optimal);
                    }
                }
                else
                {
                    zip.CreateEntryFromFile(path, Path.GetFileName(path), CompressionLevel.Optimal);
                }
            }

            return target;
        },
        cancellationToken);

    /// <summary>Saves a PNG copy of an image (or a JPG copy, if it already is a PNG). Returns the new file's path.</summary>
    public static Task<string> ConvertImageAsync(string path)
    {
        bool toJpeg = Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase);
        string name = Path.GetFileNameWithoutExtension(path) + (toJpeg ? ".jpg" : ".png");
        return SaveImageAsync(path, name, toJpeg ? BitmapEncoder.JpegEncoderId : BitmapEncoder.PngEncoderId, scale: 1);
    }

    /// <summary>Saves a copy at half the width and height, in the same format family. Returns the new file's path.</summary>
    public static Task<string> ResizeImageAsync(string path)
    {
        bool jpeg = Path.GetExtension(path) is { } ext && (ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase));
        string name = $"{Path.GetFileNameWithoutExtension(path)} (half){(jpeg ? ".jpg" : ".png")}";
        return SaveImageAsync(path, name, jpeg ? BitmapEncoder.JpegEncoderId : BitmapEncoder.PngEncoderId, scale: 0.5);
    }

    private static async Task<string> SaveImageAsync(string path, string name, Guid encoderId, double scale)
    {
        string folder = Path.GetDirectoryName(path) ?? throw new IOException("The file has no folder.");
        string target = FreeName(folder, name);
        StorageFile source = await StorageFile.GetFileFromPathAsync(path);
        using IRandomAccessStream input = await source.OpenReadAsync();
        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(input);
        SoftwareBitmap bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

        StorageFolder destination = await StorageFolder.GetFolderFromPathAsync(folder);
        StorageFile output = await destination.CreateFileAsync(Path.GetFileName(target), CreationCollisionOption.FailIfExists);
        try
        {
            using IRandomAccessStream stream = await output.OpenAsync(FileAccessMode.ReadWrite);
            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(encoderId, stream);
            encoder.SetSoftwareBitmap(bitmap);
            if (scale != 1)
            {
                encoder.BitmapTransform.ScaledWidth = (uint)Math.Max(1, decoder.PixelWidth * scale);
                encoder.BitmapTransform.ScaledHeight = (uint)Math.Max(1, decoder.PixelHeight * scale);
                encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;
            }

            await encoder.FlushAsync();
        }
        catch
        {
            // Don't leave a half-written file behind.
            await output.DeleteAsync();
            throw;
        }

        return target;
    }
}
