using System.Runtime.InteropServices;

namespace FluxKit.Platform.Shell;

/// <summary>The user's special folders, wherever they have moved them (for example Downloads on another drive).</summary>
public static partial class KnownFolders
{
    private static readonly Guid DownloadsId = new("374DE290-123F-4565-9164-39C4925E467B");
    private static readonly Guid ScreenshotsId = new("B7BEDE81-DF94-4682-A7D8-57A52620B86F");

    /// <summary>The Downloads folder, or null if Windows can't say where it is.</summary>
    public static string? Downloads => Find(DownloadsId);

    /// <summary>Pictures\Screenshots, where Print Screen and the Snipping Tool save, or null if it doesn't exist yet.</summary>
    public static string? Screenshots => Find(ScreenshotsId);

    private static string? Find(Guid id)
    {
        if (SHGetKnownFolderPath(id, 0, 0, out nint path) != 0)
        {
            return null;
        }

        try
        {
            string? folder = Marshal.PtrToStringUni(path);
            return Directory.Exists(folder) ? folder : null;
        }
        finally
        {
            Marshal.FreeCoTaskMem(path);
        }
    }

    [LibraryImport("shell32.dll")]
    private static partial int SHGetKnownFolderPath(in Guid id, uint flags, nint token, out nint path);
}
