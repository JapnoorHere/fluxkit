using System.Numerics;
using FluxKit.App.Modules.FluxIsland.Design;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>
/// Press feedback for everything on the island (plan M2/M3): it dips to 97 % with a firm spring and springs back
/// with a little bounce when released.
/// </summary>
internal static class PressFeedback
{
    private const float PressedScale = 0.97f;

    public static void Attach(UIElement element)
    {
        // handledEventsToo: buttons mark pointer events handled, and the squish must still play.
        element.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler((_, _) => ScaleTo(element, PressedScale)), true);
        element.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler((_, _) => ScaleTo(element, 1, IslandSprings.Bouncy)), true);
        element.AddHandler(UIElement.PointerExitedEvent, new PointerEventHandler((_, _) => ScaleTo(element, 1)), true);
        element.AddHandler(UIElement.PointerCaptureLostEvent, new PointerEventHandler((_, _) => ScaleTo(element, 1)), true);
    }

    private static void ScaleTo(UIElement element, float scale, SpringPreset? preset = null)
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(element);
        visual.CenterPoint = new Vector3(element.ActualSize / 2, 0);
        visual.StartAnimation("Scale", IslandSprings.Vector3(visual.Compositor, preset ?? IslandSprings.Snappy, new Vector3(scale, scale, 1)));
    }
}
