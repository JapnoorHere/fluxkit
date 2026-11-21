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
    private const int BumpCount = 7;
    private const float BumpWidth = 5;
    private const float BumpHeight = 11;

    private readonly List<CompositionSpriteShape> _shapes = [];
    private readonly List<CompositionEllipseGeometry> _bumps = [];
    private int _tick;

    /// <param name="layer">The shape visual the island's body is drawn in; the liquid goes just under the body.</param>
    /// <param name="index">Where in <paramref name="layer"/> to insert the liquid (under the body, over its shadow).</param>
    /// <param name="surface">The body's brush, shared so the liquid always matches the island's look.</param>
    /// <param name="body">The pill's geometry: the ferrofluid edge rides along its underside.</param>
    /// <param name="stageWidth">The width the pill is centred in.</param>
    public LiquidShape(ShapeVisual layer, int index, CompositionBrush surface, CompositionRoundedRectangleGeometry body, float stageWidth)
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

        // The ferrofluid edge: bumps centred on the pill's bottom edge, spread over its middle. Half of each bump is
        // inside the body and invisible, so at rest there is nothing to see; as a bump grows it rises out of the edge.
        for (int i = 0; i < BumpCount; i++)
        {
            CompositionEllipseGeometry bump = _compositor.CreateEllipseGeometry();
            bump.Radius = new Vector2(BumpWidth, 0);
            ExpressionAnimation place = _compositor.CreateExpressionAnimation("Vector2((w - g.Size.X) / 2 + g.Size.X * f, g.Offset.Y + g.Size.Y)");
            place.SetReferenceParameter("g", body);
            place.SetScalarParameter("w", stageWidth);
            place.SetScalarParameter("f", 0.2f + (0.6f * i / (BumpCount - 1)));
            bump.StartAnimation("Center", place);
            _bumps.Add(bump);
            CompositionSpriteShape shape = _compositor.CreateSpriteShape(bump);
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

    /// <summary>How far the ferrofluid edge can rise below the pill, so the window region can leave room.</summary>
    public static float RippleReach => BumpHeight + 2;

    /// <summary>
    /// The ferrofluid edge following sound (0 silent, 1 loud): the middle bumps rise highest, and each one wobbles a
    /// little on its own so the edge looks alive rather than like a bar chart. Called about 30 times a second while
    /// music plays; 0 settles every bump back into the edge.
    /// </summary>
    public void Ripple(float level)
    {
        level = Math.Clamp(level, 0, 1);
        _tick++;
        for (int i = 0; i < _bumps.Count; i++)
        {
            // A bell across the edge, times a slow, per-bump wobble.
            float across = (i / (float)(_bumps.Count - 1)) - 0.5f;
            float bell = MathF.Exp(-across * across * 4);
            float wobble = 0.6f + (0.4f * MathF.Sin((_tick * 0.9f) + (i * 1.7f)));
            Vector2KeyFrameAnimation rise = _compositor.CreateVector2KeyFrameAnimation();
            rise.InsertKeyFrame(1, new Vector2(BumpWidth, BumpHeight * level * bell * wobble));
            rise.Duration = TimeSpan.FromMilliseconds(level > 0 ? 70 : 240);
            _bumps[i].StartAnimation("Radius", rise);
        }
    }

    /// <summary>A ripple running across the edge from left to right, as when a finger touches liquid (hover).</summary>
    public void Wave()
    {
        for (int i = 0; i < _bumps.Count; i++)
        {
            Vector2KeyFrameAnimation wave = _compositor.CreateVector2KeyFrameAnimation();
            wave.InsertKeyFrame(0.4f, new Vector2(BumpWidth, 3.5f), IslandSprings.EaseOut(_compositor));
            wave.InsertKeyFrame(1, new Vector2(BumpWidth, 0), IslandSprings.EaseIn(_compositor));
            wave.Duration = TimeSpan.FromMilliseconds(340);
            wave.DelayTime = TimeSpan.FromMilliseconds(i * 24);
            _bumps[i].StartAnimation("Radius", wave);
        }
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
