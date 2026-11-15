using FluxKit.Core;
using FluxKit.Tools.FluxIsland.Abstractions;
using Serilog;

namespace FluxKit.Tools.FluxIsland.Sources;

/// <summary><see cref="IShelfStore"/> as a JSON list of paths in <see cref="FluxKitPaths.ShelfFile"/>. A bad file is ignored, never fatal.</summary>
internal sealed class JsonShelfStore(string file, ILogger log) : IShelfStore
{
    private readonly JsonFile<List<string>> _file = new(file, IslandJsonContext.Default.ListString, log.ForContext<JsonShelfStore>(), "shelf");

    public IReadOnlyList<string> Load() => _file.Read() ?? [];

    public void Save(IReadOnlyList<string> paths) => _file.Write([.. paths]);

    public void Delete() => _file.Delete();
}
