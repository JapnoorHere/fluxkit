using FluxKit.Platform.Shell;
using FluxKit.Tools.FluxIsland.Abstractions;
using Serilog;

namespace FluxKit.Tools.FluxIsland.Sources;

/// <summary>
/// <see cref="IFileMomentsSource"/> over two <see cref="FileSystemWatcher"/>s: the Downloads folder (through a
/// <see cref="DownloadTracker"/>) and Pictures\Screenshots. Windows tells us about changes; nothing polls, and only
/// names and sizes are read.
/// </summary>
internal sealed class FileMomentsSource(bool downloads, bool screenshots, TimeProvider time, ILogger log) : IFileMomentsSource
{
    /// <summary>Screenshots are announced a moment after they appear, once the app saving them has written the file.</summary>
    private static readonly TimeSpan ScreenshotSettle = TimeSpan.FromMilliseconds(350);

    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg"];

    private readonly ILogger _log = log.ForContext<FileMomentsSource>();
    private readonly Lock _sync = new();
    private readonly DownloadTracker _tracker = new();
    private readonly List<FileSystemWatcher> _watchers = [];

    public event EventHandler<DownloadUpdate>? DownloadChanged;

    public event EventHandler<string>? ScreenshotSaved;

    public void Start()
    {
        if (downloads && KnownFolders.Downloads is { } downloadsFolder)
        {
            FileSystemWatcher watcher = Watch(downloadsFolder, NotifyFilters.FileName | NotifyFilters.Size);
            watcher.Created += (_, e) => OnGrew(e.FullPath);
            watcher.Changed += (_, e) => OnGrew(e.FullPath);
            watcher.Renamed += (_, e) => Report(() => _tracker.Renamed(e.OldFullPath, e.FullPath, SizeOf(e.FullPath), time.GetUtcNow()));
            watcher.Deleted += (_, e) => Report(() => _tracker.Deleted(e.FullPath));
        }

        if (screenshots && KnownFolders.Screenshots is { } screenshotsFolder)
        {
            FileSystemWatcher watcher = Watch(screenshotsFolder, NotifyFilters.FileName);
            watcher.Created += (_, e) => OnScreenshot(e.FullPath);
        }
    }

    public void Dispose()
    {
        foreach (FileSystemWatcher watcher in _watchers)
        {
            watcher.Dispose();
        }

        _watchers.Clear();
    }

    private FileSystemWatcher Watch(string folder, NotifyFilters filters)
    {
        var watcher = new FileSystemWatcher(folder) { NotifyFilter = filters, IncludeSubdirectories = false };
        watcher.Error += (_, e) => _log.Warning(e.GetException(), "A Flux Island folder watcher stopped");
        watcher.EnableRaisingEvents = true;
        _watchers.Add(watcher);
        return watcher;
    }

    private void OnGrew(string path) => Report(() => _tracker.Grew(path, SizeOf(path), time.GetUtcNow()));

    private void Report(Func<DownloadUpdate?> step)
    {
        DownloadUpdate? update;
        lock (_sync)
        {
            update = step();
        }

        if (update is { } news)
        {
            DownloadChanged?.Invoke(this, news);
        }
    }

    private void OnScreenshot(string path)
    {
        if (!ImageExtensions.Any(e => path.EndsWith(e, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _ = Task.Delay(ScreenshotSettle, time).ContinueWith(_ => ScreenshotSaved?.Invoke(this, path), TaskScheduler.Default);
    }

    private static long SizeOf(string path)
    {
        try
        {
            return new FileInfo(path).Length;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }
}
