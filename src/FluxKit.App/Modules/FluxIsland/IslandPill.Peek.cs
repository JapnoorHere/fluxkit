using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>
/// Window peek (plan C): while the Peek page is open with a window chosen, the island window draws that window's live
/// thumbnail over the page's frame. The frame's place is sent once the panel has settled, and the thumbnail is hidden
/// the moment the page closes or changes.
/// </summary>
public sealed partial class IslandPill
{
    private static readonly TimeSpan PeekSettle = TimeSpan.FromMilliseconds(260);

    private DispatcherQueueTimer? _peekSettle;
    private bool _peekShowing;

    /// <summary>
    /// The watched window, and where its live view goes in view pixels of the island window (null: hide it). The island
    /// window turns this into a DWM thumbnail.
    /// </summary>
    public event EventHandler<(nint Window, Rect? Area)>? PeekChanged;

    private void UpdatePeek()
    {
        bool showing = _active && _mode == PillMode.Panel && _page == PanelPage.Peek && PeekView.Pinned != 0;
        if (!showing)
        {
            _peekSettle?.Stop();
            if (_peekShowing)
            {
                _peekShowing = false;
                PeekChanged?.Invoke(this, (PeekView.Pinned, null));
            }

            return;
        }

        // Wait for the panel to finish growing, so the live view lands where the frame ends up.
        _peekSettle ??= OneShot(PeekSettle, PlacePeek);
        _peekSettle.Stop();
        _peekSettle.Start();
    }

    private void PlacePeek()
    {
        if (!(_mode == PillMode.Panel && _page == PanelPage.Peek && PeekView.Pinned != 0) || XamlRoot?.Content is not UIElement root)
        {
            return;
        }

        FrameworkElement frame = PeekView.ThumbArea;
        Rect area = frame.TransformToVisual(root).TransformBounds(new Rect(0, 0, frame.ActualWidth, frame.ActualHeight));
        _peekShowing = true;
        PeekChanged?.Invoke(this, (PeekView.Pinned, area));
    }
}
