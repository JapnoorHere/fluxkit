using System.Numerics;
using Microsoft.UI.Composition;

namespace FluxKit.App.Modules.FluxIsland.Design;

/// <summary>A named spring feel: how much it overshoots (damping) and how fast it settles (period).</summary>
internal readonly record struct SpringPreset(float Damping, TimeSpan Period);

/// <summary>
/// The island's motion vocabulary. Every animation picks one of these, so related things move alike and the whole
/// island feels like one object. All of them run on the compositor; nothing animates on the UI thread.
/// </summary>
internal static class IslandSprings
{
    /// <summary>Taps, chips and presses: quick and firm.</summary>
    public static readonly SpringPreset Snappy = new(0.78f, TimeSpan.FromMilliseconds(36));

    /// <summary>Page changes and content sliding: calm, no visible overshoot.</summary>
    public static readonly SpringPreset Smooth = new(0.9f, TimeSpan.FromMilliseconds(55));

    /// <summary>Things arriving: a lively overshoot.</summary>
    public static readonly SpringPreset Bouncy = new(0.6f, TimeSpan.FromMilliseconds(46));

    /// <summary>The pill changing size: a small, satisfying overshoot.</summary>
    public static readonly SpringPreset Morph = new(0.74f, TimeSpan.FromMilliseconds(48));

    /// <summary>What reduced motion uses instead of any spring.</summary>
    public static readonly TimeSpan ReducedDuration = TimeSpan.FromMilliseconds(120);

    public static SpringVector2NaturalMotionAnimation Vector2(Compositor compositor, SpringPreset preset, Vector2 to)
    {
        SpringVector2NaturalMotionAnimation spring = compositor.CreateSpringVector2Animation();
        spring.DampingRatio = preset.Damping;
        spring.Period = preset.Period;
        spring.FinalValue = to;
        return spring;
    }

    public static SpringVector3NaturalMotionAnimation Vector3(Compositor compositor, SpringPreset preset, Vector3 to)
    {
        SpringVector3NaturalMotionAnimation spring = compositor.CreateSpringVector3Animation();
        spring.DampingRatio = preset.Damping;
        spring.Period = preset.Period;
        spring.FinalValue = to;
        return spring;
    }

    public static SpringScalarNaturalMotionAnimation Scalar(Compositor compositor, SpringPreset preset, float to)
    {
        SpringScalarNaturalMotionAnimation spring = compositor.CreateSpringScalarAnimation();
        spring.DampingRatio = preset.Damping;
        spring.Period = preset.Period;
        spring.FinalValue = to;
        return spring;
    }

    /// <summary>The standard ease-out curve: fast start, long soft landing.</summary>
    public static CubicBezierEasingFunction EaseOut(Compositor compositor) =>
        compositor.CreateCubicBezierEasingFunction(new System.Numerics.Vector2(0.16f, 1f), new System.Numerics.Vector2(0.3f, 1f));

    /// <summary>For things leaving: accelerates away.</summary>
    public static CubicBezierEasingFunction EaseIn(Compositor compositor) =>
        compositor.CreateCubicBezierEasingFunction(new System.Numerics.Vector2(0.4f, 0f), new System.Numerics.Vector2(1f, 1f));
}
