using FluxKit.Core.Settings;

namespace FluxKit.FluxIsland.Tests.Fakes;

/// <summary>An in-memory settings store; sections start at their defaults.</summary>
internal sealed class FakeSettings : ISettingsService
{
    private readonly Dictionary<string, object> _sections = [];

    public bool RecoveredFromCorruption => false;

    public bool IsReadOnly => false;

    public string FilePath => "memory";

    public event EventHandler<string>? SettingsChanged;

    public T Read<T>(string section) where T : class, new() =>
        _sections.TryGetValue(section, out object? value) ? (T)value : new T();

    public Task UpdateAsync<T>(string section, Func<T, T> mutate) where T : class, new()
    {
        _sections[section] = mutate(Read<T>(section));
        SettingsChanged?.Invoke(this, section);
        return Task.CompletedTask;
    }

    public Task FlushAsync() => Task.CompletedTask;

    public Task ResetAsync()
    {
        _sections.Clear();
        return Task.CompletedTask;
    }
}
