using System.Numerics;
using FluxKit.App.Modules.FluxIsland.Design;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Windows.Foundation;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>
/// Continuity (plan M4/M5): when the island opens or closes, the thing it was showing doesn't vanish and reappear. The
/// compact icon, ring or art travels and grows into its place on the panel (the album art, the timer's dial), and
/// shrinks back on close. This is the FLIP technique: note where the element was, lay out the new state, then start
/// the new element at the old place and size and spring it home, on the compositor.
/// </summary>
public sealed partial class IslandPill
{
    /// <summary>The element that carries a moment's identity in <paramref name="view"/>, if it has one on screen.</summary>
    private UIElement? HeroOf(UIElement? view)
    {
        if (view == CompactView)
        {
            UIElement[] candidates = [CompactArtFrame, CompactRing, CompactGlyph];
            return candidates.FirstOrDefault(c => c.Visibility == Visibility.Visible);
        }

        return view == MediaView ? MediaView.Hero : view == TimerView ? TimerView.Hero : null;
    }

    /// <summary>Where <paramref name="element"/> is on the stage right now, or null if it isn't laid out.</summary>
    private Rect? BoundsOnStage(UIElement? element)
    {
        if (element is not FrameworkElement { ActualWidth: > 0, ActualHeight: > 0 } framed)
        {
            return null;
        }

        try
        {
            return framed.TransformToVisual(Stage).TransformBounds(new Rect(0, 0, framed.ActualWidth, framed.ActualHeight));
        }
        catch (ArgumentException)
        {
            return null; // Not in the tree yet.
        }
    }

    /// <summary>
    /// Moves the new view's hero from where the old view's hero was to its own place, scaling from the old size, with
    /// the morph spring. Only between compact and panel, and only when both have a hero.
    /// </summary>
    private void Travel(Rect? from, UIElement newView)
    {
        if (!Springs || from is not { } start || HeroOf(newView) is not FrameworkElement hero)
        {
            return;
        }

        newView.UpdateLayout();
        if (BoundsOnStage(hero) is not { } end || end.Width <= 0)
        {
            return;
        }

        var delta = new Vector3(
            (float)((start.X + (start.Width / 2)) - (end.X + (end.Width / 2))),
            (float)((start.Y + (start.Height / 2)) - (end.Y + (end.Height / 2))),
            0);
        float scale = (float)Math.Clamp(start.Width / end.Width, 0.2, 5);

        ElementCompositionPreview.SetIsTranslationEnabled(hero, true);
        Visual visual = ElementCompositionPreview.GetElementVisual(hero);
        visual.CenterPoint = new Vector3((float)end.Width / 2, (float)end.Height / 2, 0);
        visual.Properties.InsertVector3("Translation", delta);
        visual.Scale = new Vector3(scale, scale, 1);
        visual.StartAnimation("Translation", IslandSprings.Vector3(visual.Compositor, IslandSprings.Morph, Vector3.Zero));
        visual.StartAnimation("Scale", IslandSprings.Vector3(visual.Compositor, IslandSprings.Morph, Vector3.One));
    }
}
