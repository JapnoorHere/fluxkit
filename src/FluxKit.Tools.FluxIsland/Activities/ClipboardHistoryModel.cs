namespace FluxKit.Tools.FluxIsland.Activities;

/// <summary>One copied piece of text, and whether it is pinned so clearing leaves it.</summary>
public sealed record ClipEntry(string Text, DateTimeOffset At, bool Pinned);

/// <summary>
/// The text copied lately, newest first (pinned items first of all), at most <see cref="MaxItems"/> plus pins.
/// Copying the same text again moves it to the top. Only used when the user turns clipboard history on; kept in
/// memory only and gone when FluxKit exits.
/// </summary>
public sealed class ClipboardHistoryModel
{
    public const int MaxItems = 25;
    public const int MaxLength = 2048;

    private readonly List<ClipEntry> _items = [];

    public IReadOnlyList<ClipEntry> Items => [.. _items.Where(i => i.Pinned), .. _items.Where(i => !i.Pinned)];

    public int Count => _items.Count;

    public bool Add(string text, DateTimeOffset at)
    {
        text = text.Trim();
        if (text.Length == 0)
        {
            return false;
        }

        if (text.Length > MaxLength)
        {
            text = text[..MaxLength];
        }

        bool pinned = _items.Any(i => i.Pinned && string.Equals(i.Text, text, StringComparison.Ordinal));
        _items.RemoveAll(i => string.Equals(i.Text, text, StringComparison.Ordinal));
        _items.Insert(0, new ClipEntry(text, at, pinned));

        // The oldest unpinned items drop off; pins stay.
        while (_items.Count(i => !i.Pinned) > MaxItems)
        {
            _items.RemoveAt(_items.FindLastIndex(i => !i.Pinned));
        }

        return true;
    }

    public bool TogglePin(string text)
    {
        int index = _items.FindIndex(i => string.Equals(i.Text, text, StringComparison.Ordinal));
        if (index < 0)
        {
            return false;
        }

        _items[index] = _items[index] with { Pinned = !_items[index].Pinned };
        return true;
    }

    public bool Remove(string text) => _items.RemoveAll(i => string.Equals(i.Text, text, StringComparison.Ordinal)) > 0;

    /// <summary>Puts back items taken away by <see cref="Clear"/> (undo).</summary>
    public void Restore(IEnumerable<ClipEntry> items)
    {
        _items.Clear();
        _items.AddRange(items);
    }

    /// <summary>Forgets everything, pins too: for when the user turns clipboard history off.</summary>
    public void ForgetAll() => _items.Clear();

    /// <summary>Forgets everything except pinned items.</summary>
    public bool Clear() => _items.RemoveAll(i => !i.Pinned) > 0;
}
