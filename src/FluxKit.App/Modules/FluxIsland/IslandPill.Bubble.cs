using System.Numerics;
using FluxKit.Tools.FluxIsland.Activities;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>The second activity's bubble beside the pill, which buds off it and merges back.</summary>
public sealed partial class IslandPill
{
    /// <summary>Shows the second activity in a bubble that springs out from the pill's right edge, or tucks it away.</summary>
    private void UpdateBubble(IslandActivity? secondary, Size pill, DateTimeOffset now)
    {
        Visual visual = ElementCompositionPreview.GetElementVisual(Bubble);
        visual.CenterPoint = new Vector3((float)BubbleSize / 2, (float)BubbleSize / 2, 0);
        bool show = secondary is not null;
        float x = (float)((pill.Width / 2) + BubbleGap + (BubbleSize / 2));
        if (show)
        {
            CompactContent content = IslandPresenter.Describe(secondary!, now);
            bool art = secondary!.Kind == ActivityKind.Media && _art.Image is not null;
            BubbleArtBrush.ImageSource = art ? _art.Image : null;
            BubbleArt.Visibility = art ? Visibility.Visible : Visibility.Collapsed;
            BubbleGlyph.Visibility = art ? Visibility.Collapsed : Visibility.Visible;
            BubbleGlyph.Glyph = content.Glyph;
            BubbleGlyph.Foreground = new SolidColorBrush(secondary.Kind == ActivityKind.Media ? _art.Accent : content.Accent);
            AutomationProperties.SetName(Bubble, string.Join(", ", new[] { content.Title, content.Trail }.Where(t => !string.IsNullOrEmpty(t))));
            ToolTipService.SetToolTip(Bubble, content.Title);
        }

        if (show == _bubbleShown)
        {
            // Already out: just follow the pill's width.
            if (show)
            {
                Spring(visual, "Translation", new Vector3(x, 0, 0));
            }

            return;
        }

        _bubbleShown = show;
        if (show)
        {
            // It buds off the pill's edge: starts small and tucked in, then springs out to its place.
            Bubble.Visibility = Visibility.Visible;
            visual.StopAnimation("Opacity");
            visual.Opacity = 1;
            visual.Properties.InsertVector3("Translation", new Vector3(x - 24, 0, 0));
            visual.Scale = new Vector3(0.4f, 0.4f, 1);
            Spring(visual, "Translation", new Vector3(x, 0, 0));
            Spring(visual, "Scale", Vector3.One);
            _shape.Bud((float)pill.Width, (float)(TopInset + (CompactHeight / 2)), (float)BubbleGap, splitting: true, Springs);
            return;
        }

        Spring(visual, "Translation", new Vector3(x - 24, 0, 0));
        Spring(visual, "Scale", new Vector3(0.4f, 0.4f, 1));
        _shape.Bud((float)pill.Width, (float)(TopInset + (CompactHeight / 2)), (float)BubbleGap, splitting: false, Springs);
        IslandMotion.Exit(Bubble, () => !_bubbleShown, shrink: false);
    }

    private void Spring(Visual visual, string property, Vector3 to)
    {
        if (!Springs)
        {
            visual.StopAnimation(property);
            if (property == "Scale")
            {
                visual.Scale = to;
            }
            else
            {
                visual.Properties.InsertVector3(property, to);
            }

            return;
        }

        SpringVector3NaturalMotionAnimation spring = visual.Compositor.CreateSpringVector3Animation();
        spring.DampingRatio = 0.62f;
        spring.Period = TimeSpan.FromMilliseconds(50);
        spring.FinalValue = to;
        visual.StartAnimation(property, spring);
    }
}
