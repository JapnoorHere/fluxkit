using System.Text.Json;
using FluxKit.Core;
using FluxKit.Tools.FluxIsland.Abstractions;
using Serilog;

namespace FluxKit.Tools.FluxIsland.Sources;

/// <summary><see cref="IShelfStore"/> as a JSON list of paths in <see cref="FluxKitPaths.ShelfFile"/>. A bad file is ignored, never fatal.</summary>
internal sealed class JsonShelfStore(string file, ILogger log) : IShelfStore
{
    private readonly ILogger _log = log.ForContext<JsonShelfStore>();

    public IReadOnlyList<string> Load()
    {
        try
        {
            return File.Exists(file) ? JsonSerializer.Deserialize<List<string>>(File.ReadAllText(file)) ?? [] : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _log.Warning(ex, "The saved shelf could not be read; starting with an empty shelf");
            return [];
        }
    }

    public void Save(IReadOnlyList<string> paths)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            string temp = file + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(paths));
            File.Move(temp, file, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Warning(ex, "Saving the shelf failed");
        }
    }

    public void Delete()
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Warning(ex, "Removing the saved shelf failed");
        }
    }
}
