using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Serilog;

namespace FluxKit.Core.Settings;

/// <summary>Implemented by settings sections that need clamping or cross-field fixes after loading.</summary>
public interface ISettingsSection
{
    /// <summary>Clamps or replaces invalid values in place; returns descriptions of what changed.</summary>
    IEnumerable<string> Normalize();
}

/// <summary>Reads and writes FluxKit settings as one JSON document.</summary>
public interface ISettingsService
{
    bool RecoveredFromCorruption { get; }

    bool IsReadOnly { get; }

    string FilePath { get; }

    event EventHandler<string>? SettingsChanged;

    T Read<T>(string section) where T : class, new();

    Task UpdateAsync<T>(string section, Func<T, T> mutate) where T : class, new();

    Task FlushAsync();

    Task ResetAsync();
}

/// <summary>
/// JSON settings store with tolerant per-field loading, unknown-field preservation,
/// schema migration, corrupt-file quarantine, debounced atomic writes and a .bak copy.
/// Section keys come from <see cref="SettingsKeys"/>: "global" or "utilities/&lt;id&gt;".
/// </summary>
public sealed class SettingsService : ISettingsService, IDisposable
{
    public const int CurrentSchemaVersion = 1;

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
    };

    private readonly object _sync = new();
    private readonly TimeProvider _time;
    private readonly ILogger _log;
    private readonly TimeSpan _debounce;
    private JsonObject _root;
    private ITimer? _pendingWrite;
    private int _version;

    public SettingsService(string filePath, ILogger log, TimeProvider? time = null, TimeSpan? debounce = null)
    {
        FilePath = filePath;
        _log = log.ForContext<SettingsService>();
        _time = time ?? TimeProvider.System;
        _debounce = debounce ?? TimeSpan.FromMilliseconds(300);
        _root = Load();
    }

    public string FilePath { get; }

    public bool RecoveredFromCorruption { get; private set; }

    public bool IsReadOnly { get; private set; }

    public bool IsFirstRun { get; private set; }

    public event EventHandler<string>? SettingsChanged;

    public T Read<T>(string section) where T : class, new()
    {
        JsonObject? node;
        lock (_sync)
        {
            node = Navigate(section, create: false)?.DeepClone() as JsonObject;
        }

        T value = Bind<T>(node, section);
        if (value is ISettingsSection normalizable)
        {
            foreach (string fix in normalizable.Normalize())
            {
                _log.Warning("Settings {Section}: {Fix}", section, fix);
            }
        }

        return value;
    }

    public Task UpdateAsync<T>(string section, Func<T, T> mutate) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(mutate);
        T updated = mutate(Read<T>(section));
        if (updated is ISettingsSection normalizable)
        {
            _ = normalizable.Normalize().ToList();
        }

        JsonObject serialized = JsonSerializer.SerializeToNode(updated, JsonOptions)!.AsObject();
        lock (_sync)
        {
            JsonObject target = Navigate(section, create: true)!;
            foreach ((string key, JsonNode? val) in serialized.ToList())
            {
                // Merge so unknown keys written by newer/older versions survive the round-trip.
                string? existing = target.Select(p => p.Key).FirstOrDefault(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
                if (existing is not null && existing != key)
                {
                    target.Remove(existing);
                }

                target[key] = val?.DeepClone();
            }

            ScheduleWrite();
        }

        SettingsChanged?.Invoke(this, section);
        return Task.CompletedTask;
    }

    public Task FlushAsync()
    {
        lock (_sync)
        {
            _pendingWrite?.Dispose();
            _pendingWrite = null;
            WriteNow();
        }

        return Task.CompletedTask;
    }

    public async Task ResetAsync()
    {
        lock (_sync)
        {
            _root = NewDocument();
        }

        await FlushAsync().ConfigureAwait(false);
        SettingsChanged?.Invoke(this, "*");
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_pendingWrite is not null)
            {
                _pendingWrite.Dispose();
                _pendingWrite = null;
                WriteNow();
            }
        }
    }

    private JsonObject Load()
    {
        if (!File.Exists(FilePath))
        {
            IsFirstRun = true;
            return NewDocument();
        }

        try
        {
            JsonObject root = JsonNode.Parse(File.ReadAllText(FilePath))?.AsObject()
                ?? throw new JsonException("Settings root is not an object.");
            int version = root["schemaVersion"]?.GetValueKind() == JsonValueKind.Number ? root["schemaVersion"]!.GetValue<int>() : 0;
            if (version > CurrentSchemaVersion)
            {
                IsReadOnly = true;
                _log.Warning("Settings schema {Version} is newer than supported {Supported}; opening read-only", version, CurrentSchemaVersion);
                return root;
            }

            return Migrate(root, version);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            string quarantine = Path.Combine(Path.GetDirectoryName(FilePath)!, $"settings.corrupt-{_time.GetUtcNow():yyyyMMddHHmmss}.json");
            try
            {
                File.Move(FilePath, quarantine, overwrite: true);
            }
            catch (IOException moveError)
            {
                _log.Error(moveError, "Could not quarantine corrupt settings file");
            }

            _log.Error(ex, "Settings file was invalid; moved to {Quarantine} and using defaults", Path.GetFileName(quarantine));
            RecoveredFromCorruption = true;
            return NewDocument();
        }
    }

    private static JsonObject Migrate(JsonObject root, int fromVersion)
    {
        // v0 -> v1: unversioned preview files; structure is compatible, only the version is stamped.
        if (fromVersion < 1)
        {
            root["schemaVersion"] = 1;
        }

        root["global"] ??= new JsonObject();
        root["utilities"] ??= new JsonObject();
        return root;
    }

    private static JsonObject NewDocument() => new()
    {
        ["schemaVersion"] = CurrentSchemaVersion,
        ["global"] = new JsonObject(),
        ["utilities"] = new JsonObject(),
    };

    private JsonObject? Navigate(string section, bool create)
    {
        JsonObject current = _root;
        foreach (string part in section.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current[part] is JsonObject child)
            {
                current = child;
            }
            else if (create)
            {
                var created = new JsonObject();
                current[part] = created;
                current = created;
            }
            else
            {
                return null;
            }
        }

        return current;
    }

    /// <summary>Binds each property independently so one bad value never discards the whole section.</summary>
    private T Bind<T>(JsonObject? node, string section) where T : class, new()
    {
        var value = new T();
        if (node is null)
        {
            return value;
        }

        foreach (PropertyInfo property in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite))
        {
            string name = JsonOptions.PropertyNamingPolicy!.ConvertName(property.Name);
            JsonNode? field = node.FirstOrDefault(p => string.Equals(p.Key, name, StringComparison.OrdinalIgnoreCase)).Value;
            if (field is null)
            {
                continue;
            }

            try
            {
                property.SetValue(value, field.Deserialize(property.PropertyType, JsonOptions));
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or NotSupportedException or ArgumentException)
            {
                _log.Warning("Settings {Section}.{Field} had an invalid value; using the default", section, name);
            }
        }

        return value;
    }

    private void ScheduleWrite()
    {
        if (IsReadOnly)
        {
            return;
        }

        _pendingWrite?.Dispose();
        _pendingWrite = _time.CreateTimer(_ =>
        {
            lock (_sync)
            {
                _pendingWrite?.Dispose();
                _pendingWrite = null;
                WriteNow();
            }
        }, null, _debounce, Timeout.InfiniteTimeSpan);
    }

    private void WriteNow()
    {
        if (IsReadOnly)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            string temp = FilePath + ".tmp";
            File.WriteAllText(temp, _root.ToJsonString(JsonOptions));
            if (File.Exists(FilePath))
            {
                File.Replace(temp, FilePath, FilePath + ".bak", ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temp, FilePath);
            }

            _version++;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Error(ex, "Saving settings failed");
        }
    }

    internal int WriteCount => _version;
}
