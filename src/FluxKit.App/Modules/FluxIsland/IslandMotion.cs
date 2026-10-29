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
