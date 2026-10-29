namespace FluxKit.Tools.FluxIsland.Activities;

/// <summary>
/// The files waiting on the shelf. Only paths are kept: the files themselves are never copied, moved or opened.
/// Newest first, no duplicates, at most <see cref="MaxItems"/>.
/// </summary>
internal sealed class ShelfModel
{
    public const int MaxItems = 12;

    private readonly List<string> _items = [];

    public IReadOnlyList<string> Items => [.. _items];

    public int Count => _items.Count;

    /// <summary>Adds paths to the front of the shelf. Returns true if anything changed.</summary>
    public bool Add(IEnumerable<string> paths)
    {
        bool changed = false;
        foreach (string path in paths.Where(p => !string.IsNullOrWhiteSpace(p)).Reverse())
        {
            int existing = _items.FindIndex(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            if (existing == 0)
            {
                continue;
            }

            if (existing > 0)
            {
                _items.RemoveAt(existing);
            }

            _items.Insert(0, path);
            changed = true;
        }

        if (_items.Count > MaxItems)
        {
            _items.RemoveRange(MaxItems, _items.Count - MaxItems);
        }

        return changed;
    }

    public bool Remove(string path) => _items.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase)) > 0;

    public bool Clear()
    {
        bool had = _items.Count > 0;
        _items.Clear();
        return had;
    }
}
