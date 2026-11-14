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
    private const float DefaultSize = 24;
    private static readonly TimeSpan SweepDuration = TimeSpan.FromMilliseconds(700);

    private readonly CompositionEllipseGeometry _arc;
    private readonly CompositionSpriteShape _arcShape;
    private readonly CompositionColorBrush _arcBrush;
    private readonly CompositionEllipseGeometry _track;
    private readonly CompositionSpriteShape _trackShape;
    private readonly ShapeVisual _shapes;
    private readonly Compositor _compositor;
    private float _value = -1;

    public ProgressGlyph()
    {
        InitializeComponent();
        _compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
        _track = _compositor.CreateEllipseGeometry();
        _trackShape = _compositor.CreateSpriteShape(_track);
        _trackShape.StrokeBrush = _compositor.CreateColorBrush(IslandPalette.Hairline);

        _arc = _compositor.CreateEllipseGeometry();
        _arc.TrimEnd = 0;
        _arcBrush = _compositor.CreateColorBrush(IslandPalette.Green);
        _arcShape = _compositor.CreateSpriteShape(_arc);
        _arcShape.StrokeBrush = _arcBrush;
        _arcShape.StrokeStartCap = CompositionStrokeCap.Round;
        _arcShape.StrokeEndCap = CompositionStrokeCap.Round;

        // Fill from twelve o'clock rather than three.
        _arcShape.RotationAngleInDegrees = -90;

        _shapes = _compositor.CreateShapeVisual();
        _shapes.Shapes.Add(_trackShape);
        _shapes.Shapes.Add(_arcShape);
        ElementCompositionPreview.SetElementChildVisual(RingHost, _shapes);
        Layout(DefaultSize);
        SizeChanged += (_, e) => Layout((float)Math.Min(e.NewSize.Width, e.NewSize.Height));
    }

    /// <summary>Fits the ring to the control: a thin ring round a small icon, a bolder one when used as a big dial.</summary>
    private void Layout(float size)
    {
        if (size <= 0)
        {
            return;
        }

        float stroke = size <= 28 ? 2.25f : size * 0.07f;
        float radius = (size - stroke) / 2;
        var center = new Vector2(size / 2);
        foreach (CompositionEllipseGeometry ellipse in new[] { _track, _arc })
        {
            ellipse.Center = center;
            ellipse.Radius = new Vector2(radius);
        }

        _trackShape.StrokeThickness = stroke;
        _arcShape.StrokeThickness = stroke;
        _arcShape.CenterPoint = center;
        _shapes.Size = new Vector2(size);
        Icon.FontSize = size <= 28 ? 11 : size * 0.34;
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
