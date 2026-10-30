using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace FluxKit.App.Common;

/// <summary>The small preview Windows shows for a file in File Explorer: a picture's own image, or the file's icon.</summary>
internal static class FileThumbnail
{
    /// <summary>The preview of <paramref name="path"/> at about <paramref name="size"/> pixels, or null if there is none.</summary>
    public static async Task<ImageSource?> LoadAsync(string path, uint size)
    {
        try
        {
            StorageFile file = await StorageFile.GetFileFromPathAsync(path);
            using StorageItemThumbnail thumbnail = await file.GetThumbnailAsync(ThumbnailMode.SingleItem, size, ThumbnailOptions.UseCurrentScale);
            if (thumbnail is null)
            {
                return null;
            }

            var image = new BitmapImage();
            await image.SetSourceAsync(thumbnail);
            return image;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Runtime.InteropServices.COMException)
        {
            // The file is gone or unreadable; the moment shows its glyph instead. The path is never logged.
            return null;
        }
    }
}
