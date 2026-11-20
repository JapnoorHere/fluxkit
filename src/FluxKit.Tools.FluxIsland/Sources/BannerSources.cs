using System.Security;
using FluxKit.Platform.Shell;
using FluxKit.Tools.FluxIsland.Abstractions;
using Serilog;

namespace FluxKit.Tools.FluxIsland.Sources;

/// <summary><see cref="IBannerSwitch"/> backed by Windows' registry switch. A failed write is logged, never thrown.</summary>
internal sealed class RegistryBannerSwitch(ILogger log, string root = NotificationBanners.WindowsRoot) : IBannerSwitch
{
    private readonly NotificationBanners _banners = new(root);
    private readonly ILogger _log = log.ForContext<RegistryBannerSwitch>();

    public int? Read(string appId)
    {
        try
        {
            return _banners.Get(appId);
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException or ArgumentException)
        {
            _log.Warning(ex, "Reading the banner setting for {App} failed", appId);
            return null;
        }
    }

    public void Write(string appId, int? value)
    {
        try
        {
            _banners.Set(appId, value);
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException or ArgumentException)
        {
            _log.Warning(ex, "Changing the banner setting for {App} failed", appId);
        }
    }
}

/// <summary><see cref="IBannerSnapshotStore"/> as JSON in <c>banners.json</c>.</summary>
internal sealed class JsonBannerStore(string file, ILogger log) : IBannerSnapshotStore
{
    private readonly JsonFile<Dictionary<string, int?>> _file = new(file, IslandJsonContext.Default.DictionaryStringNullableInt32, log.ForContext<JsonBannerStore>(), "banner settings");

    public IReadOnlyDictionary<string, int?> Load() => _file.Read() ?? [];

    public void Save(IReadOnlyDictionary<string, int?> saved)
    {
        if (saved.Count == 0)
        {
            _file.Delete();
            return;
        }

        _file.Write(new Dictionary<string, int?>(saved, StringComparer.OrdinalIgnoreCase));
    }
}
