namespace FluxKit.Tools.FluxIsland.Activities;

/// <summary>A window tucked away on the island (plan D). The handle only means something while FluxKit runs.</summary>
public sealed record StashedWindow(long Handle, string Title, DateTimeOffset At);

/// <summary>
/// The windows dragged onto the island and tucked away: newest first, at most <see cref="MaxItems"/>, each window once.
/// Kept in memory only, since window handles don't survive a restart. Pure, so it is tested without windows.
/// </summary>
public sealed class StashModel
{
    public const int MaxItems = 12;

    private readonly List<StashedWindow> _items = [];

    public IReadOnlyList<StashedWindow> Items => [.. _items];

    public void Add(StashedWindow window)
    {
        _items.RemoveAll(w => w.Handle == window.Handle);
        _items.Insert(0, window);
        if (_items.Count > MaxItems)
        {
            _items.RemoveRange(MaxItems, _items.Count - MaxItems);
        }
    }

    public bool Remove(long handle) => _items.RemoveAll(w => w.Handle == handle) > 0;

    /// <summary>Forgets windows that were closed in the meantime.</summary>
    public bool Prune(Func<long, bool> exists) => _items.RemoveAll(w => !exists(w.Handle)) > 0;
}
