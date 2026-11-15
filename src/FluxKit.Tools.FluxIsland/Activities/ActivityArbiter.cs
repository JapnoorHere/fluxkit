namespace FluxKit.Tools.FluxIsland.Activities;

/// <summary>
/// Decides what the compact pill shows. Lasting activities (music, a timer, the shelf) sit underneath; an alert takes
/// over for its duration, then the pill goes back. An alert of the same kind refreshes the current one, a more
/// important alert replaces it, and a less important one waits its turn (up to <see cref="MaxQueued"/>).
/// </summary>
internal sealed class ActivityArbiter(TimeProvider time)
{
    public const int MaxQueued = 3;

    private readonly Dictionary<ActivityKind, (IslandActivity Activity, DateTimeOffset? Until)> _lasting = [];
    private readonly List<(IslandActivity Activity, TimeSpan Duration)> _queue = [];
    private (IslandActivity Activity, DateTimeOffset Until)? _alert;
    private ActivityKind? _preferred;

    /// <summary>The alert on screen, or else the most important lasting activity.</summary>
    public IslandActivity? Primary => _alert?.Activity ?? Lasting().FirstOrDefault();

    /// <summary>A second lasting activity shown small beside the primary one, if there is one.</summary>
    public IslandActivity? Secondary => (_alert is { } alert ? Lasting().Where(a => a.Kind != alert.Activity.Kind) : Lasting().Skip(1)).FirstOrDefault();

    /// <summary>When the next alert or lasting activity runs out, so the owner can wake up then and call <see cref="Expire"/>.</summary>
    public DateTimeOffset? NextChangeAt
    {
        get
        {
            IEnumerable<DateTimeOffset> times = _lasting.Values.Where(v => v.Until is not null).Select(v => v.Until!.Value);
            if (_alert is { } alert)
            {
                times = times.Append(alert.Until);
            }

            return times.Any() ? times.Min() : null;
        }
    }

    public static int PriorityOf(IslandActivity activity) => activity.Kind switch
    {
        ActivityKind.TimerDone => 90,
        ActivityKind.LowBattery => 80,
        ActivityKind.Offline => 70,
        ActivityKind.Screenshot => 66,
        ActivityKind.DownloadDone => 64,
        ActivityKind.PluggedIn => 60,
        ActivityKind.DeviceConnected or ActivityKind.DeviceDisconnected => 58,
        ActivityKind.Unplugged => 55,
        ActivityKind.BatterySaver => 54,
        ActivityKind.AudioDevice => 52,
        ActivityKind.Online => 50,
        ActivityKind.DesktopSwitched => 48,
        ActivityKind.Volume or ActivityKind.Brightness => 45,
        ActivityKind.CapsLock or ActivityKind.NumLock => 44,
        ActivityKind.Copied => 40,
        ActivityKind.MicMuted => 35,
        ActivityKind.Call => 32,
        ActivityKind.Timer => 30,
        ActivityKind.Stopwatch => 28,
        ActivityKind.Media => activity.Media?.IsPlaying == true ? 25 : 10,
        ActivityKind.Download => 20,
        ActivityKind.Shelf => 15,
        _ => 0,
    };

    public static TimeSpan DurationOf(ActivityKind kind) => kind switch
    {
        ActivityKind.TimerDone => TimeSpan.FromSeconds(6),
        ActivityKind.LowBattery => TimeSpan.FromSeconds(4),
        ActivityKind.Screenshot or ActivityKind.DownloadDone => TimeSpan.FromSeconds(5),
        ActivityKind.PluggedIn => TimeSpan.FromSeconds(3.2),
        ActivityKind.Copied => TimeSpan.FromSeconds(1.4),
        ActivityKind.Volume or ActivityKind.Brightness => TimeSpan.FromSeconds(1.6),
        ActivityKind.CapsLock or ActivityKind.NumLock or ActivityKind.DesktopSwitched => TimeSpan.FromSeconds(1.4),
        _ => TimeSpan.FromSeconds(2.8),
    };

    /// <summary>Drops the alert on screen at once (the user waved it away) and shows the next queued one. Returns true if one was showing.</summary>
    public bool Dismiss()
    {
        if (_alert is null)
        {
            return false;
        }

        _alert = null;
        if (_queue.Count > 0)
        {
            (IslandActivity next, TimeSpan duration) = _queue[0];
            _queue.RemoveAt(0);
            _alert = (next, time.GetUtcNow() + duration);
        }

        return true;
    }

    /// <summary>Puts the second lasting activity in front (and the first in the bubble). Returns true if anything moved.</summary>
    public bool Swap()
    {
        List<IslandActivity> lasting = Lasting().ToList();
        if (lasting.Count < 2)
        {
            return false;
        }

        _preferred = lasting[1].Kind;
        return true;
    }

    /// <summary>Sets or clears a lasting activity. <paramref name="until"/> lets it run out on its own (e.g. paused music).</summary>
    public void SetLasting(ActivityKind kind, IslandActivity? activity, DateTimeOffset? until = null)
    {
        if (activity is null)
        {
            _lasting.Remove(kind);
        }
        else
        {
            _lasting[kind] = (activity, until);
        }
    }

    public void Raise(IslandActivity alert) => Raise(alert, DurationOf(alert.Kind));

    public void Raise(IslandActivity alert, TimeSpan duration)
    {
        DateTimeOffset until = time.GetUtcNow() + duration;
        _queue.RemoveAll(q => Cancels(alert.Kind, q.Activity.Kind));
        if (_alert is not { } current || current.Activity.Kind == alert.Kind || Cancels(alert.Kind, current.Activity.Kind) || PriorityOf(alert) >= PriorityOf(current.Activity))
        {
            if (_alert is { } replaced && replaced.Activity.Kind != alert.Kind && !Cancels(alert.Kind, replaced.Activity.Kind) && replaced.Until > time.GetUtcNow())
            {
                Enqueue(replaced.Activity, replaced.Until - time.GetUtcNow());
            }

            _alert = (alert, until);
            return;
        }

        Enqueue(alert, duration);
    }

    /// <summary>Drops whatever has run out and shows the next queued alert. Returns true if anything changed.</summary>
    public bool Expire()
    {
        DateTimeOffset now = time.GetUtcNow();
        bool changed = false;
        foreach (ActivityKind kind in _lasting.Where(p => p.Value.Until <= now).Select(p => p.Key).ToList())
        {
            _lasting.Remove(kind);
            changed = true;
        }

        if (_alert is { } alert && alert.Until <= now)
        {
            _alert = null;
            changed = true;
            if (_queue.Count > 0)
            {
                (IslandActivity next, TimeSpan duration) = _queue[0];
                _queue.RemoveAt(0);
                _alert = (next, now + duration);
            }
        }

        return changed;
    }

    /// <summary>True when <paramref name="incoming"/> makes <paramref name="shown"/> out of date, such as "back online" after "offline".</summary>
    private static bool Cancels(ActivityKind incoming, ActivityKind shown) => (incoming, shown) switch
    {
        (ActivityKind.Online, ActivityKind.Offline) or (ActivityKind.Offline, ActivityKind.Online) => true,
        (ActivityKind.PluggedIn, ActivityKind.Unplugged or ActivityKind.LowBattery) or (ActivityKind.Unplugged, ActivityKind.PluggedIn) => true,
        _ => false,
    };

    private void Enqueue(IslandActivity alert, TimeSpan duration)
    {
        _queue.RemoveAll(q => q.Activity.Kind == alert.Kind);
        _queue.Add((alert, duration));
        _queue.Sort((a, b) => PriorityOf(b.Activity).CompareTo(PriorityOf(a.Activity)));
        if (_queue.Count > MaxQueued)
        {
            _queue.RemoveRange(MaxQueued, _queue.Count - MaxQueued);
        }
    }

    /// <summary>Lasting activities, most important first, except that the one the user swapped to stays in front while it lasts.</summary>
    private IEnumerable<IslandActivity> Lasting() =>
        _lasting.Values.Select(v => v.Activity).OrderByDescending(a => a.Kind == _preferred).ThenByDescending(PriorityOf);
}
