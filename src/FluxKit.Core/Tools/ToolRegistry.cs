using Serilog;

namespace FluxKit.Core.Tools;

/// <summary>
/// Holds every registered tool in display order and owns their start/stop lifecycle.
/// The shell builds its navigation, home cards and tray toggles from <see cref="All"/>.
/// </summary>
public sealed class ToolRegistry : IAsyncDisposable
{
    private readonly List<IFluxTool> _tools = [];
    private readonly Func<string, bool> _isEnabled;
    private readonly Func<string, bool, Task> _persistEnabled;
    private readonly ILogger _log;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <param name="isEnabled">Reads the persisted enabled flag for a tool id.</param>
    /// <param name="persistEnabled">Persists a new enabled flag for a tool id.</param>
    public ToolRegistry(Func<string, bool> isEnabled, Func<string, bool, Task> persistEnabled, ILogger log)
    {
        _isEnabled = isEnabled;
        _persistEnabled = persistEnabled;
        _log = log.ForContext<ToolRegistry>();
    }

    public IReadOnlyList<IFluxTool> All => _tools;

    public void Register(IFluxTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        if (_tools.Any(u => u.Descriptor.Id == tool.Descriptor.Id))
        {
            throw new InvalidOperationException($"A tool with id '{tool.Descriptor.Id}' is already registered.");
        }

        _tools.Add(tool);
        _tools.Sort((a, b) => a.Descriptor.Order.CompareTo(b.Descriptor.Order));
    }

    public IFluxTool? Get(string id) => _tools.FirstOrDefault(u => u.Descriptor.Id == id);

    public bool IsEnabled(string id) => _isEnabled(id);

    /// <summary>Starts every tool whose persisted setting is enabled.</summary>
    public async Task StartEnabledAsync(CancellationToken cancellationToken)
    {
        foreach (IFluxTool tool in _tools.Where(u => _isEnabled(u.Descriptor.Id)))
        {
            await StartSafeAsync(tool, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Persists the flag, then starts or stops the tool exactly once.</summary>
    public async Task SetEnabledAsync(string id, bool enabled, CancellationToken cancellationToken = default)
    {
        IFluxTool tool = Get(id) ?? throw new KeyNotFoundException($"Unknown tool '{id}'.");
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _persistEnabled(id, enabled).ConfigureAwait(false);
            if (enabled && !tool.State.IsActive)
            {
                await StartSafeAsync(tool, cancellationToken).ConfigureAwait(false);
            }
            else if (!enabled && tool.State.Status != ToolStatus.Disabled)
            {
                await StopSafeAsync(tool, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Stops all tools in reverse order with a per-tool timeout.</summary>
    public async Task StopAllAsync(TimeSpan perToolTimeout)
    {
        for (int i = _tools.Count - 1; i >= 0; i--)
        {
            using var cts = new CancellationTokenSource(perToolTimeout);
            await StopSafeAsync(_tools[i], cts.Token).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAllAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        for (int i = _tools.Count - 1; i >= 0; i--)
        {
            try
            {
                await _tools[i].DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Disposing tool {Tool} failed", _tools[i].Descriptor.Id);
            }
        }

        _gate.Dispose();
    }

    private async Task StartSafeAsync(IFluxTool tool, CancellationToken cancellationToken)
    {
        try
        {
            _log.Information("Starting tool {Tool}", tool.Descriptor.Id);
            await tool.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A faulty tool must never take the app down; it reports Faulted itself where it can.
            _log.Error(ex, "Tool {Tool} failed to start", tool.Descriptor.Id);
        }
    }

    private async Task StopSafeAsync(IFluxTool tool, CancellationToken cancellationToken)
    {
        try
        {
            _log.Information("Stopping tool {Tool}", tool.Descriptor.Id);
            await tool.StopAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Tool {Tool} failed to stop cleanly", tool.Descriptor.Id);
        }
    }
}
