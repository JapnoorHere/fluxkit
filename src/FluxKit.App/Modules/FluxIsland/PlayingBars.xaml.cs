using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>Three bars that bounce while music plays and rest low when it is paused. Runs entirely on the compositor.</summary>
public sealed partial class PlayingBars : UserControl
{
    private const float RestScale = 0.3f;

    // Different lengths per bar so the loop never looks in step.
    private static readonly TimeSpan[] Periods = [TimeSpan.FromMilliseconds(820), TimeSpan.FromMilliseconds(1040), TimeSpan.FromMilliseconds(700)];

    private readonly Rectangle[] _bars;
    private bool? _playing;

    public PlayingBars()
    {
        InitializeComponent();
        _bars = [Bar1, Bar2, Bar3];
        foreach (Rectangle bar in _bars)
        {
            Visual visual = ElementCompositionPreview.GetElementVisual(bar);
            visual.CenterPoint = new Vector3(1.5f, 14, 0);
            visual.Scale = new Vector3(1, RestScale, 1);
        }
    }

    public void SetColor(Color color)
    {
        var brush = new SolidColorBrush(color);
        foreach (Rectangle bar in _bars)
        {
            bar.Fill = brush;
        }
    }

    /// <summary>Bounces while <paramref name="playing"/>; without <paramref name="animate"/> the bars stand still at mid height.</summary>
    public void SetPlaying(bool playing, bool animate)
    {
        if (_playing == playing && animate)
        {
            return;
        }

        _playing = playing;
        for (int i = 0; i < _bars.Length; i++)
        {
            Visual visual = ElementCompositionPreview.GetElementVisual(_bars[i]);
            visual.StopAnimation("Scale");
            if (!playing || !animate)
            {
                visual.Scale = new Vector3(1, playing ? 0.7f - (0.2f * (i % 2)) : RestScale, 1);
                continue;
            }

            Compositor compositor = visual.Compositor;
            Vector3KeyFrameAnimation bounce = compositor.CreateVector3KeyFrameAnimation();
            CubicBezierEasingFunction ease = compositor.CreateCubicBezierEasingFunction(new Vector2(0.4f, 0), new Vector2(0.6f, 1));
            bounce.InsertKeyFrame(0, new Vector3(1, RestScale, 1));
            bounce.InsertKeyFrame(0.35f + (0.1f * i), new Vector3(1, 1, 1), ease);
            bounce.InsertKeyFrame(0.7f, new Vector3(1, 0.5f, 1), ease);
            bounce.InsertKeyFrame(1, new Vector3(1, RestScale, 1), ease);
            bounce.Duration = Periods[i];
            bounce.IterationBehavior = AnimationIterationBehavior.Forever;
            visual.StartAnimation("Scale", bounce);
        }
    }
}
