namespace FluxKit.Tools.FluxIsland.Activities;

/// <summary>A moment that already showed, and when.</summary>
public sealed record RewindEntry(IslandActivity Activity, DateTimeOffset At);

/// <summary>
/// The last <see cref="MaxItems"/> moments worth looking back at (downloads, screenshots, copied links, finished
/// timers, devices…), newest first, so nothing missed while away is lost. Kept in memory only; quick status moments
/// such as volume or Caps Lock aren't kept.
/// </summary>
public sealed class MomentHistory
{
    public const int MaxItems = 30;

    private readonly List<RewindEntry> _items = [];

    public IReadOnlyList<RewindEntry> Items => [.. _items];

    public int Count => _items.Count;

    public static bool IsWorthKeeping(IslandActivity activity) => activity.Kind switch
    {
        ActivityKind.Copied => activity.Smart != SmartText.None && activity.Text is not null,
        ActivityKind.DownloadDone or ActivityKind.Screenshot or ActivityKind.FileReady or ActivityKind.TimerDone => true,
        ActivityKind.DeviceConnected or ActivityKind.DeviceDisconnected or ActivityKind.AudioDevice => true,
        ActivityKind.LowBattery or ActivityKind.Offline => true,
        _ => false,
    };

    /// <summary>Keeps <paramref name="activity"/> if it is worth looking back at. Returns true if it was kept.</summary>
    public bool Record(IslandActivity activity, DateTimeOffset at)
    {
        if (!IsWorthKeeping(activity))
        {
            return false;
        }

        _items.Insert(0, new RewindEntry(activity, at));
        if (_items.Count > MaxItems)
        {
            _items.RemoveRange(MaxItems, _items.Count - MaxItems);
        }

        return true;
    }

    /// <summary>Puts back entries taken away by <see cref="Clear"/> (undo).</summary>
    public void Restore(IEnumerable<RewindEntry> entries)
    {
        _items.Clear();
        _items.AddRange(entries.Take(MaxItems));
    }

    public bool Clear()
    {
        bool had = _items.Count > 0;
        _items.Clear();
        return had;
    }
}
