using System.Diagnostics;
using FluxKit.Platform.Input;
using FluxKit.Platform.Threading;
using FluxKit.Platform.VirtualDesktop.Interop;
using Serilog;

namespace FluxKit.Platform.VirtualDesktop;

public enum VirtualDesktopBackend
{
    /// <summary>Undocumented shell COM API, validated against the registry. Fast and precise.</summary>
    ShellCom,

    /// <summary>Registry enumeration + injected Ctrl+Win+Arrow. Works when the COM layout is unknown.</summary>
    Keyboard,

    /// <summary>Virtual desktops could not be read on this system.</summary>
    Unavailable,
}

public enum SwitchOutcome
{
    Success,
    AlreadyCurrent,
    NotFound,
    Failed,
    TimedOut,
}

/// <summary>Result of a switch attempt. <see cref="Message"/> is user-presentable on failure.</summary>
public sealed record SwitchResult(SwitchOutcome Outcome, TimeSpan Elapsed, string? Message = null)
{
    public bool Succeeded => Outcome is SwitchOutcome.Success or SwitchOutcome.AlreadyCurrent;
}

public interface IVirtualDesktopService : IDisposable
{
    VirtualDesktopBackend Backend { get; }

    string BackendDescription { get; }

    bool CanPinWindows { get; }

    IReadOnlyList<VirtualDesktopInfo> Desktops { get; }

    Guid CurrentDesktopId { get; }

    /// <summary>Raised (on a thread-pool thread) after desktops are added, removed, renamed, reordered or switched.</summary>
    event EventHandler? DesktopsChanged;

    Task<SwitchResult> SwitchToAsync(Guid desktopId, CancellationToken cancellationToken = default);

    /// <summary>True when other apps' windows can be moved between desktops (shell COM backend only).</summary>
    bool CanMoveWindows { get; }

    /// <summary>Moves a top-level window to a desktop; returns a user-presentable reason on failure, null on success.</summary>
    Task<string?> MoveWindowAsync(nint hwnd, Guid desktopId);

    /// <summary>
    /// Moves <paramref name="hwnd"/> to the desktop, then switches there, so the window travels with the user.
    /// If the switch fails the window is put back on the desktop it came from.
    /// </summary>
    Task<SwitchResult> MoveWindowAndSwitchAsync(nint hwnd, Guid desktopId, CancellationToken cancellationToken = default);

    Task<bool> PinWindowAsync(nint hwnd);

    Task RefreshAsync();

    /// <summary>Re-probes the backend, e.g. after Explorer restarted.</summary>
    Task ReinitializeAsync();
}

/// <summary>
/// Virtual desktop access with a backend ladder: shell COM → keyboard → unavailable.
/// Enumeration always comes from the registry, which is stable across builds; every switch is
/// verified by re-reading the registry so a switch is never reported without evidence.
/// </summary>
public sealed class VirtualDesktopService : IVirtualDesktopService
{
    private static readonly TimeSpan ComTimeout = TimeSpan.FromMilliseconds(1500);

    private readonly ILogger _log;
    private readonly StaWorker _sta = new("FluxKit.Shell");
    private readonly DesktopChangeWatcher _watcher;
    private readonly SemaphoreSlim _switchGate = new(1, 1);
    private ShellComSession? _session;
    private IReadOnlyList<VirtualDesktopInfo> _desktops = [];

    private VirtualDesktopService(ILogger log)
    {
        _log = log.ForContext<VirtualDesktopService>();
        _watcher = new DesktopChangeWatcher(RegistryDesktopReader.KeyPath, OnRegistryChanged, log);
    }

    public VirtualDesktopBackend Backend { get; private set; } = VirtualDesktopBackend.Unavailable;

    public string BackendDescription { get; private set; } = "Not initialized";

    public bool CanPinWindows => _session?.CanPin == true;

    public IReadOnlyList<VirtualDesktopInfo> Desktops => Volatile.Read(ref _desktops);

    public Guid CurrentDesktopId => Desktops.FirstOrDefault(d => d.IsCurrent)?.Id ?? Guid.Empty;

    public event EventHandler? DesktopsChanged;

    /// <summary>Creates and probes the service. <paramref name="forcedBackend"/> overrides detection (FLUXKIT_VD_BACKEND).</summary>
    public static async Task<VirtualDesktopService> CreateAsync(ILogger log, VirtualDesktopBackend? forcedBackend = null)
    {
        var service = new VirtualDesktopService(log);
        await service.ProbeAsync(forcedBackend).ConfigureAwait(false);
        service._watcher.Start();
        return service;
    }

    public Task RefreshAsync()
    {
        IReadOnlyList<VirtualDesktopInfo> fresh = RegistryDesktopReader.Read();
        IReadOnlyList<VirtualDesktopInfo> old = Interlocked.Exchange(ref _desktops, fresh);
        if (!old.SequenceEqual(fresh))
        {
            _log.Debug("Desktops changed: {Count} desktops, current index {Current}", fresh.Count, fresh.FirstOrDefault(d => d.IsCurrent)?.Index);
            DesktopsChanged?.Invoke(this, EventArgs.Empty);
        }

        return Task.CompletedTask;
    }

    public Task ReinitializeAsync() => ProbeAsync(null);

    public async Task<SwitchResult> SwitchToAsync(Guid desktopId, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        await RefreshAsync().ConfigureAwait(false);
        VirtualDesktopInfo? target = Desktops.FirstOrDefault(d => d.Id == desktopId);
        if (target is null)
        {
            return new SwitchResult(SwitchOutcome.NotFound, sw.Elapsed, "That desktop no longer exists.");
        }

        if (target.IsCurrent)
        {
            return new SwitchResult(SwitchOutcome.AlreadyCurrent, sw.Elapsed);
        }

        await _switchGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            SwitchResult result = Backend switch
            {
                VirtualDesktopBackend.ShellCom => await SwitchViaComAsync(target, sw, cancellationToken).ConfigureAwait(false),
                VirtualDesktopBackend.Keyboard => await SwitchViaKeyboardAsync(target, sw, cancellationToken).ConfigureAwait(false),
                _ => new SwitchResult(SwitchOutcome.Failed, sw.Elapsed, "Virtual desktops are unavailable on this system."),
            };
            _log.Information("Switch to desktop {Index} via {Backend}: {Outcome} in {Ms} ms", target.Index + 1, Backend, result.Outcome, (int)result.Elapsed.TotalMilliseconds);
            await RefreshAsync().ConfigureAwait(false);
            return result;
        }
        finally
        {
            _switchGate.Release();
        }
    }

    public bool CanMoveWindows => Backend == VirtualDesktopBackend.ShellCom && _session?.CanMoveWindows == true;

    public async Task<string?> MoveWindowAsync(nint hwnd, Guid desktopId)
    {
        ShellComSession? session = _session;
        if (session is null || Backend != VirtualDesktopBackend.ShellCom)
        {
            return "Moving windows needs the full shell integration, which isn't available in limited mode.";
        }

        try
        {
            string? error = await _sta.InvokeAsync(() => session.MoveWindow(hwnd, desktopId), ComTimeout).ConfigureAwait(false);
            if (error is not null)
            {
                _log.Warning("Moving a window to another desktop failed: {Reason}", error);
            }

            return error;
        }
        catch (Exception ex) when (ex is ShellComException or TimeoutException)
        {
            _log.Warning(ex, "Moving a window to another desktop failed");
            return "Windows refused to move the window.";
        }
    }

    public async Task<SwitchResult> MoveWindowAndSwitchAsync(nint hwnd, Guid desktopId, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        Guid origin = CurrentDesktopId;
        string? error = await MoveWindowAsync(hwnd, desktopId).ConfigureAwait(false);
        if (error is not null)
        {
            return new SwitchResult(SwitchOutcome.Failed, sw.Elapsed, error);
        }

        SwitchResult result = await SwitchToAsync(desktopId, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded && origin != Guid.Empty)
        {
            _ = await MoveWindowAsync(hwnd, origin).ConfigureAwait(false);
        }

        return result with { Elapsed = sw.Elapsed };
    }

    public async Task<bool> PinWindowAsync(nint hwnd)
    {
        if (_session is null)
        {
            return false;
        }

        try
        {
            return await _sta.InvokeAsync(() => _session?.PinWindow(hwnd) == true, ComTimeout).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ShellComException or TimeoutException)
        {
            _log.Warning(ex, "Pinning window to all desktops failed");
            return false;
        }
    }

    public void Dispose()
    {
        _watcher.Dispose();
        if (_session is not null)
        {
            ShellComSession session = _session;
            _session = null;
            try
            {
                _sta.InvokeAsync(session.Dispose, ComTimeout).Wait();
            }
            catch (AggregateException)
            {
                // Explorer may already be gone during shutdown.
            }
        }

        _sta.Dispose();
        _switchGate.Dispose();
    }

    private async Task ProbeAsync(VirtualDesktopBackend? forced)
    {
        await RefreshAsync().ConfigureAwait(false);
        if (_session is not null)
        {
            ShellComSession old = _session;
            _session = null;
            await _sta.InvokeAsync(old.Dispose, ComTimeout).ConfigureAwait(false);
        }

        if (Desktops.Count == 0)
        {
            SetBackend(VirtualDesktopBackend.Unavailable, "Virtual desktops could not be read on this version of Windows.");
            return;
        }

        if (forced != VirtualDesktopBackend.Keyboard)
        {
            try
            {
                (ShellComSession? session, string diagnostics, bool valid) = await _sta.InvokeAsync(() =>
                {
                    ShellComSession? s = ShellComSession.TryCreate(out string diag);
                    if (s is null)
                    {
                        return (null, diag, false);
                    }

                    bool ok = s.GetDesktopIds().SequenceEqual(Desktops.Select(d => d.Id)) && s.GetCurrentDesktopId() == CurrentDesktopId;
                    if (!ok)
                    {
                        s.Dispose();
                        return ((ShellComSession?)null, diag + "; registry validation failed", false);
                    }

                    return (s, diag, true);
                }, TimeSpan.FromSeconds(3)).ConfigureAwait(false);

                _log.Information("Shell COM probe: {Diagnostics}", diagnostics);
                if (valid && session is not null)
                {
                    _session = session;
                    SetBackend(VirtualDesktopBackend.ShellCom, $"Shell integration (layout {session.Layout.Name})");
                    return;
                }
            }
            catch (Exception ex) when (ex is ShellComException or TimeoutException or InvalidCastException)
            {
                _log.Warning(ex, "Shell COM probe failed");
            }
        }

        SetBackend(VirtualDesktopBackend.Keyboard, "Limited mode: switching with Ctrl+Win+Arrow shortcuts");
    }

    private void SetBackend(VirtualDesktopBackend backend, string description)
    {
        Backend = backend;
        BackendDescription = description;
        _log.Information("Virtual desktop backend: {Backend} ({Description}), {Count} desktops", backend, description, Desktops.Count);
    }

    private async Task<SwitchResult> SwitchViaComAsync(VirtualDesktopInfo target, Stopwatch sw, CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                bool found = await _sta.InvokeAsync(() => _session?.SwitchTo(target.Id) == true, ComTimeout, cancellationToken).ConfigureAwait(false);
                if (!found)
                {
                    return new SwitchResult(SwitchOutcome.NotFound, sw.Elapsed, "That desktop no longer exists.");
                }

                return await VerifyAsync(target.Id, TimeSpan.FromMilliseconds(500), sw, cancellationToken).ConfigureAwait(false);
            }
            catch (ShellComException ex) when (ex.IsDisconnected && attempt == 0)
            {
                _log.Warning("Explorer connection lost; re-initializing shell COM");
                await ProbeAsync(null).ConfigureAwait(false);
                if (Backend != VirtualDesktopBackend.ShellCom)
                {
                    return await SwitchViaKeyboardAsync(target, sw, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (ShellComException ex)
            {
                return new SwitchResult(SwitchOutcome.Failed, sw.Elapsed, $"Windows refused the switch (0x{ex.Code:X8}).");
            }
            catch (TimeoutException)
            {
                return new SwitchResult(SwitchOutcome.TimedOut, sw.Elapsed, "Windows did not respond in time.");
            }
        }

        return new SwitchResult(SwitchOutcome.Failed, sw.Elapsed, "Windows refused the switch.");
    }

    private async Task<SwitchResult> SwitchViaKeyboardAsync(VirtualDesktopInfo target, Stopwatch sw, CancellationToken cancellationToken)
    {
        int currentIndex = Desktops.FirstOrDefault(d => d.IsCurrent)?.Index ?? 0;
        int delta = target.Index - currentIndex;
        ushort arrow = delta > 0 ? InputSynthesizer.VkRight : InputSynthesizer.VkLeft;
        for (int i = 0; i < Math.Abs(delta); i++)
        {
            InputSynthesizer.KeyCombo(InputSynthesizer.FluxKitSignature, InputSynthesizer.VkControl, InputSynthesizer.VkLeftWindows, arrow);
            await Task.Delay(80, cancellationToken).ConfigureAwait(false);
        }

        return await VerifyAsync(target.Id, TimeSpan.FromMilliseconds(1000), sw, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<SwitchResult> VerifyAsync(Guid target, TimeSpan timeout, Stopwatch sw, CancellationToken cancellationToken)
    {
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < timeout)
        {
            if (RegistryDesktopReader.ReadCurrentId() == target)
            {
                return new SwitchResult(SwitchOutcome.Success, sw.Elapsed);
            }

            await Task.Delay(10, cancellationToken).ConfigureAwait(false);
        }

        return new SwitchResult(SwitchOutcome.TimedOut, sw.Elapsed, "Windows did not confirm the switch.");
    }

    private void OnRegistryChanged() => _ = RefreshAsync();
}
