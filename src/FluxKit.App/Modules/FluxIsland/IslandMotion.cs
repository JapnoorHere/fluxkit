using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>
/// Content motion for the island, run on the compositor: new content fades in while sliding up 6 px, old content
/// fades out quickly. Without springs (reduced motion) content only fades.
/// </summary>
internal static class IslandMotion
{
    public static readonly TimeSpan Stagger = TimeSpan.FromMilliseconds(40);

    private const float Slide = 6;
    private static readonly TimeSpan EnterDuration = TimeSpan.FromMilliseconds(220);
    private static readonly TimeSpan ExitDuration = TimeSpan.FromMilliseconds(90);

    /// <summary>Shows <paramref name="element"/> and plays its entrance after <paramref name="delay"/>.</summary>
    public static void Enter(UIElement element, TimeSpan delay, bool springs)
    {
        element.Visibility = Visibility.Visible;
        Visual visual = ElementCompositionPreview.GetElementVisual(element);
        Compositor compositor = visual.Compositor;
        CubicBezierEasingFunction ease = compositor.CreateCubicBezierEasingFunction(new Vector2(0.1f, 0.9f), new Vector2(0.2f, 1f));

        ScalarKeyFrameAnimation fade = compositor.CreateScalarKeyFrameAnimation();
        fade.InsertKeyFrame(0, 0);
        fade.InsertKeyFrame(1, 1, ease);
        fade.Duration = EnterDuration;
        fade.DelayTime = delay;
        fade.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
        visual.StartAnimation("Opacity", fade);

        ElementCompositionPreview.SetIsTranslationEnabled(element, true);
        Vector3KeyFrameAnimation slide = compositor.CreateVector3KeyFrameAnimation();
        slide.InsertKeyFrame(0, new Vector3(0, springs ? Slide : 0, 0));
        slide.InsertKeyFrame(1, Vector3.Zero, ease);
        slide.Duration = EnterDuration;
        slide.DelayTime = delay;
        slide.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
        visual.StartAnimation("Translation", slide);
    }

    /// <summary>A small pop for an icon that just arrived: it grows from 60 % past full size and springs back.</summary>
    public static void Pop(UIElement element, bool springs)
    {
        if (!springs)
        {
            return;
        }

        Visual visual = ElementCompositionPreview.GetElementVisual(element);
        Compositor compositor = visual.Compositor;
        visual.CenterPoint = new Vector3(element.ActualSize / 2, 0);
        if (visual.CenterPoint == Vector3.Zero && element is FrameworkElement { Width: > 0, Height: > 0 } sized)
        {
            // Not measured yet, but it has a fixed size.
            visual.CenterPoint = new Vector3((float)sized.Width / 2, (float)sized.Height / 2, 0);
        }
        else if (visual.CenterPoint == Vector3.Zero)
        {
            // Not measured yet; the icons on the island are 16 to 22 px.
            visual.CenterPoint = new Vector3(8, 8, 0);
        }

        Vector3KeyFrameAnimation pop = compositor.CreateVector3KeyFrameAnimation();
        pop.InsertKeyFrame(0, new Vector3(0.6f, 0.6f, 1));
        pop.InsertKeyFrame(0.55f, new Vector3(1.18f, 1.18f, 1), compositor.CreateCubicBezierEasingFunction(new Vector2(0.2f, 0.9f), new Vector2(0.3f, 1)));
        pop.InsertKeyFrame(1, Vector3.One, compositor.CreateCubicBezierEasingFunction(new Vector2(0.4f, 0), new Vector2(0.2f, 1)));
        pop.Duration = TimeSpan.FromMilliseconds(420);
        pop.DelayTime = Stagger;
        pop.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
        visual.StartAnimation("Scale", pop);
    }

    /// <summary>A ring that grows out of an icon and fades away, for moments that deserve attention.</summary>
    public static void Burst(UIElement ring, bool springs)
    {
        if (!springs)
        {
            return;
        }

        Visual visual = ElementCompositionPreview.GetElementVisual(ring);
        Compositor compositor = visual.Compositor;
        visual.CenterPoint = new Vector3(11, 11, 0);
        CubicBezierEasingFunction ease = compositor.CreateCubicBezierEasingFunction(new Vector2(0.1f, 0.8f), new Vector2(0.3f, 1));

        Vector3KeyFrameAnimation grow = compositor.CreateVector3KeyFrameAnimation();
        grow.InsertKeyFrame(0, new Vector3(0.6f, 0.6f, 1));
        grow.InsertKeyFrame(1, new Vector3(2.4f, 2.4f, 1), ease);
        grow.Duration = TimeSpan.FromMilliseconds(700);
        grow.IterationCount = 2;
        grow.IterationBehavior = AnimationIterationBehavior.Count;

        ScalarKeyFrameAnimation fade = compositor.CreateScalarKeyFrameAnimation();
        fade.InsertKeyFrame(0, 0.9f);
        fade.InsertKeyFrame(1, 0, ease);
        fade.Duration = grow.Duration;
        fade.IterationCount = 2;
        fade.IterationBehavior = AnimationIterationBehavior.Count;

        visual.StartAnimation("Scale", grow);
        visual.StartAnimation("Opacity", fade);
    }

    /// <summary>Fades <paramref name="element"/> out, then collapses it unless <paramref name="stillHidden"/> says it came back.</summary>
    public static void Exit(UIElement element, Func<bool> stillHidden)
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(element);
        Compositor compositor = visual.Compositor;
        CompositionScopedBatch batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        ScalarKeyFrameAnimation fade = compositor.CreateScalarKeyFrameAnimation();
        fade.InsertKeyFrame(1, 0);
        fade.Duration = ExitDuration;
        visual.StartAnimation("Opacity", fade);
        batch.End();
        batch.Completed += (_, _) =>
        {
            if (stillHidden())
            {
                element.Visibility = Visibility.Collapsed;
            }
        };
    }
}
