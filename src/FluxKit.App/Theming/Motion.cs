using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;

namespace FluxKit.App.Theming;

/// <summary>Shared motion durations and a helper for one-off property animations (Fluent motion timings).</summary>
internal static class Motion
{
    public static readonly TimeSpan Entrance = TimeSpan.FromMilliseconds(167);

    public static readonly TimeSpan Exit = TimeSpan.FromMilliseconds(120);

    public static readonly TimeSpan Hover = TimeSpan.FromMilliseconds(83);

    /// <summary>Animates a double property once, easing out when <paramref name="decelerate"/> is true and in otherwise.</summary>
    public static void Animate(DependencyObject target, string property, double from, double to, TimeSpan duration, bool decelerate, TimeSpan? beginAfter = null)
    {
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = duration,
            BeginTime = beginAfter,
            EasingFunction = new ExponentialEase { EasingMode = decelerate ? EasingMode.EaseOut : EasingMode.EaseIn, Exponent = 4.5 },
        };
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, property);
        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        storyboard.Begin();
    }
}
