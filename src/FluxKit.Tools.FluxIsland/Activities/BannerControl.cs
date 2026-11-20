using FluxKit.Tools.FluxIsland.Abstractions;

namespace FluxKit.Tools.FluxIsland.Activities;

/// <summary>
/// Turns Windows' banners off for the apps the island shows instead, and back on exactly as they were. Before changing
/// an app it records the old value; the record is saved, so even after a crash the next start can restore it. Apps the
/// user never chose are never touched.
/// </summary>
public sealed class BannerControl
{
    private readonly IBannerSwitch _banners;
    private readonly IBannerSnapshotStore _store;
    private readonly Dictionary<string, int?> _saved;

    public BannerControl(IBannerSwitch banners, IBannerSnapshotStore store)
    {
        _banners = banners;
        _store = store;
        _saved = new Dictionary<string, int?>(store.Load(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The apps whose banner FluxKit has turned off right now.</summary>
    public IReadOnlyCollection<string> Hidden => _saved.Keys;

    /// <summary>Makes exactly <paramref name="appIds"/> have their banner off; every other app FluxKit changed is restored.</summary>
    public void Apply(IEnumerable<string> appIds)
    {
        var wanted = new HashSet<string>(appIds, StringComparer.OrdinalIgnoreCase);
        bool changed = false;
        foreach (string app in _saved.Keys.Where(a => !wanted.Contains(a)).ToList())
        {
            _banners.Write(app, _saved[app]);
            _saved.Remove(app);
            changed = true;
        }

        foreach (string app in wanted.Where(a => !_saved.ContainsKey(a)))
        {
            _saved[app] = _banners.Read(app);
            _banners.Write(app, 0);
            changed = true;
        }

        if (changed)
        {
            _store.Save(_saved);
        }
    }

    /// <summary>Puts every banner FluxKit turned off back as it was.</summary>
    public void RestoreAll() => Apply([]);
}
