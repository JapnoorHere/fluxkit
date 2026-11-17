using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace FluxKit.App.Modules.FluxIsland.Design;

/// <summary>
/// A glyph in a circle of its moment's colour: tinted (soft colour behind a coloured glyph) or filled (solid colour
/// behind a black glyph). It also owns the small "icon life" animations, all on the compositor: <see cref="Pop"/>
/// for arrivals, <see cref="Breathe"/> while something is live, <see cref="Flash"/>, <see cref="Strike"/> for a slash
/// that draws across (muted) and <see cref="DrawCheck"/> for a tick that draws itself (done).
/// </summary>
public sealed partial class IconBadge : UserControl
{
    private const string CheckGlyph = "\xE73E";

    private Color _accent = IslandPalette.TextPrimary;
    private bool _filled;
    private bool _breathing;

    public IconBadge()
    {
        InitializeComponent();
        Resize(24);
        Paint();
    }

    public string Glyph
    {
        get => Icon.Glyph;
        set => Icon.Glyph = value;
    }

    public Color Accent
    {
        get => _accent;
        set
        {
            _accent = value;
            Paint();
        }
    }

    /// <summary>Solid colour with a black glyph, for emphasis (a hovered drop zone, a primary state).</summary>
    public bool Filled
    {
        get => _filled;
        set
        {
            _filled = value;
            Paint();
        }
    }

    /// <summary>The badge's diameter: 24 on the compact pill, 28 in lists, 40 in panels, 52 for a hero.</summary>
    public double BadgeSize
    {
        get => Width;
        set => Resize(value);
    }

    /// <summary>Sets glyph and colour together, without restarting animations unless they change.</summary>
    public void Show(string glyph, Color accent, bool filled = false)
    {
        if (glyph == Icon.Glyph && accent == _accent && filled == _filled)
        {
            return;
        }

        Icon.Glyph = glyph;
        _accent = accent;
        _filled = filled;
        Slash.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
        Paint();
    }

    /// <summary>Arrival: grows from 60 % past full size and settles.</summary>
    public void Pop(bool springs)
    {
        if (!springs)
        {
            return;
        }

        Visual visual = Centred(Root);
        Vector3KeyFrameAnimation pop = visual.Compositor.CreateVector3KeyFrameAnimation();
        pop.InsertKeyFrame(0, new Vector3(0.6f, 0.6f, 1));
        pop.InsertKeyFrame(0.55f, new Vector3(1.12f, 1.12f, 1), IslandSprings.EaseOut(visual.Compositor));
        pop.InsertKeyFrame(1, Vector3.One, IslandSprings.EaseOut(visual.Compositor));
        pop.Duration = TimeSpan.FromMilliseconds(420);
        visual.StartAnimation("Scale", pop);
    }

    /// <summary>A slow, gentle swell while something is live (a call, the mic in use). Stops when turned off.</summary>
    public void Breathe(bool on, bool springs)
    {
        on &= springs;
        if (on == _breathing)
        {
            return;
        }

        _breathing = on;
        Visual visual = Centred(Disc);
        if (!on)
        {
            visual.StopAnimation("Scale");
            visual.Scale = Vector3.One;
            return;
        }

        Vector3KeyFrameAnimation swell = visual.Compositor.CreateVector3KeyFrameAnimation();
        CubicBezierEasingFunction ease = visual.Compositor.CreateCubicBezierEasingFunction(new Vector2(0.45f, 0), new Vector2(0.55f, 1));
        swell.InsertKeyFrame(0, Vector3.One);
        swell.InsertKeyFrame(0.5f, new Vector3(1.12f, 1.12f, 1), ease);
        swell.InsertKeyFrame(1, Vector3.One, ease);
        swell.Duration = TimeSpan.FromMilliseconds(1600);
        swell.IterationBehavior = AnimationIterationBehavior.Forever;
        visual.StartAnimation("Scale", swell);
    }

    /// <summary>The glyph dims and comes back once, like a spark (the charger's bolt).</summary>
    public void Flash(bool springs)
    {
        if (!springs)
        {
            return;
        }

        Visual visual = ElementCompositionPreview.GetElementVisual(Icon);
        ScalarKeyFrameAnimation flash = visual.Compositor.CreateScalarKeyFrameAnimation();
        flash.InsertKeyFrame(0.2f, 0.25f);
        flash.InsertKeyFrame(0.45f, 1);
        flash.InsertKeyFrame(0.65f, 0.4f);
        flash.InsertKeyFrame(1, 1);
        flash.Duration = TimeSpan.FromMilliseconds(600);
        flash.DelayTime = TimeSpan.FromMilliseconds(150);
        visual.StartAnimation("Opacity", flash);
    }

    /// <summary>A slash that draws across the glyph from one end (muted), or hides it.</summary>
    public void Strike(bool on, bool springs)
    {
        Slash.Visibility = on ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        if (!on)
        {
            return;
        }

        Visual visual = ElementCompositionPreview.GetElementVisual(Slash);
        visual.CenterPoint = new Vector3(0, 1, 0);
        if (!springs)
        {
            visual.Scale = Vector3.One;
            return;
        }

        Vector3KeyFrameAnimation draw = visual.Compositor.CreateVector3KeyFrameAnimation();
        draw.InsertKeyFrame(0, new Vector3(0, 1, 1));
        draw.InsertKeyFrame(1, Vector3.One, IslandSprings.EaseOut(visual.Compositor));
        draw.Duration = TimeSpan.FromMilliseconds(260);
        draw.DelayTime = TimeSpan.FromMilliseconds(120);
        draw.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
        visual.StartAnimation("Scale", draw);
    }

    /// <summary>Turns the glyph into a tick that draws itself left to right (done, saved, copied).</summary>
    public void DrawCheck(Color accent, bool springs)
    {
        Show(CheckGlyph, accent);
        Visual visual = ElementCompositionPreview.GetElementVisual(Icon);
        InsetClip clip = visual.Compositor.CreateInsetClip();
        visual.Clip = clip;
        if (!springs)
        {
            clip.RightInset = 0;
            return;
        }

        float width = (float)Math.Max(Icon.ActualWidth, Icon.FontSize);
        ScalarKeyFrameAnimation reveal = visual.Compositor.CreateScalarKeyFrameAnimation();
        reveal.InsertKeyFrame(0, width);
        reveal.InsertKeyFrame(1, 0, IslandSprings.EaseOut(visual.Compositor));
        reveal.Duration = TimeSpan.FromMilliseconds(360);
        reveal.DelayTime = TimeSpan.FromMilliseconds(80);
        reveal.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
        clip.StartAnimation("RightInset", reveal);
    }

    private static Visual Centred(Microsoft.UI.Xaml.UIElement element)
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(element);
        ExpressionAnimation centre = visual.Compositor.CreateExpressionAnimation("Vector3(this.Target.Size.X / 2, this.Target.Size.Y / 2, 0)");
        visual.StartAnimation("CenterPoint", centre);
        return visual;
    }

    private void Resize(double size)
    {
        Width = size;
        Height = size;
        Icon.FontSize = Math.Round(size * 0.48);
        Slash.Width = size * 0.62;
    }

    private void Paint()
    {
        Disc.Fill = new SolidColorBrush(_filled ? _accent : IslandPalette.Tint(_accent));
        Color glyph = _filled ? Color.FromArgb(0xFF, 0, 0, 0) : _accent;
        Icon.Foreground = new SolidColorBrush(glyph);
        Slash.Fill = new SolidColorBrush(glyph);
    }
}
