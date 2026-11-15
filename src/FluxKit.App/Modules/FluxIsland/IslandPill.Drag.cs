using System.Runtime.InteropServices;
using FluxKit.Platform.Shell;
using FluxKit.Tools.FluxIsland.Activities;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>
/// Dropping files on the island: it opens into drop zones (shelf, copy path, zip, convert, resize), the zone under the
/// pointer lights up, and dropping there runs it. Results land on the shelf with a moment that confirms them.
/// </summary>
public sealed partial class IslandPill
{
    private DropAction? _dropAction;

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        _dragLeaveTimer.Stop();
        if (_dragging || ShelfView.IsDraggingOut || !_scene.Settings.ShelfEnabled || !e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        _dragging = true;
        _dropAction = null;
        DropView.SetFiles([]);
        DropView.Highlight(null, Springs);
        _ = ReadDraggedFilesAsync(e.DataView);
        _peekTimer.Stop();
        _closeTimer.Stop();
        _lingerTimer.Stop();
        if (_mode == PillMode.Panel)
        {
            Update();
        }
        else
        {
            SetMode(PillMode.Panel);
        }
    }

    /// <summary>Looks at what is being dragged, so the image zones light up only for images. Some apps only allow it on drop.</summary>
    private async Task ReadDraggedFilesAsync(DataPackageView data)
    {
        try
        {
            IReadOnlyList<IStorageItem> items = await data.GetStorageItemsAsync();
            DropView.SetFiles([.. items.Select(i => i.Path).Where(p => !string.IsNullOrEmpty(p))]);
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or UnauthorizedAccessException)
        {
            // Not readable until the drop: image zones stay dimmed.
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        _dropAction = DropView.ZoneAt(e.GetPosition(DropView));
        DropView.Highlight(_dropAction, Springs);

        // Nothing here moves the originals, so ask for a link (or a copy, which sources treat the same way).
        e.AcceptedOperation = _dropAction is null
            ? DataPackageOperation.None
            : e.AllowedOperations.HasFlag(DataPackageOperation.Link) ? DataPackageOperation.Link : e.AllowedOperations & DataPackageOperation.Copy;
        e.DragUIOverride.Caption = _dropAction is { } action ? DropZones.Caption(action) : string.Empty;
        e.Handled = true;
    }

    /// <summary>Moving between the island's own parts raises leave then enter, so the drag ends only after a short pause.</summary>
    private void OnDragLeave(object sender, DragEventArgs e)
    {
        if (_dragging)
        {
            _dragLeaveTimer.Start();
        }
    }

    private void EndDrag()
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        Update();
        if (!_hovering)
        {
            _closeTimer.Start();
        }
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        _dragLeaveTimer.Stop();
        _dragging = false;
        DropAction action = _dropAction ?? DropAction.Shelf;
        List<string> paths = [];
        DragOperationDeferral deferral = e.GetDeferral();
        try
        {
            IReadOnlyList<IStorageItem> items = await e.DataView.GetStorageItemsAsync();
            paths = [.. items.Select(i => i.Path).Where(p => !string.IsNullOrEmpty(p))];
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or UnauthorizedAccessException)
        {
            // The source took the data back (or never had real files); nothing happens.
            e.AcceptedOperation = DataPackageOperation.None;
        }
        finally
        {
            deferral.Complete();
        }

        // The shelf opens to show where the results landed.
        _page = PanelPage.Shelf;
        Update();
        _shape.Bounce(Springs);
        _lingerTimer.Start();
        if (paths.Count > 0)
        {
            await RunDropAsync(action, paths);
        }
    }

    /// <summary>Does what the zone says. File jobs run off the UI thread; their results are added to the shelf.</summary>
    private async Task RunDropAsync(DropAction action, List<string> paths)
    {
        if (Commands is not { } commands)
        {
            return;
        }

        try
        {
            switch (action)
            {
                case DropAction.Shelf:
                    commands.AddToShelf(paths);
                    break;
                case DropAction.CopyPath:
                    var package = new DataPackage();
                    package.SetText(string.Join(Environment.NewLine, paths));
                    Clipboard.SetContent(package);
                    commands.ShowFileResult(FileResult.PathCopied, paths.Count == 1 ? paths[0] : null, paths.Count);
                    break;
                case DropAction.Zip:
                    string zip = await FileTransforms.ZipAsync(paths);
                    commands.AddToShelf([zip]);
                    commands.ShowFileResult(FileResult.Zipped, zip, paths.Count);
                    break;
                case DropAction.Convert or DropAction.Resize:
                    List<string> made = [];
                    foreach (string image in paths.Where(FileTransforms.IsImage))
                    {
                        made.Add(action == DropAction.Convert ? await FileTransforms.ConvertImageAsync(image) : await FileTransforms.ResizeImageAsync(image));
                    }

                    commands.AddToShelf(made);
                    commands.ShowFileResult(action == DropAction.Convert ? FileResult.Converted : FileResult.Resized, made.FirstOrDefault(), made.Count);
                    break;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException or InvalidDataException or ArgumentException)
        {
            Serilog.Log.Warning(ex, "A drop action ({Action}) failed", action);
            commands.ShowFileResult(FileResult.Failed, null, paths.Count);
        }
    }
}
