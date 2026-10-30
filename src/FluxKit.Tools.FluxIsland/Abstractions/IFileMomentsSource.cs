namespace FluxKit.Tools.FluxIsland.Abstractions;

/// <summary>
/// New files worth a moment: downloads (while they arrive and when they finish) and screenshots. Only names and sizes
/// are looked at, never what is inside. Events may arrive on any thread.
/// </summary>
public interface IFileMomentsSource : IIslandSource
{
    event EventHandler<DownloadUpdate>? DownloadChanged;

    /// <summary>A screenshot was saved. Carries its full path.</summary>
    event EventHandler<string>? ScreenshotSaved;
}

public enum DownloadStage
{
    Arriving,
    Finished,
    Cancelled,
}

/// <summary>
/// One download's news. <paramref name="Key"/> tells downloads apart; <paramref name="Name"/> is the file name it will
/// have (empty while the browser hasn't said), and <paramref name="Path"/> is set once it has finished.
/// <paramref name="Previous"/> is the key this download had before the browser renamed its temporary file.
/// </summary>
public readonly record struct DownloadUpdate(string Key, DownloadStage Stage, string Name, long Bytes, string? Path = null, string? Previous = null);
