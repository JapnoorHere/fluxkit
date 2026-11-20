using Microsoft.Win32;

namespace FluxKit.Platform.Shell;

/// <summary>
/// Reads and writes Windows' per-app "Show notification banners" switch: the <c>ShowBanner</c> value under
/// <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\&lt;AUMID&gt;</c> (ADR-014). Current user only,
/// no admin rights. Tests pass their own <paramref name="root"/> under HKCU.
/// </summary>
public sealed class NotificationBanners(string root = NotificationBanners.WindowsRoot)
{
    public const string WindowsRoot = @"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings";

    private const string Value = "ShowBanner";

    public int? Get(string appId)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(KeyFor(appId));
        return key?.GetValue(Value) is int value ? value : null;
    }

    /// <summary>Sets the value, or removes it when <paramref name="value"/> is null (Windows' default: shown).</summary>
    public void Set(string appId, int? value)
    {
        if (value is int set)
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(KeyFor(appId));
            key.SetValue(Value, set, RegistryValueKind.DWord);
            return;
        }

        using RegistryKey? existing = Registry.CurrentUser.OpenSubKey(KeyFor(appId), writable: true);
        existing?.DeleteValue(Value, throwOnMissingValue: false);
    }

    private string KeyFor(string appId) =>
        string.IsNullOrWhiteSpace(appId) || appId.Contains("..", StringComparison.Ordinal)
            ? throw new ArgumentException("Not an app id.", nameof(appId))
            : root + @"\" + appId;
}
