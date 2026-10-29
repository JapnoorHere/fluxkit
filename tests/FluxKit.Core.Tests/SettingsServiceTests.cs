using System.Text.Json.Nodes;
using FluxKit.Core.Logging;
using FluxKit.Core.Settings;
using Microsoft.Extensions.Time.Testing;
using Serilog;

namespace FluxKit.Core.Tests;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("fluxkit-settings-").FullName;
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero));

    private string FilePath => Path.Combine(_dir, "settings.json");

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private SettingsService Create() => new(FilePath, new LoggerConfiguration().CreateLogger(), _time);

    private sealed class SampleSection : ISettingsSection
    {
        public int Delay { get; set; } = 600;

        public AppTheme Theme { get; set; } = AppTheme.System;

        public bool Flag { get; set; } = true;

        public IEnumerable<string> Normalize()
        {
            if (Delay is < 200 or > 1500)
            {
                Delay = Math.Clamp(Delay, 200, 1500);
                yield return "delay clamped";
            }
        }
    }

    [Fact]
    public void Missing_file_yields_defaults_and_first_run()
    {
        using SettingsService service = Create();

        Assert.True(service.IsFirstRun);
        Assert.False(service.RecoveredFromCorruption);
        Assert.Equal(AppTheme.System, service.Read<GlobalSettings>(GlobalSettings.Section).Theme);
        Assert.False(File.Exists(FilePath));
    }

    [Fact]
    public async Task Update_round_trips_after_flush()
    {
        using (SettingsService service = Create())
        {
            await service.UpdateAsync<GlobalSettings>(GlobalSettings.Section, s =>
            {
                s.Theme = AppTheme.Dark;
                s.LaunchAtStartup = true;
                return s;
            });
            await service.FlushAsync();
        }

        using SettingsService reloaded = Create();
        GlobalSettings global = reloaded.Read<GlobalSettings>(GlobalSettings.Section);
        Assert.Equal(AppTheme.Dark, global.Theme);
        Assert.True(global.LaunchAtStartup);
        Assert.False(reloaded.IsFirstRun);
    }

    [Fact]
    public async Task Unknown_fields_are_preserved()
    {
        File.WriteAllText(FilePath, """{"schemaVersion":1,"global":{"theme":"Light","futureField":42},"futureSection":{"x":1}}""");
        using (SettingsService service = Create())
        {
            await service.UpdateAsync<GlobalSettings>(GlobalSettings.Section, s =>
            {
                s.LaunchAtStartup = true;
                return s;
            });
            await service.FlushAsync();
        }

        JsonObject root = JsonNode.Parse(File.ReadAllText(FilePath))!.AsObject();
        Assert.Equal(42, root["global"]!["futureField"]!.GetValue<int>());
        Assert.Equal(1, root["futureSection"]!["x"]!.GetValue<int>());
        Assert.Equal("Light", root["global"]!["theme"]!.GetValue<string>());
    }

    [Fact]
    public void Corrupt_file_is_quarantined_and_defaults_used()
    {
        File.WriteAllText(FilePath, "{ this is not json");

        using SettingsService service = Create();

        Assert.True(service.RecoveredFromCorruption);
        Assert.False(File.Exists(FilePath));
        Assert.Single(Directory.GetFiles(_dir, "settings.corrupt-20261026120000.json"));
        Assert.Equal(AppTheme.System, service.Read<GlobalSettings>(GlobalSettings.Section).Theme);
    }

    [Fact]
    public void Invalid_field_falls_back_only_for_that_field()
    {
        File.WriteAllText(FilePath, """{"schemaVersion":1,"utilities":{"x":{"delay":"soon","theme":"Purple","flag":false}}}""");

        using SettingsService service = Create();
        SampleSection section = service.Read<SampleSection>("utilities/x");

        Assert.Equal(600, section.Delay);
        Assert.Equal(AppTheme.System, section.Theme);
        Assert.False(section.Flag);
    }

    [Theory]
    [InlineData(50, 200)]
    [InlineData(99999, 1500)]
    [InlineData(700, 700)]
    public void Out_of_range_values_are_clamped(int stored, int expected)
    {
        File.WriteAllText(FilePath, """{"schemaVersion":1,"utilities":{"x":{"delay":""" + stored + "}}}");

        using SettingsService service = Create();

        Assert.Equal(expected, service.Read<SampleSection>("utilities/x").Delay);
    }

    [Fact]
    public void Unversioned_file_is_migrated_to_current_schema()
    {
        File.WriteAllText(FilePath, """{"global":{"theme":"Dark"}}""");

        using SettingsService service = Create();

        Assert.False(service.IsReadOnly);
        Assert.Equal(AppTheme.Dark, service.Read<GlobalSettings>(GlobalSettings.Section).Theme);
    }

    [Fact]
    public async Task Newer_schema_opens_read_only_and_never_writes()
    {
        const string content = """{"schemaVersion":99,"global":{"theme":"Dark"}}""";
        File.WriteAllText(FilePath, content);

        using (SettingsService service = Create())
        {
            Assert.True(service.IsReadOnly);
            await service.UpdateAsync<GlobalSettings>(GlobalSettings.Section, s =>
            {
                s.Theme = AppTheme.Light;
                return s;
            });
            await service.FlushAsync();
        }

        Assert.Equal(content, File.ReadAllText(FilePath));
    }

    [Fact]
    public async Task Writes_are_debounced_and_coalesced()
    {
        using SettingsService service = Create();
        for (int i = 0; i < 5; i++)
        {
            await service.UpdateAsync<GlobalSettings>(GlobalSettings.Section, s =>
            {
                s.LaunchAtStartup = !s.LaunchAtStartup;
                return s;
            });
            _time.Advance(TimeSpan.FromMilliseconds(100));
        }

        Assert.Equal(0, service.WriteCount);
        _time.Advance(TimeSpan.FromMilliseconds(300));
        Assert.Equal(1, service.WriteCount);
    }

    [Fact]
    public async Task Second_write_keeps_a_backup()
    {
        using SettingsService service = Create();
        await service.FlushAsync();
        await service.UpdateAsync<GlobalSettings>(GlobalSettings.Section, s =>
        {
            s.Theme = AppTheme.Dark;
            return s;
        });
        await service.FlushAsync();

        Assert.True(File.Exists(FilePath + ".bak"));
        Assert.False(File.Exists(FilePath + ".tmp"));
    }

    [Fact]
    public async Task Reset_restores_defaults_and_raises_wildcard_change()
    {
        using SettingsService service = Create();
        await service.UpdateAsync<GlobalSettings>(GlobalSettings.Section, s =>
        {
            s.Theme = AppTheme.Dark;
            return s;
        });
        string? changed = null;
        service.SettingsChanged += (_, section) => changed = section;

        await service.ResetAsync();

        Assert.Equal("*", changed);
        Assert.Equal(AppTheme.System, service.Read<GlobalSettings>(GlobalSettings.Section).Theme);
    }

    [Fact]
    public void Verbose_logging_reverts_after_expiry()
    {
        var settings = new GlobalSettings { LogLevel = LogVerbosity.Verbose, VerboseUntilUtc = _time.GetUtcNow().AddHours(-1) };

        Assert.Equal(LogVerbosity.Normal, LoggingSetup.Apply(settings, _time.GetUtcNow()));
        settings.VerboseUntilUtc = _time.GetUtcNow().AddHours(1);
        Assert.Equal(LogVerbosity.Verbose, LoggingSetup.Apply(settings, _time.GetUtcNow()));
    }

    [Theory]
    [InlineData(@"Failed to open C:\Users\me\secret.txt now", "Failed to open <path> now")]
    [InlineData(@"share \\server\x\y.docx", "share <path>")]
    [InlineData("no paths here", "no paths here")]
    public void Redaction_removes_paths(string input, string expected) =>
        Assert.Equal(expected, Redaction.ScrubPaths(input));
}
