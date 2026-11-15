using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Serilog;

namespace FluxKit.Tools.FluxIsland.Sources;

/// <summary>
/// Reads and writes one small JSON file safely: a missing or broken file reads as nothing, writes go to a temporary
/// file that then replaces the real one, and failures are logged, never thrown. The file's contents are never logged.
/// </summary>
internal sealed class JsonFile<T>(string file, JsonTypeInfo<T> type, ILogger log, string what)
    where T : class
{
    public T? Read()
    {
        try
        {
            return File.Exists(file) ? JsonSerializer.Deserialize(File.ReadAllText(file), type) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            log.Warning(ex, "The saved {What} could not be read; starting empty", what);
            return null;
        }
    }

    public void Write(T value)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            string temp = file + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(value, type));
            File.Move(temp, file, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.Warning(ex, "Saving the {What} failed", what);
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
            log.Warning(ex, "Removing the saved {What} failed", what);
        }
    }
}
