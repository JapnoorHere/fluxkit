using System.Numerics;
using Microsoft.UI.Composition;

namespace FluxKit.App.Modules.FluxIsland.Design;

/// <summary>
/// The liquid in "Liquid" (ADR-015): drops and necks drawn in the island's own surface, so they read as part of one
/// body of liquid. A neck is a rounded bar that stretches and thins until it snaps; a drop is a circle. All motion is
/// keyframes on the compositor: nothing runs per frame on the UI thread, and nothing runs at all between moments.
/// </summary>
internal sealed class LiquidShape
{
    private const float DropRadius = 7;

    private readonly Compositor _compositor;
    private readonly CompositionRoundedRectangleGeometry _sideNeck;
    private readonly CompositionRoundedRectangleGeometry _neck;
    private readonly CompositionEllipseGeometry _drop;
    private readonly List<CompositionSpriteShape> _shapes = [];

    /// <param name="layer">The shape visual the island's body is drawn in; the liquid goes just under the body.</param>
    /// <param name="index">Where in <paramref name="layer"/> to insert the liquid (under the body, over its shadow).</param>
    /// <param name="surface">The body's brush, shared so the liquid always matches the island's look.</param>
    public LiquidShape(ShapeVisual layer, int index, CompositionBrush surface)
    {
        _compositor = layer.Compositor;
        _sideNeck = RoundBar();
        _neck = RoundBar();
        _drop = _compositor.CreateEllipseGeometry();
        _drop.Radius = Vector2.Zero;
        foreach (CompositionGeometry geometry in new CompositionGeometry[] { _sideNeck, _neck, _drop })
        {
            CompositionSpriteShape shape = _compositor.CreateSpriteShape(geometry);
            shape.FillBrush = surface;
            _shapes.Add(shape);
            layer.Shapes.Insert(index++, shape);
        }
    }

    /// <summary>How far below the pill a drop can reach, so the window region can leave room while one hangs.</summary>
    public static float Reach => (DropRadius * 2) + 12;

    /// <summary>Switches every drop and neck to the island's new surface.</summary>
    public void SetSurface(CompositionBrush surface)
    {
        foreach (CompositionSpriteShape shape in _shapes)
        {
            shape.FillBrush = surface;
        }
    }

    /// <summary>
    /// The second bubble splitting off the pill sideways (or merging back): a thick neck joins the pill's right edge
    /// to the bubble, then thins away, so the two read as one drop of liquid dividing.
    /// </summary>
    public void Bud(float left, float centreY, float width, bool splitting)
    {
        ExpressionAnimation offset = _compositor.CreateExpressionAnimation("Vector2(x, y - n.Size.Y / 2)");
        offset.SetReferenceParameter("n", _sideNeck);
        offset.SetScalarParameter("x", left);
        offset.SetScalarParameter("y", centreY);
        _sideNeck.StartAnimation("Offset", offset);

        Vector2KeyFrameAnimation thin = _compositor.CreateVector2KeyFrameAnimation();
        if (splitting)
        {
            thin.InsertKeyFrame(0, new Vector2(width, 22));
            thin.InsertKeyFrame(0.35f, new Vector2(width, 14), IslandSprings.EaseOut(_compositor));
        }
        else
        {
            thin.InsertKeyFrame(0, new Vector2(width, 0));
            thin.InsertKeyFrame(0.45f, new Vector2(width, 16), IslandSprings.EaseOut(_compositor));
        }

        thin.InsertKeyFrame(1, new Vector2(width, 0), IslandSprings.EaseIn(_compositor));
        thin.Duration = TimeSpan.FromMilliseconds(splitting ? 320 : 300);
        _sideNeck.StartAnimation("Size", thin);
    }

    /// <summary>
    /// A moment arriving: surface tension pulls a drop out of the pill's underside, it hangs on a neck for a beat, then
    /// the island drinks it back in. The signature arrival no other island has.
    /// </summary>
    /// <param name="centreX">The pill's horizontal centre.</param>
    /// <param name="bottom">The pill's bottom edge.</param>
    public void Gulp(float centreX, float bottom)
    {
        TimeSpan duration = TimeSpan.FromMilliseconds(680);
        Vector2KeyFrameAnimation centre = _compositor.CreateVector2KeyFrameAnimation();
        centre.InsertKeyFrame(0, new Vector2(centreX, bottom - DropRadius));
        centre.InsertKeyFrame(0.4f, new Vector2(centreX, bottom + DropRadius + 6), IslandSprings.EaseOut(_compositor));
        centre.InsertKeyFrame(0.55f, new Vector2(centreX, bottom + DropRadius + 3));
        centre.InsertKeyFrame(1, new Vector2(centreX, bottom - DropRadius), IslandSprings.EaseIn(_compositor));
        centre.Duration = duration;
        _drop.StartAnimation("Center", centre);

        Vector2KeyFrameAnimation radius = _compositor.CreateVector2KeyFrameAnimation();
        radius.InsertKeyFrame(0, new Vector2(DropRadius * 0.6f));
        radius.InsertKeyFrame(0.4f, new Vector2(DropRadius), IslandSprings.EaseOut(_compositor));
        radius.InsertKeyFrame(0.9f, new Vector2(DropRadius * 0.8f));
        radius.InsertKeyFrame(1, Vector2.Zero);
        radius.Duration = duration;
        _drop.StartAnimation("Radius", radius);

        // The neck runs from inside the pill down to the drop's centre, thinnest in the middle of the stretch.
        Stretch(centreX, bottom, duration, snaps: false);
    }

    /// <summary>A moment waved away: a drop slips out of the underside, the neck snaps, and it falls and fades.</summary>
    public void Shed(float centreX, float bottom)
    {
        TimeSpan duration = TimeSpan.FromMilliseconds(560);
        Vector2KeyFrameAnimation centre = _compositor.CreateVector2KeyFrameAnimation();
        centre.InsertKeyFrame(0, new Vector2(centreX, bottom - DropRadius));
        centre.InsertKeyFrame(0.35f, new Vector2(centreX, bottom + DropRadius + 2), IslandSprings.EaseOut(_compositor));
        centre.InsertKeyFrame(1, new Vector2(centreX, bottom + Reach), IslandSprings.EaseIn(_compositor));
        centre.Duration = duration;
        _drop.StartAnimation("Center", centre);

        Vector2KeyFrameAnimation radius = _compositor.CreateVector2KeyFrameAnimation();
        radius.InsertKeyFrame(0, new Vector2(DropRadius * 0.6f));
        radius.InsertKeyFrame(0.35f, new Vector2(DropRadius));
        radius.InsertKeyFrame(1, Vector2.Zero, IslandSprings.EaseIn(_compositor));
        radius.Duration = duration;
        _drop.StartAnimation("Radius", radius);

        Stretch(centreX, bottom, duration, snaps: true);
    }

    /// <summary>The vertical neck between the pill and the drop: it follows the drop down and thins (and snaps, when shed).</summary>
    private void Stretch(float centreX, float bottom, TimeSpan duration, bool snaps)
    {
        ExpressionAnimation offset = _compositor.CreateExpressionAnimation("Vector2(x - n.Size.X / 2, top)");
        offset.SetReferenceParameter("n", _neck);
        offset.SetScalarParameter("x", centreX);
        offset.SetScalarParameter("top", bottom - 6);
        _neck.StartAnimation("Offset", offset);

        Vector2KeyFrameAnimation size = _compositor.CreateVector2KeyFrameAnimation();
        size.InsertKeyFrame(0, new Vector2(16, 6));
        if (snaps)
        {
            size.InsertKeyFrame(0.3f, new Vector2(7, DropRadius + 8), IslandSprings.EaseOut(_compositor));
            size.InsertKeyFrame(0.38f, new Vector2(0, DropRadius + 8));
            size.InsertKeyFrame(1, Vector2.Zero);
        }
        else
        {
            size.InsertKeyFrame(0.4f, new Vector2(8, DropRadius + 12), IslandSprings.EaseOut(_compositor));
            size.InsertKeyFrame(0.55f, new Vector2(10, DropRadius + 9));
            size.InsertKeyFrame(1, new Vector2(16, 0), IslandSprings.EaseIn(_compositor));
        }

        size.Duration = duration;
        _neck.StartAnimation("Size", size);
    }

    /// <summary>A bar whose ends are always fully round, whatever its size.</summary>
    private CompositionRoundedRectangleGeometry RoundBar()
    {
        CompositionRoundedRectangleGeometry bar = _compositor.CreateRoundedRectangleGeometry();
        ExpressionAnimation round = _compositor.CreateExpressionAnimation("Vector2(Min(n.Size.X, n.Size.Y) / 2, Min(n.Size.X, n.Size.Y) / 2)");
        round.SetReferenceParameter("n", bar);
        bar.StartAnimation("CornerRadius", round);
        return bar;
    }
}
