using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>
/// Window stash (plan D): while a window is dragged over the island, the pill swells into a lip that says it will take
/// it, and ripples as if touched. The island window does the dragging work; this only shows the invitation.
/// </summary>
public sealed partial class IslandPill
{
    private bool _stashLip;

    /// <summary>Shows or hides the "drop to tuck it away" lip while a window is dragged over the island.</summary>
    public void SetStashLip(bool on)
    {
        if (on == _stashLip)
        {
            return;
        }

        _stashLip = on;
        if (on && _mode != PillMode.Compact)
        {
            // A window dragged over an open island: fold it back to the pill, which then offers to take the window.
            SetMode(PillMode.Compact);
        }

        _shape.ScaleTo(on ? 1.08f : 1, Springs);
        if (on)
        {
            MakeLiquidRoom();
            _shape.Wave(Springs);
        }

        _shownKey = null;
        Update();
    }

    /// <summary>The lip, while a window hovers over the island; null otherwise.</summary>
    private (UIElement View, string Key, Size Size)? BuildStashLip() =>
        _stashLip
            ? FillCompact(new CompactContent(IslandPresenter.StashGlyph, Strings.Get("Island_StashDrop"), null, null, IslandPalette.Cyan), "stash-lip", twoLines: false, activity: null)
            : null;
}
