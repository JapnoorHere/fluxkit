using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;

namespace FluxKit.App.Common;

/// <summary>
/// The Windows share sheet (Nearby Share, mail, Phone Link and other apps) for files, opened from one of FluxKit's
/// windows. Unpackaged apps reach it through the documented window interop.
/// </summary>
internal static class ShareSheet
{
    /// <summary>Shares <paramref name="paths"/> from <paramref name="hwnd"/>. Files that have gone are left out; returns false if none are left.</summary>
    public static bool Show(nint hwnd, IReadOnlyList<string> paths, string title)
    {
        string[] files = [.. paths.Where(File.Exists)];
        if (files.Length == 0)
        {
            return false;
        }

        DataTransferManager manager = DataTransferManagerInterop.GetForWindow(hwnd);
        TypedEventHandler<DataTransferManager, DataRequestedEventArgs>? handler = null;
        handler = async (sender, args) =>
        {
            sender.DataRequested -= handler;
            DataRequestDeferral deferral = args.Request.GetDeferral();
            try
            {
                var items = new List<IStorageItem>();
                foreach (string file in files)
                {
                    items.Add(await StorageFile.GetFileFromPathAsync(file));
                }

                args.Request.Data.Properties.Title = title;
                args.Request.Data.SetStorageItems(items, readOnly: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Runtime.InteropServices.COMException)
            {
                args.Request.FailWithDisplayText(Strings.Get("Island_ShareFailed"));
            }
            finally
            {
                deferral.Complete();
            }
        };
        manager.DataRequested += handler;
        DataTransferManagerInterop.ShowShareUIForWindow(hwnd);
        return true;
    }
}
