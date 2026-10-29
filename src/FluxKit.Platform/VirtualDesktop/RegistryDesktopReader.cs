using Microsoft.Win32;

namespace FluxKit.Platform.VirtualDesktop;

/// <summary>
/// Reads the virtual desktop list from the registry that Explorer maintains.
/// This is read-only, works across Windows 11 builds, and needs no COM.
/// </summary>
public static class RegistryDesktopReader
{
    public const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops";

    public static IReadOnlyList<VirtualDesktopInfo> Read()
    {
        using RegistryKey? root = Registry.CurrentUser.OpenSubKey(KeyPath);
        if (root is null)
        {
            return [];
        }

        IReadOnlyList<Guid> ids = ParseGuids(root.GetValue("VirtualDesktopIDs") as byte[]);
        Guid current = ReadCurrent(root);

        var result = new List<VirtualDesktopInfo>(ids.Count);
        for (int i = 0; i < ids.Count; i++)
        {
            Guid id = ids[i];
            string? name = null;
            string? wallpaper = null;
            using (RegistryKey? desktopKey = root.OpenSubKey($@"Desktops\{id:B}"))
            {
                name = desktopKey?.GetValue("Name") as string;
                wallpaper = desktopKey?.GetValue("Wallpaper") as string;
            }

            result.Add(new VirtualDesktopInfo(
                id,
                i,
                string.IsNullOrWhiteSpace(name) ? VirtualDesktopInfo.DefaultName(i) : name,
                string.IsNullOrWhiteSpace(wallpaper) ? null : wallpaper,
                id == current));
        }

        return result;
    }

    public static Guid ReadCurrentId()
    {
        using RegistryKey? root = Registry.CurrentUser.OpenSubKey(KeyPath);
        return root is null ? Guid.Empty : ReadCurrent(root);
    }

    /// <summary>Splits the concatenated 16-byte GUID blob Explorer stores.</summary>
    public static IReadOnlyList<Guid> ParseGuids(byte[]? blob)
    {
        if (blob is null || blob.Length < 16)
        {
            return [];
        }

        var ids = new List<Guid>(blob.Length / 16);
        for (int offset = 0; offset + 16 <= blob.Length; offset += 16)
        {
            ids.Add(new Guid(blob.AsSpan(offset, 16)));
        }

        return ids;
    }

    private static Guid ReadCurrent(RegistryKey root)
    {
        if (root.GetValue("CurrentVirtualDesktop") is byte[] { Length: 16 } bytes)
        {
            return new Guid(bytes);
        }

        // Some builds only keep the current desktop in the per-session key.
        int sessionId = System.Diagnostics.Process.GetCurrentProcess().SessionId;
        using RegistryKey? session = Registry.CurrentUser.OpenSubKey(
            $@"Software\Microsoft\Windows\CurrentVersion\Explorer\SessionInfo\{sessionId}\VirtualDesktops");
        return session?.GetValue("CurrentVirtualDesktop") is byte[] { Length: 16 } sessionBytes
            ? new Guid(sessionBytes)
            : Guid.Empty;
    }
}
