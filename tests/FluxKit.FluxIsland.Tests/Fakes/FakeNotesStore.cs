using FluxKit.Tools.FluxIsland.Abstractions;
using FluxKit.Tools.FluxIsland.Activities;

namespace FluxKit.FluxIsland.Tests.Fakes;

internal sealed class FakeNotesStore : INotesStore
{
    public List<IslandNote> Saved { get; } = [];

    public bool Deleted { get; private set; }

    public IReadOnlyList<IslandNote> Load() => [.. Saved];

    public void Save(IReadOnlyList<IslandNote> notes)
    {
        Saved.Clear();
        Saved.AddRange(notes);
        Deleted = false;
    }

    public void Delete()
    {
        Saved.Clear();
        Deleted = true;
    }
}
