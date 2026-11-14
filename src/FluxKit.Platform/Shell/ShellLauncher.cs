using System.Diagnostics;

namespace FluxKit.Platform.Shell;

/// <summary>Hands a file to Windows: opens it in its default app, or shows it selected in File Explorer.</summary>
public static class ShellLauncher
{
    /// <summary>Opens a file in its default app, or a folder in File Explorer. Returns false if it is gone or nothing opened.</summary>
    public static bool Open(string path) => (File.Exists(path) || Directory.Exists(path)) && Start(new ProcessStartInfo(path) { UseShellExecute = true });

    /// <summary>Opens a web page in the default browser or a mailto link in the mail app. Any other kind of link is refused.</summary>
    public static bool OpenLink(string link) =>
        Uri.TryCreate(link, UriKind.Absolute, out Uri? uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeMailto)
        && Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });

    /// <summary>Opens one of Windows' own fixed links, such as <c>ms-screenclip:</c>. Only FluxKit passes these, never copied text.</summary>
    internal static bool OpenSystemLink(string link) =>
        link.StartsWith("ms-", StringComparison.Ordinal) && Start(new ProcessStartInfo(link) { UseShellExecute = true });

    /// <summary>Opens File Explorer with <paramref name="path"/> selected, or just its folder if the file is gone.</summary>
    public static bool ShowInFolder(string path)
    {
        if (File.Exists(path))
        {
            return Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
        }

        string? folder = Path.GetDirectoryName(path);
        return Directory.Exists(folder) && Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
    }

    private static bool Start(ProcessStartInfo info)
    {
        try
        {
            using Process? process = Process.Start(info);
            return true;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }
}
