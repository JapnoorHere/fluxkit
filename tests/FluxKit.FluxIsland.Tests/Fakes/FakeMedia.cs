using FluxKit.Tools.FluxIsland.Abstractions;

namespace FluxKit.FluxIsland.Tests.Fakes;

internal sealed class FakeMedia : IMediaSource
{
    public MediaInfo? Current { get; private set; }

    public int PlayPauseCalls { get; private set; }

    public event EventHandler? Changed;

    public void Set(MediaInfo? media)
    {
        Current = media;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Start()
    {
    }

    public Task PlayPauseAsync()
    {
        PlayPauseCalls++;
        return Task.CompletedTask;
    }

    public Task NextAsync() => Task.CompletedTask;

    public Task PreviousAsync() => Task.CompletedTask;

    public Task SeekAsync(TimeSpan position) => Task.CompletedTask;

    public void Dispose()
    {
    }
}
