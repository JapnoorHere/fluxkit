using System.Numerics;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>
/// The island's shape on the compositor: a near-black rounded rectangle with a faint inner edge and a soft shadow.
/// Size changes run as springs on the GPU. The corner radius and the centring follow the size through expressions,
/// and the same geometry clips the content, so text never spills outside while the shape morphs.
/// </summary>
internal sealed class PillShape
{
    public const float MaxCornerRadius = 26;

    private const float SpringDamping = 0.72f;
    private static readonly TimeSpan SpringPeriod = TimeSpan.FromMilliseconds(45);
    private static readonly TimeSpan FadeDuration = TimeSpan.FromMilliseconds(120);

    private readonly Compositor _compositor;
    private readonly CompositionRoundedRectangleGeometry _geometry;
    private readonly Visual _stage;
    private readonly float _stageWidth;
    private readonly float _top;
    private bool _placed;

    /// <param name="shapeHost">An empty element covering the stage; the shape is drawn as its child visual.</param>
    /// <param name="content">The stage-sized layer holding the XAML content; it is clipped to the shape.</param>
    /// <param name="stage">The element scaled on hover and press (holds both of the above).</param>
    public PillShape(UIElement shapeHost, UIElement content, UIElement stage, Vector2 stageSize, float top)
    {
        _stage = ElementCompositionPreview.GetElementVisual(stage);
        _compositor = _stage.Compositor;
        _stageWidth = stageSize.X;
        _top = top;
        _geometry = _compositor.CreateRoundedRectangleGeometry();

        CompositionSpriteShape fill = _compositor.CreateSpriteShape(_geometry);
        fill.FillBrush = _compositor.CreateColorBrush(IslandPalette.Surface);
        fill.StrokeBrush = _compositor.CreateColorBrush(IslandPalette.Highlight);
        fill.StrokeThickness = 1;

        ShapeVisual shapes = _compositor.CreateShapeVisual();
        shapes.Size = stageSize;
        shapes.Shapes.Add(fill);

        // The layer's shadow takes its outline from the shape, so it morphs with it at no extra cost.
        LayerVisual layer = _compositor.CreateLayerVisual();
        layer.Size = stageSize;
        layer.Children.InsertAtTop(shapes);
        DropShadow shadow = _compositor.CreateDropShadow();
        shadow.Color = Colors.Black;
        shadow.BlurRadius = 14;
        shadow.Opacity = 0.4f;
        shadow.Offset = new Vector3(0, 3, 0);
        layer.Shadow = shadow;
        ElementCompositionPreview.SetElementChildVisual(shapeHost, layer);

        ExpressionAnimation corner = _compositor.CreateExpressionAnimation("Vector2(Min(g.Size.Y / 2, r), Min(g.Size.Y / 2, r))");
        corner.SetReferenceParameter("g", _geometry);
        corner.SetScalarParameter("r", MaxCornerRadius);
        _geometry.StartAnimation("CornerRadius", corner);

        ExpressionAnimation offset = _compositor.CreateExpressionAnimation("Vector2((w - g.Size.X) / 2, top)");
        offset.SetReferenceParameter("g", _geometry);
        offset.SetScalarParameter("w", _stageWidth);
        offset.SetScalarParameter("top", _top);
        _geometry.StartAnimation("Offset", offset);

        ElementCompositionPreview.GetElementVisual(content).Clip = _compositor.CreateGeometricClip(_geometry);
    }

    /// <summary>The size the shape is moving to (or resting at).</summary>
    public Vector2 Target { get; private set; }

    /// <summary>Morphs to <paramref name="size"/>: a spring that overshoots a little, or a short ease with reduced motion.</summary>
    public void MorphTo(Vector2 size, bool springs)
    {
        if (size == Target && _placed)
        {
            return;
        }

        Target = size;
        _stage.CenterPoint = new Vector3(_stageWidth / 2, _top + (size.Y / 2), 0);
        if (!_placed)
        {
            _placed = true;
            _geometry.Size = size;
            return;
        }

        if (springs)
        {
            SpringVector2NaturalMotionAnimation spring = _compositor.CreateSpringVector2Animation();
            spring.DampingRatio = SpringDamping;
            spring.Period = SpringPeriod;
            spring.FinalValue = size;
            _geometry.StartAnimation("Size", spring);
        }
        else
        {
            Vector2KeyFrameAnimation ease = _compositor.CreateVector2KeyFrameAnimation();
            ease.InsertKeyFrame(1, size, EaseOut());
            ease.Duration = FadeDuration;
            _geometry.StartAnimation("Size", ease);
        }
    }

    /// <summary>Jumps to <paramref name="size"/> with no animation, e.g. when the island comes back after being hidden.</summary>
    public void Snap(Vector2 size)
    {
        _geometry.StopAnimation("Size");
        _geometry.Size = size;
        Target = size;
        _placed = true;
        _stage.CenterPoint = new Vector3(_stageWidth / 2, _top + (size.Y / 2), 0);
    }

    /// <summary>Swells or squishes the whole island around its centre.</summary>
    public void ScaleTo(float scale, bool springs)
    {
        var final = new Vector3(scale, scale, 1);
        if (springs)
        {
            SpringVector3NaturalMotionAnimation spring = _compositor.CreateSpringVector3Animation();
            spring.DampingRatio = 0.6f;
            spring.Period = SpringPeriod;
            spring.FinalValue = final;
            _stage.StartAnimation("Scale", spring);
        }
        else
        {
            _stage.StopAnimation("Scale");
            _stage.Scale = Vector3.One;
        }
    }

    private CubicBezierEasingFunction EaseOut() => _compositor.CreateCubicBezierEasingFunction(new Vector2(0.1f, 0.9f), new Vector2(0.2f, 1f));
}
