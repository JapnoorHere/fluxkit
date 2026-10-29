using Serilog;

namespace FluxKit.Core.Utilities;

/// <summary>
/// Holds every registered utility in display order and owns their start/stop lifecycle.
/// The shell builds its navigation, home cards and tray toggles from <see cref="All"/>.
/// </summary>
public sealed class UtilityRegistry : IAsyncDisposable
{
    private readonly List<IFluxUtility> _utilities = [];
    private readonly Func<string, bool> _isEnabled;
    private readonly Func<string, bool, Task> _persistEnabled;
    private readonly ILogger _log;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <param name="isEnabled">Reads the persisted enabled flag for a utility id.</param>
    /// <param name="persistEnabled">Persists a new enabled flag for a utility id.</param>
    public UtilityRegistry(Func<string, bool> isEnabled, Func<string, bool, Task> persistEnabled, ILogger log)
    {
        _isEnabled = isEnabled;
        _persistEnabled = persistEnabled;
        _log = log.ForContext<UtilityRegistry>();
    }

    public IReadOnlyList<IFluxUtility> All => _utilities;

    public void Register(IFluxUtility utility)
    {
        ArgumentNullException.ThrowIfNull(utility);
        if (_utilities.Any(u => u.Descriptor.Id == utility.Descriptor.Id))
        {
            throw new InvalidOperationException($"A utility with id '{utility.Descriptor.Id}' is already registered.");
        }

        _utilities.Add(utility);
        _utilities.Sort((a, b) => a.Descriptor.Order.CompareTo(b.Descriptor.Order));
    }

    public IFluxUtility? Get(string id) => _utilities.FirstOrDefault(u => u.Descriptor.Id == id);

    public bool IsEnabled(string id) => _isEnabled(id);

    /// <summary>Starts every utility whose persisted setting is enabled.</summary>
    public async Task StartEnabledAsync(CancellationToken cancellationToken)
    {
        foreach (IFluxUtility utility in _utilities.Where(u => _isEnabled(u.Descriptor.Id)))
        {
            await StartSafeAsync(utility, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Persists the flag, then starts or stops the utility exactly once.</summary>
    public async Task SetEnabledAsync(string id, bool enabled, CancellationToken cancellationToken = default)
    {
        IFluxUtility utility = Get(id) ?? throw new KeyNotFoundException($"Unknown utility '{id}'.");
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _persistEnabled(id, enabled).ConfigureAwait(false);
            if (enabled && !utility.State.IsActive)
            {
                await StartSafeAsync(utility, cancellationToken).ConfigureAwait(false);
            }
            else if (!enabled && utility.State.Status != UtilityStatus.Disabled)
            {
                await StopSafeAsync(utility, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Stops all utilities in reverse order with a per-utility timeout.</summary>
    public async Task StopAllAsync(TimeSpan perUtilityTimeout)
    {
        for (int i = _utilities.Count - 1; i >= 0; i--)
        {
            using var cts = new CancellationTokenSource(perUtilityTimeout);
            await StopSafeAsync(_utilities[i], cts.Token).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAllAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        for (int i = _utilities.Count - 1; i >= 0; i--)
        {
            try
            {
                await _utilities[i].DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Disposing utility {Utility} failed", _utilities[i].Descriptor.Id);
            }
        }

        _gate.Dispose();
    }

    private async Task StartSafeAsync(IFluxUtility utility, CancellationToken cancellationToken)
    {
        try
        {
            _log.Information("Starting utility {Utility}", utility.Descriptor.Id);
            await utility.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A faulty utility must never take the app down; it reports Faulted itself where it can.
            _log.Error(ex, "Utility {Utility} failed to start", utility.Descriptor.Id);
        }
    }

    private async Task StopSafeAsync(IFluxUtility utility, CancellationToken cancellationToken)
    {
        try
        {
            _log.Information("Stopping utility {Utility}", utility.Descriptor.Id);
            await utility.StopAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Utility {Utility} failed to stop cleanly", utility.Descriptor.Id);
        }
    }
}
