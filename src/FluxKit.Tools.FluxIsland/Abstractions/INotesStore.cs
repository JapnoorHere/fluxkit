using FluxKit.Tools.FluxIsland.Activities;

namespace FluxKit.Tools.FluxIsland.Abstractions;

/// <summary>Keeps quick notes between runs, on this PC only.</summary>
public interface INotesStore
{
    IReadOnlyList<IslandNote> Load();

    void Save(IReadOnlyList<IslandNote> notes);

    /// <summary>Forgets the saved notes, for when the user turns keeping them off.</summary>
    void Delete();
}
