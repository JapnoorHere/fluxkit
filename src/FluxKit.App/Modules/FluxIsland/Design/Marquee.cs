using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;

namespace FluxKit.App.Modules.FluxIsland.Design;

/// <summary>
/// Long titles, read in full (plan 5b): while the pointer is over <c>area</c>, a text that is cut off slides left to
/// show its end, pauses, and slides back. At rest it is trimmed with an ellipsis as usual. Runs on the compositor and
/// only while hovered.
/// </summary>
internal static class Marquee
{
    private const float Speed = 40; // pixels per second

    /// <param name="area">What the pointer must be over (the row holding the text).</param>
    /// <param name="text">The text; its parent should be no wider than the space it has.</param>
    /// <param name="animate">Whether motion is allowed right now (reduced motion turns it off).</param>
    public static void Attach(UIElement area, TextBlock text, Func<bool> animate)
    {
        area.PointerEntered += (_, _) => Start(text, animate());
        area.PointerExited += (_, _) => Stop(text);
    }

    private static void Start(TextBlock text, bool animate)
    {
        if (!animate || text.Parent is not FrameworkElement holder)
        {
            return;
        }

        text.TextTrimming = TextTrimming.None;
        text.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        float overflow = (float)(text.DesiredSize.Width - holder.ActualWidth);
        if (overflow <= 4)
        {
            text.TextTrimming = TextTrimming.CharacterEllipsis;
            return;
        }

        // The text may now be wider than its holder; keep it inside.
        holder.Clip = new RectangleGeometry { Rect = new Windows.Foundation.Rect(0, -4, holder.ActualWidth, holder.ActualHeight + 8) };
        ElementCompositionPreview.SetIsTranslationEnabled(text, true);
        Visual visual = ElementCompositionPreview.GetElementVisual(text);
        Compositor compositor = visual.Compositor;
        float travel = overflow / Speed;
        float hold = 1.2f;
        float total = 0.6f + travel + hold + travel;
        Vector3KeyFrameAnimation slide = compositor.CreateVector3KeyFrameAnimation();
        CubicBezierEasingFunction ease = compositor.CreateCubicBezierEasingFunction(new Vector2(0.45f, 0), new Vector2(0.55f, 1));
        slide.InsertKeyFrame(0.6f / total, Vector3.Zero);
        slide.InsertKeyFrame((0.6f + travel) / total, new Vector3(-overflow, 0, 0), ease);
        slide.InsertKeyFrame((0.6f + travel + hold) / total, new Vector3(-overflow, 0, 0));
        slide.InsertKeyFrame(1, Vector3.Zero, ease);
        slide.Duration = TimeSpan.FromSeconds(total);
        visual.StartAnimation("Translation", slide);
    }

    private static void Stop(TextBlock text)
    {
        // Only a text that started scrolling has anything to undo (its trimming was turned off to scroll).
        if (text.TextTrimming != TextTrimming.None)
        {
            return;
        }

        Visual visual = ElementCompositionPreview.GetElementVisual(text);
        visual.StopAnimation("Translation");
        visual.Properties.InsertVector3("Translation", Vector3.Zero);
        text.TextTrimming = TextTrimming.CharacterEllipsis;
        if (text.Parent is FrameworkElement holder)
        {
            holder.Clip = null;
        }
    }
}
