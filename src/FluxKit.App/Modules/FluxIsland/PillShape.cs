using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Windows.UI;

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

    // Shadow layers from the outside in: how far each spreads past the pill, how far it drops, and its opacity.
    private static readonly (float Spread, float Drop, byte Alpha)[] Shadow = [(9, 4, 0x08), (6, 3, 0x0C), (3, 2, 0x14), (1, 1, 0x1E)];

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

        ShapeVisual shapes = _compositor.CreateShapeVisual();
        shapes.Size = stageSize;

        // The soft shadow is a few slightly larger, nearly clear copies of the shape below it, not a live blur.
        // They follow the shape through expressions, so the shadow morphs with the pill at no extra cost.
        foreach ((float spread, float drop, byte alpha) in Shadow)
        {
            CompositionRoundedRectangleGeometry halo = _compositor.CreateRoundedRectangleGeometry();
            Follow(halo, "Size", "g.Size + Vector2(s * 2, s * 2)", spread);
            Follow(halo, "Offset", "g.Offset - Vector2(s, s) + Vector2(0, d)", spread, drop);
            Follow(halo, "CornerRadius", "g.CornerRadius + Vector2(s, s)", spread);
            CompositionSpriteShape layer = _compositor.CreateSpriteShape(halo);
            layer.FillBrush = _compositor.CreateColorBrush(Color.FromArgb(alpha, 0, 0, 0));
            shapes.Shapes.Add(layer);
        }

        CompositionSpriteShape fill = _compositor.CreateSpriteShape(_geometry);
        fill.FillBrush = _compositor.CreateColorBrush(IslandPalette.Surface);
        fill.StrokeBrush = _compositor.CreateColorBrush(IslandPalette.Highlight);
        fill.StrokeThickness = 1;
        shapes.Shapes.Add(fill);
        ElementCompositionPreview.SetElementChildVisual(shapeHost, shapes);

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

    private void Follow(CompositionGeometry target, string property, string expression, float spread, float drop = 0)
    {
        ExpressionAnimation animation = _compositor.CreateExpressionAnimation(expression);
        animation.SetReferenceParameter("g", _geometry);
        animation.SetScalarParameter("s", spread);
        animation.SetScalarParameter("d", drop);
        target.StartAnimation(property, animation);
    }

    private CubicBezierEasingFunction EaseOut() => _compositor.CreateCubicBezierEasingFunction(new Vector2(0.1f, 0.9f), new Vector2(0.2f, 1f));
}
