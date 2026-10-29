using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>The charging ring. The sweep is a trim animation on the compositor, so it costs nothing on the UI thread.</summary>
public sealed partial class ChargeRing : UserControl
{
    private const float Size = 22;
    private const float Stroke = 2;
    private static readonly TimeSpan SweepDuration = TimeSpan.FromMilliseconds(700);

    private readonly CompositionEllipseGeometry _arc;
    private readonly CompositionSpriteShape _arcShape;
    private readonly Compositor _compositor;

    public ChargeRing()
    {
        InitializeComponent();
        _compositor = ElementCompositionPreview.GetElementVisual(this).Compositor;
        float radius = (Size - Stroke) / 2;
        var center = new Vector2(Size / 2);

        CompositionEllipseGeometry track = _compositor.CreateEllipseGeometry();
        track.Center = center;
        track.Radius = new Vector2(radius);
        CompositionSpriteShape trackShape = _compositor.CreateSpriteShape(track);
        trackShape.StrokeBrush = _compositor.CreateColorBrush(Color.FromArgb(0x2E, 0xFF, 0xFF, 0xFF));
        trackShape.StrokeThickness = Stroke;

        _arc = _compositor.CreateEllipseGeometry();
        _arc.Center = center;
        _arc.Radius = new Vector2(radius);
        _arc.TrimEnd = 0;
        _arcShape = _compositor.CreateSpriteShape(_arc);
        _arcShape.StrokeThickness = Stroke;
        _arcShape.StrokeStartCap = CompositionStrokeCap.Round;
        _arcShape.StrokeEndCap = CompositionStrokeCap.Round;

        // Start the sweep at twelve o'clock rather than three.
        _arcShape.CenterPoint = center;
        _arcShape.RotationAngleInDegrees = -90;

        ShapeVisual shapes = _compositor.CreateShapeVisual();
        shapes.Size = new Vector2(Size);
        shapes.Shapes.Add(trackShape);
        shapes.Shapes.Add(_arcShape);
        ElementCompositionPreview.SetElementChildVisual(RingHost, shapes);
        SetColor(IslandPalette.Green);
    }

    public void SetColor(Color color)
    {
        _arcShape.StrokeBrush = _compositor.CreateColorBrush(color);
        Bolt.Foreground = new SolidColorBrush(color);
    }

    /// <summary>Sweeps from empty to <paramref name="percent"/>, or jumps there without <paramref name="animate"/>.</summary>
    public void Sweep(int percent, bool animate)
    {
        float end = Math.Clamp(percent, 0, 100) / 100f;
        _arc.StopAnimation("TrimEnd");
        if (!animate)
        {
            _arc.TrimEnd = end;
            return;
        }

        ScalarKeyFrameAnimation sweep = _compositor.CreateScalarKeyFrameAnimation();
        sweep.InsertKeyFrame(0, 0);
        sweep.InsertKeyFrame(1, end, _compositor.CreateCubicBezierEasingFunction(new Vector2(0.2f, 0.8f), new Vector2(0.2f, 1)));
        sweep.Duration = SweepDuration;
        sweep.DelayTime = IslandMotion.Stagger * 3;
        sweep.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
        _arc.StartAnimation("TrimEnd", sweep);
    }
}
