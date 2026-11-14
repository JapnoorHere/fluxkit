using System.Numerics;
using FluxKit.App.Modules.FluxIsland.Design;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>
/// Content motion for the island, all on the compositor. New content grows in from 104 % while it fades in and
/// settles; old content shrinks to 96 % and fades out quickly, so a change reads as one thing turning into another.
/// Each kind of moment also has a small signature flourish (a pop, a shake, a flip, a tilt). With reduced motion,
/// content only fades and flourishes are skipped.
/// </summary>
internal static class IslandMotion
{
    public static readonly TimeSpan Stagger = TimeSpan.FromMilliseconds(40);

    private const float EnterScale = 1.04f;
    private const float ExitScale = 0.96f;
    private const float Slide = 4;
    private static readonly TimeSpan EnterDuration = TimeSpan.FromMilliseconds(260);
    private static readonly TimeSpan ExitDuration = TimeSpan.FromMilliseconds(110);

    /// <summary>Shows <paramref name="element"/> and plays its entrance after <paramref name="delay"/>.</summary>
    public static void Enter(UIElement element, TimeSpan delay, bool springs)
    {
        element.Visibility = Visibility.Visible;
        Visual visual = Prepare(element);
        Compositor compositor = visual.Compositor;
        CubicBezierEasingFunction ease = IslandSprings.EaseOut(compositor);

        visual.StartAnimation("Opacity", KeyFrames(compositor, 0, 1, ease, EnterDuration, delay));
        if (!springs)
        {
            visual.StopAnimation("Scale");
            visual.StopAnimation("Translation");
            visual.Scale = Vector3.One;
            visual.Properties.InsertVector3("Translation", Vector3.Zero);
            return;
        }

        Vector3KeyFrameAnimation grow = compositor.CreateVector3KeyFrameAnimation();
        grow.InsertKeyFrame(0, new Vector3(EnterScale, EnterScale, 1));
        grow.InsertKeyFrame(1, Vector3.One, ease);
        grow.Duration = EnterDuration;
        grow.DelayTime = delay;
        grow.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
        visual.StartAnimation("Scale", grow);

        Vector3KeyFrameAnimation slide = compositor.CreateVector3KeyFrameAnimation();
        slide.InsertKeyFrame(0, new Vector3(0, Slide, 0));
        slide.InsertKeyFrame(1, Vector3.Zero, ease);
        slide.Duration = EnterDuration;
        slide.DelayTime = delay;
        slide.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
        visual.StartAnimation("Translation", slide);
    }

    /// <summary>Shrinks and fades <paramref name="element"/> out, then collapses it unless <paramref name="stillHidden"/> says it came back.</summary>
    /// <param name="shrink">False to only fade, when the caller animates the scale itself.</param>
    public static void Exit(UIElement element, Func<bool> stillHidden, bool shrink = true)
    {
        Visual visual = Prepare(element);
        Compositor compositor = visual.Compositor;
        CompositionScopedBatch batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        CubicBezierEasingFunction ease = IslandSprings.EaseIn(compositor);
        visual.StartAnimation("Opacity", KeyFrames(compositor, null, 0, ease, ExitDuration, TimeSpan.Zero));
        if (shrink)
        {
            Vector3KeyFrameAnimation smaller = compositor.CreateVector3KeyFrameAnimation();
            smaller.InsertKeyFrame(1, new Vector3(ExitScale, ExitScale, 1), ease);
            smaller.Duration = ExitDuration;
            visual.StartAnimation("Scale", smaller);
        }
        batch.End();
        batch.Completed += (_, _) =>
        {
            if (stillHidden())
            {
                element.Visibility = Visibility.Collapsed;
            }
        };
    }

    /// <summary>A small pop for an icon that just arrived: it grows from 60 % past full size and springs back.</summary>
    public static void Pop(UIElement element, bool springs)
    {
        if (!springs)
        {
            return;
        }

        Visual visual = Prepare(element);
        Compositor compositor = visual.Compositor;
        Vector3KeyFrameAnimation pop = compositor.CreateVector3KeyFrameAnimation();
        pop.InsertKeyFrame(0, new Vector3(0.6f, 0.6f, 1));
        pop.InsertKeyFrame(0.55f, new Vector3(1.18f, 1.18f, 1), IslandSprings.EaseOut(compositor));
        pop.InsertKeyFrame(1, Vector3.One, IslandSprings.EaseOut(compositor));
        pop.Duration = TimeSpan.FromMilliseconds(420);
        pop.DelayTime = Stagger;
        pop.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
        visual.StartAnimation("Scale", pop);
    }

    /// <summary>A bell ringing: the icon swings left and right a few times, dying away.</summary>
    public static void Shake(UIElement element, bool springs)
    {
        if (!springs)
        {
            return;
        }

        Visual visual = Prepare(element);
        ScalarKeyFrameAnimation swing = visual.Compositor.CreateScalarKeyFrameAnimation();
        float[] angles = [0, 18, -16, 12, -8, 4, 0];
        for (int i = 0; i < angles.Length; i++)
        {
            swing.InsertKeyFrame(i / (float)(angles.Length - 1), angles[i]);
        }

        swing.Duration = TimeSpan.FromMilliseconds(640);
        swing.DelayTime = TimeSpan.FromMilliseconds(120);
        visual.StartAnimation("RotationAngleInDegrees", swing);
    }

    /// <summary>A card turning over: the icon squeezes to nothing and opens up again, as if flipped.</summary>
    public static void Flip(UIElement element, bool springs)
    {
        if (!springs)
        {
            return;
        }

        Visual visual = Prepare(element);
        Compositor compositor = visual.Compositor;
        Vector3KeyFrameAnimation flip = compositor.CreateVector3KeyFrameAnimation();
        flip.InsertKeyFrame(0, new Vector3(0, 1, 1));
        flip.InsertKeyFrame(0.6f, new Vector3(1.12f, 1, 1), IslandSprings.EaseOut(compositor));
        flip.InsertKeyFrame(1, Vector3.One, IslandSprings.EaseOut(compositor));
        flip.Duration = TimeSpan.FromMilliseconds(380);
        flip.DelayTime = Stagger;
        flip.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
        visual.StartAnimation("Scale", flip);
    }

    /// <summary>A photo being dropped on a table: it falls in from above with a slight tilt that straightens.</summary>
    public static void Drop(UIElement element, bool springs)
    {
        if (!springs)
        {
            return;
        }

        Visual visual = Prepare(element);
        Compositor compositor = visual.Compositor;
        ScalarKeyFrameAnimation tilt = compositor.CreateScalarKeyFrameAnimation();
        tilt.InsertKeyFrame(0, -10);
        tilt.InsertKeyFrame(0.6f, 3, IslandSprings.EaseOut(compositor));
        tilt.InsertKeyFrame(1, 0, IslandSprings.EaseOut(compositor));
        tilt.Duration = TimeSpan.FromMilliseconds(520);
        visual.StartAnimation("RotationAngleInDegrees", tilt);

        Vector3KeyFrameAnimation fall = compositor.CreateVector3KeyFrameAnimation();
        fall.InsertKeyFrame(0, new Vector3(0, -14, 0));
        fall.InsertKeyFrame(1, Vector3.Zero, IslandSprings.EaseOut(compositor));
        fall.Duration = TimeSpan.FromMilliseconds(420);
        visual.StartAnimation("Translation", fall);
    }

    /// <summary>Something arriving from the side (a device connecting): the icon slides in from the left and settles.</summary>
    public static void SlideIn(UIElement element, bool springs)
    {
        if (!springs)
        {
            return;
        }

        Visual visual = Prepare(element);
        visual.Properties.InsertVector3("Translation", new Vector3(-12, 0, 0));
        visual.StartAnimation("Translation", IslandSprings.Vector3(visual.Compositor, IslandSprings.Bouncy, Vector3.Zero));
    }

    /// <summary>A ring that grows out of an icon and fades away, for moments that deserve attention.</summary>
    public static void Burst(UIElement ring, bool springs)
    {
        if (!springs)
        {
            return;
        }

        Visual visual = Prepare(ring);
        Compositor compositor = visual.Compositor;
        CubicBezierEasingFunction ease = IslandSprings.EaseOut(compositor);

        Vector3KeyFrameAnimation grow = compositor.CreateVector3KeyFrameAnimation();
        grow.InsertKeyFrame(0, new Vector3(0.6f, 0.6f, 1));
        grow.InsertKeyFrame(1, new Vector3(2.4f, 2.4f, 1), ease);
        grow.Duration = TimeSpan.FromMilliseconds(700);
        grow.IterationCount = 2;
        grow.IterationBehavior = AnimationIterationBehavior.Count;

        ScalarKeyFrameAnimation fade = KeyFrames(compositor, 0.9f, 0, ease, grow.Duration, TimeSpan.Zero);
        fade.IterationCount = 2;
        fade.IterationBehavior = AnimationIterationBehavior.Count;

        visual.StartAnimation("Scale", grow);
        visual.StartAnimation("Opacity", fade);
    }

    /// <summary>
    /// The element's visual, with translation on and its centre point tied to its size, so scales and rotations
    /// always pivot around the middle, even before it has been measured.
    /// </summary>
    private static Visual Prepare(UIElement element)
    {
        ElementCompositionPreview.SetIsTranslationEnabled(element, true);
        Visual visual = ElementCompositionPreview.GetElementVisual(element);
        ExpressionAnimation centre = visual.Compositor.CreateExpressionAnimation("Vector3(this.Target.Size.X / 2, this.Target.Size.Y / 2, 0)");
        visual.StartAnimation("CenterPoint", centre);
        return visual;
    }

    private static ScalarKeyFrameAnimation KeyFrames(Compositor compositor, float? from, float to, CompositionEasingFunction ease, TimeSpan duration, TimeSpan delay)
    {
        ScalarKeyFrameAnimation animation = compositor.CreateScalarKeyFrameAnimation();
        if (from is { } start)
        {
            animation.InsertKeyFrame(0, start);
        }

        animation.InsertKeyFrame(1, to, ease);
        animation.Duration = duration;
        animation.DelayTime = delay;
        animation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
        return animation;
    }
}
