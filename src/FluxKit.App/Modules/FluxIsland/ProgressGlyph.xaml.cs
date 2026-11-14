using System.Numerics;
using FluxKit.App.Modules.FluxIsland.Design;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>
/// An icon in a progress ring, used wherever a compact moment has a level: timers, laps, charging, battery. The ring
/// is a trimmed ellipse on the compositor, so filling it costs nothing on the UI thread; changes spring into place.
/// </summary>
public sealed partial class ProgressGlyph : UserControl
{
    private const float Size = 24;
    private const float Stroke = 2.25f;
    private static readonly TimeSpan SweepDuration = TimeSpan.FromMilliseconds(700);

    private readonly CompositionEllipseGeometry _arc;
    private readonly CompositionSpriteShape _arcShape;
    private readonly CompositionColorBrush _arcBrush;
    private readonly Compositor _compositor;
    private float _value = -1;

    public ProgressGlyph()
    {
        InitializeComponent();
        _compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
        float radius = (Size - Stroke) / 2;
        var center = new Vector2(Size / 2);

        CompositionEllipseGeometry track = _compositor.CreateEllipseGeometry();
        track.Center = center;
        track.Radius = new Vector2(radius);
        CompositionSpriteShape trackShape = _compositor.CreateSpriteShape(track);
        trackShape.StrokeBrush = _compositor.CreateColorBrush(IslandPalette.Hairline);
        trackShape.StrokeThickness = Stroke;

        _arc = _compositor.CreateEllipseGeometry();
        _arc.Center = center;
        _arc.Radius = new Vector2(radius);
        _arc.TrimEnd = 0;
        _arcBrush = _compositor.CreateColorBrush(IslandPalette.Green);
        _arcShape = _compositor.CreateSpriteShape(_arc);
        _arcShape.StrokeBrush = _arcBrush;
        _arcShape.StrokeThickness = Stroke;
        _arcShape.StrokeStartCap = CompositionStrokeCap.Round;
        _arcShape.StrokeEndCap = CompositionStrokeCap.Round;

        // Fill from twelve o'clock rather than three.
        _arcShape.CenterPoint = center;
        _arcShape.RotationAngleInDegrees = -90;

        ShapeVisual shapes = _compositor.CreateShapeVisual();
        shapes.Size = new Vector2(Size);
        shapes.Shapes.Add(trackShape);
        shapes.Shapes.Add(_arcShape);
        ElementCompositionPreview.SetElementChildVisual(RingHost, shapes);
    }

    /// <summary>Colours the ring and the icon.</summary>
    public void SetColor(Color color)
    {
        _arcBrush.Color = color;
        Icon.Foreground = new SolidColorBrush(color);
    }

    public void SetGlyph(string glyph) => Icon.Glyph = glyph;

    /// <summary>
    /// Moves the ring to <paramref name="progress"/> (0–1) with a soft spring, or straight there without
    /// <paramref name="animate"/>. Going backwards a long way (a new lap, a new timer) jumps instead of unwinding.
    /// </summary>
    public void SetProgress(double progress, bool animate)
    {
        float end = (float)Math.Clamp(progress, 0, 1);
        if (Math.Abs(end - _value) < 0.001f)
        {
            return;
        }

        bool jump = !animate || _value < 0 || end < _value - 0.25f;
        _value = end;
        if (jump)
        {
            _arc.StopAnimation("TrimEnd");
            _arc.TrimEnd = end;
            return;
        }

        _arc.StartAnimation("TrimEnd", IslandSprings.Scalar(_compositor, IslandSprings.Smooth, end));
    }

    /// <summary>Sweeps from empty to <paramref name="percent"/>: the charger's arrival flourish.</summary>
    public void Sweep(int percent, bool animate)
    {
        float end = Math.Clamp(percent, 0, 100) / 100f;
        _value = end;
        _arc.StopAnimation("TrimEnd");
        if (!animate)
        {
            _arc.TrimEnd = end;
            return;
        }

        ScalarKeyFrameAnimation sweep = _compositor.CreateScalarKeyFrameAnimation();
        sweep.InsertKeyFrame(0, 0);
        sweep.InsertKeyFrame(1, end, IslandSprings.EaseOut(_compositor));
        sweep.Duration = SweepDuration;
        sweep.DelayTime = IslandMotion.Stagger * 3;
        sweep.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
        _arc.StartAnimation("TrimEnd", sweep);
    }
}
