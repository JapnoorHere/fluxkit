namespace FluxKit.Tools.FluxIsland.Abstractions;

/// <summary>
/// Windows' own per-app "Show notification banners" switch (ADR-014). A value of 0 hides the banner; null means the
/// value isn't set, which Windows treats as shown.
/// </summary>
public interface IBannerSwitch
{
    int? Read(string appId);

    /// <summary>Sets the value, or removes it when <paramref name="value"/> is null.</summary>
    void Write(string appId, int? value);
}

/// <summary>Remembers each app's banner value from before FluxKit changed it, so it can be put back exactly.</summary>
public interface IBannerSnapshotStore
{
    IReadOnlyDictionary<string, int?> Load();

    void Save(IReadOnlyDictionary<string, int?> saved);
}
