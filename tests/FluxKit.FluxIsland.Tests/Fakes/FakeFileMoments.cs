using FluxKit.Tools.FluxIsland.Abstractions;

namespace FluxKit.FluxIsland.Tests.Fakes;

internal sealed class FakeFileMoments : IFileMomentsSource
{
    public event EventHandler<DownloadUpdate>? DownloadChanged;

    public event EventHandler<string>? ScreenshotSaved;

    public void Download(DownloadUpdate update) => DownloadChanged?.Invoke(this, update);

    public void Screenshot(string path) => ScreenshotSaved?.Invoke(this, path);

    public void Start()
    {
    }

    public void Dispose()
    {
    }
}
