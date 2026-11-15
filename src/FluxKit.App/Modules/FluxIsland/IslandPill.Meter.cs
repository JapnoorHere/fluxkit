using FluxKit.App.Modules.FluxIsland.Design;
using Microsoft.UI.Xaml;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>The live music bars: the level meter runs only while music plays and its bars are on screen.</summary>
public sealed partial class IslandPill
{
    private void UpdateMeter(UIElement view)
    {
        bool playing = _scene.Media?.IsPlaying == true;
        bool compactBars = view == CompactView && CompactBars.Visibility == Visibility.Visible;
        bool panelBars = _mode == PillMode.Panel && _page == PanelPage.Media;
        _meter.SetRunning(_active && Springs && playing && (compactBars || panelBars));
    }

    private void ShowLevel(float level)
    {
        if (_mode == PillMode.Panel)
        {
            MediaView.SetLevel(level);
        }
        else
        {
            CompactBars.SetLevel(level);
        }
    }
}
