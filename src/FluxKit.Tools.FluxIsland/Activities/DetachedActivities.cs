namespace FluxKit.Tools.FluxIsland.Activities;

/// <summary>Where a torn-off droplet sits on screen, in screen pixels (its top-left corner).</summary>
public sealed record DropletPlace(ActivityKind Kind, int X, int Y);

/// <summary>
/// Activities torn off the island into their own floating droplets (plan A). A detached activity leaves the pill and
/// lives in its droplet until it is docked again or ends. Only lasting activities can be torn off: music, a timer, a
/// stopwatch. Pure, so it is tested without windows.
/// </summary>
public sealed class DetachedActivities
{
    private readonly Dictionary<ActivityKind, DropletPlace> _places = [];

    public IReadOnlyList<DropletPlace> Places => [.. _places.Values.OrderBy(p => p.Kind)];

    public static bool CanDetach(ActivityKind kind) => kind is ActivityKind.Media or ActivityKind.Timer or ActivityKind.Stopwatch;

    public bool IsDetached(ActivityKind kind) => _places.ContainsKey(kind);

    /// <summary>Tears <paramref name="kind"/> off into a droplet at <paramref name="x"/>, <paramref name="y"/>. False if it can't be.</summary>
    public bool Detach(ActivityKind kind, int x, int y)
    {
        if (!CanDetach(kind))
        {
            return false;
        }

        _places[kind] = new DropletPlace(kind, x, y);
        return true;
    }

    /// <summary>Moves a droplet. False when <paramref name="kind"/> isn't detached or didn't move.</summary>
    public bool Move(ActivityKind kind, int x, int y)
    {
        if (!_places.TryGetValue(kind, out DropletPlace? place) || (place.X == x && place.Y == y))
        {
            return false;
        }

        _places[kind] = place with { X = x, Y = y };
        return true;
    }

    /// <summary>Puts the activity back on the island. False when it wasn't detached.</summary>
    public bool Dock(ActivityKind kind) => _places.Remove(kind);

    /// <summary>Restores the droplets saved last time, ignoring anything that can't be detached.</summary>
    public void Restore(IEnumerable<DropletPlace> places)
    {
        _places.Clear();
        foreach (DropletPlace place in places.Where(p => CanDetach(p.Kind)))
        {
            _places[place.Kind] = place;
        }
    }
}
