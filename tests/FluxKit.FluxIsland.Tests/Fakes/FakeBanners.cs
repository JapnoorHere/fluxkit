using FluxKit.Tools.FluxIsland.Abstractions;

namespace FluxKit.FluxIsland.Tests.Fakes;

internal sealed class FakeBanners : IBannerSwitch, IBannerSnapshotStore
{
    public Dictionary<string, int?> Values { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, int?> Saved { get; private set; } = [];

    public int? Read(string appId) => Values.GetValueOrDefault(appId);

    public void Write(string appId, int? value) => Values[appId] = value;

    public IReadOnlyDictionary<string, int?> Load() => Saved;

    public void Save(IReadOnlyDictionary<string, int?> saved) => Saved = new(saved);
}
