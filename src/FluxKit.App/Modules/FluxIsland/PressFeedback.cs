using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>A soft squish on press for buttons on the island: down to 88 %, then a spring back.</summary>
internal static class PressFeedback
{
    private const float PressedScale = 0.88f;

    public static void Attach(UIElement element)
    {
        // handledEventsToo: buttons mark pointer events handled, and the squish must still play.
        element.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler((_, _) => ScaleTo(element, PressedScale)), true);
        element.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler((_, _) => ScaleTo(element, 1)), true);
        element.AddHandler(UIElement.PointerExitedEvent, new PointerEventHandler((_, _) => ScaleTo(element, 1)), true);
        element.AddHandler(UIElement.PointerCaptureLostEvent, new PointerEventHandler((_, _) => ScaleTo(element, 1)), true);
    }

    private static void ScaleTo(UIElement element, float scale)
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(element);
        visual.CenterPoint = new Vector3(element.ActualSize / 2, 0);
        SpringVector3NaturalMotionAnimation spring = visual.Compositor.CreateSpringVector3Animation();
        spring.DampingRatio = 0.55f;
        spring.Period = TimeSpan.FromMilliseconds(40);
        spring.FinalValue = new Vector3(scale, scale, 1);
        visual.StartAnimation("Scale", spring);
    }
}
