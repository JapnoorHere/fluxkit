using System.Numerics;
using FluxKit.App.Modules.FluxIsland.Design;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Windows.UI;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>
/// The island's body on the compositor, from the back: a soft glow in the current moment's colour, a layered shadow,
/// the black body, and an edge light along its top rim. Size changes are springs on the GPU; the corners, the
/// centring, the glow and the shadow all follow the size through expressions, and the same geometry clips the
/// content, so nothing spills outside while the shape morphs. Nothing here runs on the UI thread per frame.
/// </summary>
internal sealed class PillShape
{
    /// <summary>The largest corner radius, reached when the panel is open. Compact pills are fully round.</summary>
    public const float MaxCornerRadius = IslandTokens.PanelRadius;

    /// <summary>How far the glow and shadow reach past the body, so the window can leave room for them.</summary>
    public const float Bleed = 12;

    // Shadow layers from the outside in: how far each spreads past the pill, how far it drops, and its opacity.
    private static readonly (float Spread, float Drop, byte Alpha)[] Shadow = [(10, 5, 0x0A), (7, 4, 0x10), (4, 2, 0x18), (2, 1, 0x24)];

    // Glow layers from the outside in: spread and opacity. Together they read as one soft halo.
    private static readonly (float Spread, byte Alpha)[] Glow = [(12, 0x07), (9, 0x0C), (6, 0x12), (3.5f, 0x1A), (1.5f, 0x22)];

    private readonly Compositor _compositor;
    private readonly CompositionRoundedRectangleGeometry _geometry;
    private readonly CompositionLinearGradientBrush _mercury;
    private readonly CompositionRadialGradientBrush _specular;
    private readonly CompositionSpriteShape _fill;
    private readonly CompositionSpriteShape _shine;
    private readonly LiquidShape _liquid;
    private CompositionColorGradientStop? _rim;
    private readonly ShapeVisual _glow;
    private readonly List<(CompositionColorBrush Brush, byte Alpha)> _glowBrushes = [];
    private readonly Visual _stage;
    private readonly float _stageWidth;
    private readonly float _top;
    private Color _accent = IslandPalette.TextPrimary;
    private float _intensity;
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

        ContainerVisual root = _compositor.CreateContainerVisual();
        root.Size = stageSize;

        // The glow sits behind everything and is invisible until a moment asks for it.
        _glow = _compositor.CreateShapeVisual();
        _glow.Size = stageSize;
        _glow.Opacity = 0;
        foreach ((float spread, byte alpha) in Glow)
        {
            CompositionColorBrush brush = _compositor.CreateColorBrush(IslandPalette.WithAlpha(_accent, alpha));
            _glowBrushes.Add((brush, alpha));
            _glow.Shapes.Add(Halo(spread, 0, brush));
        }

        root.Children.InsertAtTop(_glow);

        ShapeVisual body = _compositor.CreateShapeVisual();
        body.Size = stageSize;

        // The shadow is a few slightly larger, nearly clear copies of the shape below it, not a live blur, so it
        // morphs with the pill at no extra cost.
        foreach ((float spread, float drop, byte alpha) in Shadow)
        {
            body.Shapes.Add(Halo(spread, drop, _compositor.CreateColorBrush(Color.FromArgb(alpha, 0, 0, 0))));
        }

        // Drops and necks of the same liquid sit just under the body (see LiquidShape, ADR-015).
        _mercury = MercuryBrush();
        _specular = SpecularBrush();
        _liquid = new LiquidShape(body, body.Shapes.Count, _mercury, _geometry, _stageWidth);

        _fill = _compositor.CreateSpriteShape(_geometry);
        _fill.FillBrush = _mercury;
        _fill.StrokeBrush = EdgeLight();
        _fill.StrokeThickness = 1;
        body.Shapes.Add(_fill);

        // Mercury's highlight: a soft spot of light on the metal that slides toward the pointer (see SetLight).
        _shine = _compositor.CreateSpriteShape(_geometry);
        body.Shapes.Add(_shine);
        root.Children.InsertAtTop(body);
        ElementCompositionPreview.SetElementChildVisual(shapeHost, root);

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
            _geometry.StartAnimation("Size", IslandSprings.Vector2(_compositor, IslandSprings.Morph, size));
        }
        else
        {
            Vector2KeyFrameAnimation ease = _compositor.CreateVector2KeyFrameAnimation();
            ease.InsertKeyFrame(1, size, IslandSprings.EaseOut(_compositor));
            ease.Duration = IslandSprings.ReducedDuration;
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

    /// <summary>
    /// Tints the glow with the current moment's colour and sets how strongly it shows (0 hides it). The colour
    /// cross-fades and the strength springs, so moments hand over smoothly.
    /// </summary>
    public void SetAccent(Color accent, float intensity, bool springs)
    {
        intensity = Math.Clamp(intensity, 0, 1);
        if (accent != _accent)
        {
            _accent = accent;
            foreach ((CompositionColorBrush brush, byte alpha) in _glowBrushes)
            {
                Color to = IslandPalette.WithAlpha(accent, alpha);
                if (springs)
                {
                    ColorKeyFrameAnimation fade = _compositor.CreateColorKeyFrameAnimation();
                    fade.InsertKeyFrame(1, to);
                    fade.Duration = TimeSpan.FromMilliseconds(320);
                    brush.StartAnimation("Color", fade);
                }
                else
                {
                    brush.Color = to;
                }
            }
        }

        if (Math.Abs(intensity - _intensity) < 0.01f)
        {
            return;
        }

        _intensity = intensity;
        if (springs)
        {
            _glow.StartAnimation("Opacity", IslandSprings.Scalar(_compositor, IslandSprings.Smooth, intensity));
        }
        else
        {
            _glow.StopAnimation("Opacity");
            _glow.Opacity = intensity;
        }
    }

    /// <summary>High contrast: a solid 2 px white outline instead of the soft edge light.</summary>
    public void SetHighContrast(bool on)
    {
        _fill.StrokeThickness = on ? 2 : 1;
        _fill.StrokeBrush = on ? _compositor.CreateColorBrush(Microsoft.UI.Colors.White) : EdgeLight();
    }

    /// <summary>
    /// Paints the body in <paramref name="theme"/>: a vertical gradient from its top colour to its bottom colour (the
    /// change cross-fades), its rim light, and, for Mercury, the moving highlight. Drops and necks share the brush.
    /// </summary>
    public void SetSurface(IslandTheme theme)
    {
        bool mercury = theme.Look == FluxKit.Tools.FluxIsland.IslandLook.Mercury;
        Color middle = mercury ? IslandPalette.MercuryMid : Mix(theme.Top, theme.Bottom);
        Color[] colors = [theme.Top, middle, theme.Bottom];
        for (int i = 0; i < colors.Length; i++)
        {
            ColorKeyFrameAnimation fade = _compositor.CreateColorKeyFrameAnimation();
            fade.InsertKeyFrame(1, colors[i]);
            fade.Duration = TimeSpan.FromMilliseconds(360);
            _mercury.ColorStops[i].StartAnimation("Color", fade);
        }

        _fill.FillBrush = _mercury;
        _liquid.SetSurface(_mercury);
        _shine.FillBrush = mercury ? _specular : null;
        if (_rim is not null)
        {
            _rim.Color = theme.Rim;
        }
    }

    private static Color Mix(Color a, Color b) => Color.FromArgb(
        (byte)((a.A + b.A) / 2),
        (byte)((a.R + b.R) / 2),
        (byte)((a.G + b.G) / 2),
        (byte)((a.B + b.B) / 2));

    /// <summary>
    /// Where the light falls on Mercury, from 0 (left edge) to 1 (right edge): the highlight drifts toward the pointer
    /// with a short ease. Costs one property write per pointer move, and nothing on the other looks.
    /// </summary>
    public void SetLight(float across)
    {
        if (_shine.FillBrush is null)
        {
            return;
        }

        Vector2KeyFrameAnimation drift = _compositor.CreateVector2KeyFrameAnimation();
        drift.InsertKeyFrame(1, new Vector2(Math.Clamp(across, 0.1f, 0.9f), 0.05f), IslandSprings.EaseOut(_compositor));
        drift.Duration = TimeSpan.FromMilliseconds(260);
        _specular.StartAnimation("EllipseCenter", drift);
    }

    /// <summary>A moment arriving: the island pulls a drop out of its underside and drinks it back (see LiquidShape.Gulp).</summary>
    public void Gulp(bool springs)
    {
        if (springs)
        {
            _liquid.Gulp(_stageWidth / 2, _top + Target.Y);
        }
    }

    /// <summary>The ferrofluid edge rising with the sound (see LiquidShape.Ripple); 0 settles it.</summary>
    public void Ripple(float level) => _liquid.Ripple(level);

    /// <summary>A ripple across the underside, as the pointer touches the island (see LiquidShape.Wave).</summary>
    public void Wave(bool springs)
    {
        if (springs)
        {
            _liquid.Wave();
        }
    }

    /// <summary>A moment waved away: a drop falls off the island's underside and fades (see LiquidShape.Shed).</summary>
    public void Shed(bool springs)
    {
        if (springs)
        {
            _liquid.Shed(_stageWidth / 2, _top + Target.Y);
        }
    }

    /// <summary>A moment arriving: the glow flares, then settles back to its resting strength.</summary>
    public void Flare(bool springs)
    {
        if (!springs || _intensity <= 0)
        {
            return;
        }

        ScalarKeyFrameAnimation flare = _compositor.CreateScalarKeyFrameAnimation();
        flare.InsertKeyFrame(0.25f, 1, IslandSprings.EaseOut(_compositor));
        flare.InsertKeyFrame(1, _intensity, IslandSprings.EaseOut(_compositor));
        flare.Duration = TimeSpan.FromMilliseconds(900);
        _glow.StartAnimation("Opacity", flare);
    }

    /// <summary>
    /// The second bubble splitting off the pill (or merging back): for a moment a thick black neck joins the pill's
    /// right edge to the bubble, then thins away, so the two read as one drop of liquid dividing.
    /// </summary>
    /// <param name="pillWidth">The pill's width as the bubble appears.</param>
    /// <param name="centreY">The vertical centre of the compact pill, in stage pixels.</param>
    /// <param name="gap">The space between the pill and the bubble.</param>
    /// <param name="splitting">True as the bubble buds off, false as it merges back.</param>
    public void Bud(float pillWidth, float centreY, float gap, bool splitting, bool springs)
    {
        if (springs)
        {
            _liquid.Bud((_stageWidth / 2) + (pillWidth / 2) - 10, centreY, gap + 22, splitting);
        }
    }

    /// <summary>One happy bounce of the whole island: up to 106 % and back, settling on a spring.</summary>
    public void Bounce(bool springs)
    {
        if (!springs)
        {
            return;
        }

        Vector3KeyFrameAnimation bounce = _compositor.CreateVector3KeyFrameAnimation();
        bounce.InsertKeyFrame(0.3f, new Vector3(1.06f, 1.06f, 1), IslandSprings.EaseOut(_compositor));
        bounce.InsertKeyFrame(0.65f, new Vector3(0.98f, 0.98f, 1));
        bounce.InsertKeyFrame(1, Vector3.One, IslandSprings.EaseOut(_compositor));
        bounce.Duration = TimeSpan.FromMilliseconds(520);
        bounce.DelayTime = TimeSpan.FromMilliseconds(120);
        _stage.StartAnimation("Scale", bounce);
    }

    /// <summary>Swells or squishes the whole island around its centre.</summary>
    public void ScaleTo(float scale, bool springs)
    {
        if (springs)
        {
            _stage.StartAnimation("Scale", IslandSprings.Vector3(_compositor, IslandSprings.Snappy, new Vector3(scale, scale, 1)));
        }
        else
        {
            _stage.StopAnimation("Scale");
            _stage.Scale = Vector3.One;
        }
    }

    /// <summary>A copy of the body, <paramref name="spread"/> larger all round and dropped by <paramref name="drop"/>.</summary>
    private CompositionSpriteShape Halo(float spread, float drop, CompositionBrush fill)
    {
        CompositionRoundedRectangleGeometry halo = _compositor.CreateRoundedRectangleGeometry();
        Follow(halo, "Size", "g.Size + Vector2(s * 2, s * 2)", spread);
        Follow(halo, "Offset", "g.Offset - Vector2(s, s) + Vector2(0, d)", spread, drop);
        Follow(halo, "CornerRadius", "g.CornerRadius + Vector2(s, s)", spread);
        CompositionSpriteShape shape = _compositor.CreateSpriteShape(halo);
        shape.FillBrush = fill;
        return shape;
    }

    /// <summary>Light catching the top rim: bright at the top edge, gone by halfway down.</summary>
    private CompositionLinearGradientBrush EdgeLight()
    {
        CompositionLinearGradientBrush light = _compositor.CreateLinearGradientBrush();
        light.StartPoint = Vector2.Zero;
        light.EndPoint = new Vector2(0, 1);
        _rim = _compositor.CreateColorGradientStop(0, IslandPalette.EdgeLight);
        light.ColorStops.Add(_rim);
        light.ColorStops.Add(_compositor.CreateColorGradientStop(0.45f, IslandPalette.WithAlpha(IslandPalette.EdgeLight, 0x0C)));
        light.ColorStops.Add(_compositor.CreateColorGradientStop(1, IslandPalette.WithAlpha(IslandPalette.EdgeLight, 0x06)));
        return light;
    }

    /// <summary>The body: a vertical gradient of three stops, recoloured by SetSurface for each theme.</summary>
    private CompositionLinearGradientBrush MercuryBrush()
    {
        CompositionLinearGradientBrush metal = _compositor.CreateLinearGradientBrush();
        metal.StartPoint = Vector2.Zero;
        metal.EndPoint = new Vector2(0, 1);
        metal.ColorStops.Add(_compositor.CreateColorGradientStop(0, IslandTheme.Onyx.Top));
        metal.ColorStops.Add(_compositor.CreateColorGradientStop(0.42f, Mix(IslandTheme.Onyx.Top, IslandTheme.Onyx.Bottom)));
        metal.ColorStops.Add(_compositor.CreateColorGradientStop(1, IslandTheme.Onyx.Bottom));
        return metal;
    }

    /// <summary>Mercury's specular spot: a soft white ellipse along the top that fades to nothing.</summary>
    private CompositionRadialGradientBrush SpecularBrush()
    {
        CompositionRadialGradientBrush spot = _compositor.CreateRadialGradientBrush();
        spot.EllipseCenter = new Vector2(0.5f, 0.05f);
        spot.EllipseRadius = new Vector2(0.38f, 0.55f);
        spot.ColorStops.Add(_compositor.CreateColorGradientStop(0, IslandPalette.WithAlpha(Microsoft.UI.Colors.White, 0x55)));
        spot.ColorStops.Add(_compositor.CreateColorGradientStop(0.6f, IslandPalette.WithAlpha(Microsoft.UI.Colors.White, 0x12)));
        spot.ColorStops.Add(_compositor.CreateColorGradientStop(1, IslandPalette.WithAlpha(Microsoft.UI.Colors.White, 0)));
        return spot;
    }

    private void Follow(CompositionGeometry target, string property, string expression, float spread, float drop = 0)
    {
        ExpressionAnimation animation = _compositor.CreateExpressionAnimation(expression);
        animation.SetReferenceParameter("g", _geometry);
        animation.SetScalarParameter("s", spread);
        animation.SetScalarParameter("d", drop);
        target.StartAnimation(property, animation);
    }
}
