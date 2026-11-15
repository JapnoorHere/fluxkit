namespace FluxKit.Tools.FluxIsland.Activities;

/// <summary>A quick note from the command bar, with when it was written.</summary>
public sealed record IslandNote(string Text, DateTimeOffset Created);

/// <summary>Quick notes, newest first, at most <see cref="MaxItems"/>. Adding the same text again moves it to the top.</summary>
public sealed class NotesModel
{
    public const int MaxItems = 20;

    private readonly List<IslandNote> _items = [];

    public IReadOnlyList<IslandNote> Items => [.. _items];

    public int Count => _items.Count;

    /// <summary>Adds a note at the top. Returns false if it was empty.</summary>
    public bool Add(string text, DateTimeOffset now)
    {
        text = text.Trim();
        if (text.Length == 0)
        {
            return false;
        }

        _items.RemoveAll(n => string.Equals(n.Text, text, StringComparison.Ordinal));
        _items.Insert(0, new IslandNote(text, now));
        if (_items.Count > MaxItems)
        {
            _items.RemoveRange(MaxItems, _items.Count - MaxItems);
        }

        return true;
    }

    /// <summary>Puts back notes loaded from disk, keeping their order and the limit.</summary>
    public void Restore(IEnumerable<IslandNote> notes)
    {
        _items.Clear();
        _items.AddRange(notes.Where(n => !string.IsNullOrWhiteSpace(n.Text)).Take(MaxItems));
    }

    public bool Remove(string text) => _items.RemoveAll(n => string.Equals(n.Text, text, StringComparison.Ordinal)) > 0;

    public bool Clear()
    {
        bool had = _items.Count > 0;
        _items.Clear();
        return had;
    }
}
