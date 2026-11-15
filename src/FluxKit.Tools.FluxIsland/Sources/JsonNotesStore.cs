using FluxKit.Core;
using FluxKit.Tools.FluxIsland.Abstractions;
using FluxKit.Tools.FluxIsland.Activities;
using Serilog;

namespace FluxKit.Tools.FluxIsland.Sources;

/// <summary><see cref="INotesStore"/> as JSON in <see cref="FluxKitPaths.NotesFile"/>. Note text is never logged.</summary>
internal sealed class JsonNotesStore(string file, ILogger log) : INotesStore
{
    private readonly JsonFile<List<IslandNote>> _file = new(file, IslandJsonContext.Default.ListIslandNote, log.ForContext<JsonNotesStore>(), "notes");

    public IReadOnlyList<IslandNote> Load() => _file.Read() ?? [];

    public void Save(IReadOnlyList<IslandNote> notes) => _file.Write([.. notes]);

    public void Delete() => _file.Delete();
}
