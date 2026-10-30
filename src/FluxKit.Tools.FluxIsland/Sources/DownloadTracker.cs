using FluxKit.Tools.FluxIsland.Abstractions;

namespace FluxKit.Tools.FluxIsland.Sources;

/// <summary>
/// Turns file events in the Downloads folder into download news. Browsers write into a temporary file
/// ("report.pdf.crdownload", "report.pdf.part") and rename it when done, so a rename from a partial name to a real one
/// means finished, and deleting a partial file means cancelled. Plain files that simply appear are not downloads we can
/// vouch for, so they are left alone. Not thread-safe; the owner serialises calls.
/// </summary>
internal sealed class DownloadTracker
{
    /// <summary>Progress is reported at most this often per download.</summary>
    public static readonly TimeSpan ReportEvery = TimeSpan.FromSeconds(1);

    private static readonly string[] PartialExtensions = [".crdownload", ".part", ".partial", ".download", ".opdownload"];

    private readonly Dictionary<string, DateTimeOffset> _arriving = new(StringComparer.OrdinalIgnoreCase);

    public int ArrivingCount => _arriving.Count;

    public static bool IsPartial(string path) =>
        PartialExtensions.Any(e => path.EndsWith(e, StringComparison.OrdinalIgnoreCase));

    /// <summary>The name a partial file will have when done, or empty when the browser hasn't decided yet ("Unconfirmed 123.crdownload").</summary>
    public static string FinalName(string partialPath)
    {
        string name = Path.GetFileName(partialPath);
        string? extension = PartialExtensions.FirstOrDefault(e => name.EndsWith(e, StringComparison.OrdinalIgnoreCase));
        if (extension is not null)
        {
            name = name[..^extension.Length];
        }

        return name.StartsWith("Unconfirmed ", StringComparison.OrdinalIgnoreCase) ? string.Empty : name;
    }

    /// <summary>A file was created or grew.</summary>
    public DownloadUpdate? Grew(string path, long bytes, DateTimeOffset now)
    {
        if (!IsPartial(path) || IsHidden(path))
        {
            return null;
        }

        if (_arriving.TryGetValue(path, out DateTimeOffset last) && now - last < ReportEvery)
        {
            return null;
        }

        _arriving[path] = now;
        return new DownloadUpdate(path, DownloadStage.Arriving, FinalName(path), bytes);
    }

    public DownloadUpdate? Renamed(string oldPath, string newPath, long bytes, DateTimeOffset now)
    {
        if (!IsPartial(oldPath))
        {
            return null;
        }

        _arriving.Remove(oldPath);
        if (IsPartial(newPath))
        {
            // The browser picked the real name ("Unconfirmed 123.crdownload" → "report.pdf.crdownload"), so say it now.
            _arriving[newPath] = now;
            return new DownloadUpdate(newPath, DownloadStage.Arriving, FinalName(newPath), bytes, Previous: oldPath);
        }

        return new DownloadUpdate(oldPath, DownloadStage.Finished, Path.GetFileName(newPath), bytes, newPath);
    }

    public DownloadUpdate? Deleted(string path) =>
        _arriving.Remove(path) ? new DownloadUpdate(path, DownloadStage.Cancelled, FinalName(path), 0) : null;

    private static bool IsHidden(string path)
    {
        string name = Path.GetFileName(path);
        return name.StartsWith('.') || name.StartsWith("~$", StringComparison.Ordinal);
    }
}
