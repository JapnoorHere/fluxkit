using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>Dropping files on the island: the shelf opens with a glowing drop zone and keeps what lands on it.</summary>
public sealed partial class IslandPill
{
    /// <summary>A file dragged onto the island opens the shelf with its drop zone glowing.</summary>
    private void OnDragEnter(object sender, DragEventArgs e)
    {
        _dragLeaveTimer.Stop();
        if (_dragging || ShelfView.IsDraggingOut || !_scene.Settings.ShelfEnabled || !e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        _dragging = true;
        _peekTimer.Stop();
        _closeTimer.Stop();
        _lingerTimer.Stop();
        _page = PanelPage.Shelf;
        if (_mode == PillMode.Panel)
        {
            Update();
        }
        else
        {
            SetMode(PillMode.Panel);
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        // The shelf keeps a reference to the file and never moves it, so it asks for a link (or a copy, which the
        // source treats the same way: the original stays where it is).
        e.AcceptedOperation = e.AllowedOperations.HasFlag(DataPackageOperation.Link) ? DataPackageOperation.Link : e.AllowedOperations & DataPackageOperation.Copy;
        e.DragUIOverride.Caption = Strings.Get("Island_ShelfDropCaption");
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
        DragOperationDeferral deferral = e.GetDeferral();
        try
        {
            IReadOnlyList<IStorageItem> items = await e.DataView.GetStorageItemsAsync();
            Commands?.AddToShelf(items.Select(i => i.Path).Where(p => !string.IsNullOrEmpty(p)));
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or UnauthorizedAccessException)
        {
            // The source took the data back (or never had real files); nothing lands on the shelf.
            e.AcceptedOperation = DataPackageOperation.None;
        }
        finally
        {
            deferral.Complete();
        }

        // The tool's update brings the new tiles; meanwhile the zone settles. The shelf stays open a moment to show them.
        Update();
        _shape.Bounce(Springs);
        _lingerTimer.Start();
    }
}
