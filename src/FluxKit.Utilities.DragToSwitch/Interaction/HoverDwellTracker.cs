using FluxKit.Platform.Windows;

namespace FluxKit.Utilities.DragToSwitch.Interaction;

/// <summary>Hit-tests card rectangles with hysteresis so a cursor wobbling on an edge keeps its card.</summary>
public static class CardHitTester
{
    public static int? HitTest(IReadOnlyList<PixelRect> cards, int x, int y, int? currentIndex, int hysteresisPx)
    {
        if (currentIndex is int current && current >= 0 && current < cards.Count && cards[current].Inflate(hysteresisPx).Contains(x, y))
        {
            return current;
        }

        for (int i = 0; i < cards.Count; i++)
        {
            if (cards[i].Contains(x, y))
            {
                return i;
            }
        }

        return null;
    }
}

/// <summary>
/// Tracks how long the cursor has rested on one card. Brief exits shorter than the grace period do not
/// reset the dwell; the dwell fires exactly once per continuous hover.
/// </summary>
public sealed class HoverDwellTracker(TimeSpan grace)
{
    private DateTimeOffset _lastSeenOnTarget;
    private bool _fired;

    public int? Target { get; private set; }

    public DateTimeOffset? Since { get; private set; }

    /// <summary>Feeds the latest hit; returns true when the dwell for <see cref="Target"/> has just completed.</summary>
    public bool Update(int? hit, DateTimeOffset now, TimeSpan delay)
    {
        if (hit is not null && hit == Target)
        {
            _lastSeenOnTarget = now;
        }
        else if (Target is not null && now - _lastSeenOnTarget < grace)
        {
            // Within the grace window: keep the current target and its timer.
        }
        else
        {
            Target = hit;
            Since = hit is null ? null : now;
            _lastSeenOnTarget = now;
            _fired = false;
        }

        if (Target is not null && !_fired && Since is { } since && now - since >= delay)
        {
            _fired = true;
            return true;
        }

        return false;
    }

    public void Reset()
    {
        Target = null;
        Since = null;
        _fired = false;
    }
}

/// <summary>Prevents repeated or overlapping switches.</summary>
public sealed class SwitchGuard(TimeSpan cooldown)
{
    private DateTimeOffset _cooldownUntil = DateTimeOffset.MinValue;
    private bool _mustLeaveCards;

    public bool InFlight { get; private set; }

    public bool CanSwitch(DateTimeOffset now) => !InFlight && !_mustLeaveCards && now >= _cooldownUntil;

    /// <summary>Number-key switches skip the leave-cards rule (the cursor may legitimately rest on a card).</summary>
    public bool CanSwitchByKey(DateTimeOffset now) => !InFlight && now >= _cooldownUntil;

    public void Begin() => InFlight = true;

    public void Complete(DateTimeOffset now, bool succeeded)
    {
        InFlight = false;
        if (succeeded)
        {
            _cooldownUntil = now + cooldown;
            _mustLeaveCards = true;
        }
    }

    /// <summary>Called whenever the cursor is outside every card.</summary>
    public void CursorLeftCards() => _mustLeaveCards = false;

    public void Reset()
    {
        InFlight = false;
        _mustLeaveCards = false;
        _cooldownUntil = DateTimeOffset.MinValue;
    }
}
