using FluxKit.Tools.FluxIsland.Abstractions;

namespace FluxKit.FluxIsland.Tests.Fakes;

internal sealed class FakeShelfStore : IShelfStore
{
    public List<string>? Saved { get; set; }

    public bool Deleted { get; private set; }

    public IReadOnlyList<string> Load() => Saved ?? [];

    public void Save(IReadOnlyList<string> paths)
    {
        Saved = [.. paths];
        Deleted = false;
    }

    public void Delete()
    {
        Saved = null;
        Deleted = true;
    }
}
