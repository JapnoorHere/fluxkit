using FluxKit.Tools.FluxIsland.Abstractions;

namespace FluxKit.Tools.FluxIsland.Activities;

/// <summary>
/// Today's notifications for the island's Notifications page, newest first, at most <see cref="MaxItems"/>. Kept in
/// memory only and gone when FluxKit exits; the text is never logged.
/// </summary>
public sealed class NotificationInbox
{
    public const int MaxItems = 50;

    private readonly List<ToastInfo> _items = [];

    public IReadOnlyList<ToastInfo> Items => [.. _items];

    public int Count => _items.Count;

    public void Add(ToastInfo toast)
    {
        _items.RemoveAll(t => t.Id == toast.Id);
        _items.Insert(0, toast);
        if (_items.Count > MaxItems)
        {
            _items.RemoveRange(MaxItems, _items.Count - MaxItems);
        }
    }

    public ToastInfo? Find(uint id) => _items.FirstOrDefault(t => t.Id == id);

    public bool Remove(uint id) => _items.RemoveAll(t => t.Id == id) > 0;

    /// <summary>Clears one app's group.</summary>
    public bool RemoveApp(string appId) => _items.RemoveAll(t => string.Equals(t.AppId, appId, StringComparison.OrdinalIgnoreCase)) > 0;

    public bool Clear()
    {
        bool had = _items.Count > 0;
        _items.Clear();
        return had;
    }

    /// <summary>Puts back what <see cref="Clear"/> or <see cref="RemoveApp"/> took away (undo).</summary>
    public void Restore(IEnumerable<ToastInfo> items)
    {
        _items.Clear();
        _items.AddRange(items.Take(MaxItems));
    }
}
