using FluxKit.App.Modules.FluxIsland.Design;
using Microsoft.UI.Xaml;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>
/// The live music level: it drives the bars and the ferrofluid edge (ADR-015). The meter runs only while music plays
/// and something showing it is on screen.
/// </summary>
public sealed partial class IslandPill
{
    private bool _rippling;
    private bool _previewRipple;

    private void UpdateMeter(UIElement view)
    {
        bool playing = _scene.Media?.IsPlaying == true;
        bool compactBars = view == CompactView && CompactBars.Visibility == Visibility.Visible;
        bool panelBars = _mode == PillMode.Panel && _page == PanelPage.Media;
        bool ripple = compactBars && _scene.Settings.SoundRipples && !HighContrast;
        _meter.SetRunning(_active && Springs && playing && (compactBars || panelBars));
        SetRippling(_previewRipple || (_active && Springs && playing && ripple));
    }

    /// <summary>
    /// Starts or stops the edge following the music. While it does, the window region reaches a few pixels lower so
    /// the bumps aren't clipped; when it stops, the bumps settle back into the edge.
    /// </summary>
    private void SetRippling(bool on)
    {
        if (on == _rippling)
        {
            return;
        }

        _rippling = on;
        if (!on)
        {
            _shape.Ripple(0);
        }

        MorphTo(_size);
    }

    private void ShowLevel(float level)
    {
        if (_mode == PillMode.Panel)
        {
            MediaView.SetLevel(level);
            return;
        }

        CompactBars.SetLevel(level);
        if (_rippling)
        {
            // A preview plays no sound: it shows the edge at a lively fixed level instead.
            _shape.Ripple(_previewRipple ? 0.9f : level);
        }
    }
}
