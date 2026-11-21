namespace FluxKit.Tools.FluxIsland.Activities;

/// <summary>Where a moment was thrown.</summary>
public enum Flick
{
    /// <summary>Not a throw: a tap, or a drag that was let go gently.</summary>
    None,

    /// <summary>Down: put it away into its page (the moment stays in Recent, a notification in the inbox).</summary>
    Down,

    /// <summary>Up: let it evaporate (dismissed; a notification leaves Notification Center too).</summary>
    Up,

    /// <summary>Sideways: snooze it; it comes back in a while.</summary>
    Side,
}

/// <summary>Tells a throw from a tap or a gentle drag (pure, so it is tested without a pointer).</summary>
public static class FlickRules
{
    /// <summary>A drag at least this long (view pixels) counts, however slowly it was let go.</summary>
    public const double MinDistance = 28;

    /// <summary>A release at least this fast (pixels per millisecond) counts, however short the drag.</summary>
    public const double MinSpeed = 0.5;

    /// <summary>Below this the pointer barely moved: it is a tap.</summary>
    public const double TapSlop = 8;

    /// <param name="dx">How far the pointer moved across since the press.</param>
    /// <param name="dy">How far it moved down (negative: up).</param>
    /// <param name="vx">Its speed across at release, in pixels per millisecond.</param>
    /// <param name="vy">Its speed down at release.</param>
    public static Flick Classify(double dx, double dy, double vx, double vy)
    {
        double across = Math.Abs(dx);
        double down = Math.Abs(dy);
        if (Math.Max(across, down) < TapSlop)
        {
            return Flick.None;
        }

        bool vertical = down >= across;
        double distance = vertical ? dy : dx;
        double speed = vertical ? vy : vx;
        if (Math.Abs(distance) < MinDistance && Math.Abs(speed) < MinSpeed)
        {
            return Flick.None;
        }

        // A throw whose release goes against the drag (pulled down, then flung up) follows the release.
        double direction = Math.Abs(speed) >= MinSpeed ? speed : distance;
        return !vertical ? Flick.Side : direction > 0 ? Flick.Down : Flick.Up;
    }
}

/// <summary>Moments thrown sideways, waiting to come back. Newest-first is not needed: each has its own time.</summary>
public sealed class SnoozedMoments
{
    /// <summary>How long a snoozed moment stays away.</summary>
    public static readonly TimeSpan Delay = TimeSpan.FromMinutes(10);

    private const int MaxItems = 10;

    private readonly List<(IslandActivity Moment, DateTimeOffset Due)> _items = [];

    public int Count => _items.Count;

    /// <summary>When the next snoozed moment is due, or null when none is waiting.</summary>
    public DateTimeOffset? NextDueAt => _items.Count == 0 ? null : _items.Min(i => i.Due);

    public void Add(IslandActivity moment, DateTimeOffset due)
    {
        _items.Add((moment, due));
        if (_items.Count > MaxItems)
        {
            _items.RemoveAt(0);
        }
    }

    /// <summary>Takes every moment that is due by <paramref name="now"/>, oldest due first.</summary>
    public IReadOnlyList<IslandActivity> TakeDue(DateTimeOffset now)
    {
        List<(IslandActivity Moment, DateTimeOffset Due)> due = [.. _items.Where(i => i.Due <= now).OrderBy(i => i.Due)];
        _items.RemoveAll(i => i.Due <= now);
        return [.. due.Select(i => i.Moment)];
    }
}

/// <summary>
/// Scrubbing through today's moments by scrolling sideways over the island: maps the scroll to a moment, newest
/// first, and reports how far past either end the scroll has gone, for a rubber-band.
/// </summary>
public sealed class ScrubModel(int count, double step = ScrubModel.DefaultStep)
{
    /// <summary>How much sideways scroll moves one moment.</summary>
    public const double DefaultStep = 60;

    private double _offset;

    public int Count => count;

    /// <summary>The moment under the scrub (0 is the newest), or -1 when there is nothing to scrub.</summary>
    public int Index => count == 0 ? -1 : (int)Math.Clamp(Math.Round(_offset / step), 0, count - 1);

    /// <summary>How far past the first or last moment the scroll has gone (negative before the first).</summary>
    public double Overscroll => _offset < 0 ? _offset : Math.Max(0, _offset - ((count - 1) * step));

    /// <summary>Scrolls by <paramref name="delta"/>. Past either end it resists, up to one step.</summary>
    public void Scroll(double delta)
    {
        if (count == 0)
        {
            return;
        }

        double max = (count - 1) * step;
        double resisted = (_offset < 0 && delta < 0) || (_offset > max && delta > 0) ? delta / 3 : delta;
        _offset = Math.Clamp(_offset + resisted, -step, max + step);
    }

    /// <summary>Lets go: a scroll past the end springs back to it.</summary>
    public void Settle() => _offset = Math.Clamp(_offset, 0, Math.Max(0, (count - 1) * step));
}
