using FluxKit.Platform.Watchers;
using FluxKit.Tools.FluxIsland.Abstractions;
using Serilog;

namespace FluxKit.Tools.FluxIsland.Sources;

/// <summary><see cref="ILiveActivitySource"/> backed by the Platform <see cref="LiveActivityServer"/> pipe.</summary>
internal sealed class LiveActivitySource : ILiveActivitySource
{
    private readonly LiveActivityServer _server;

    public LiveActivitySource(ILogger log)
    {
        _server = new LiveActivityServer(log);
        _server.LineReceived += (_, line) => LineReceived?.Invoke(this, line);
    }

    public event EventHandler<string>? LineReceived;

    public void Start() => _server.Start();

    public void Dispose() => _server.Dispose();
}
