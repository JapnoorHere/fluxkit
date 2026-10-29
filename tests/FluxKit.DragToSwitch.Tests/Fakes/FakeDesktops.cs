using FluxKit.Platform.VirtualDesktop;

namespace FluxKit.DragToSwitch.Tests.Fakes;

internal sealed class FakeDesktops : IVirtualDesktopService
{
    private List<VirtualDesktopInfo> _desktops;

    public FakeDesktops(int count)
    {
        _desktops = Enumerable.Range(0, count).Select(i => new VirtualDesktopInfo(Guid.NewGuid(), i, $"Desktop {i + 1}", null, i == 0)).ToList();
    }

    public List<Guid> SwitchRequests { get; } = [];

    public SwitchResult NextResult { get; set; } = new(SwitchOutcome.Success, TimeSpan.FromMilliseconds(12));

    /// <summary>When set, switches stay pending until completed by the test.</summary>
    public TaskCompletionSource<SwitchResult>? Pending { get; set; }

    public VirtualDesktopBackend Backend { get; set; } = VirtualDesktopBackend.ShellCom;

    public string BackendDescription => "fake";

    public bool CanPinWindows => true;

    public IReadOnlyList<VirtualDesktopInfo> Desktops => _desktops;

    public Guid CurrentDesktopId => _desktops.First(d => d.IsCurrent).Id;

    public event EventHandler? DesktopsChanged;

    public Task<SwitchResult> SwitchToAsync(Guid desktopId, CancellationToken cancellationToken = default)
    {
        SwitchRequests.Add(desktopId);
        if (Pending is not null)
        {
            return Pending.Task;
        }

        if (NextResult.Succeeded)
        {
            SetCurrent(desktopId);
        }

        return Task.FromResult(NextResult);
    }

    public bool CanMoveWindows { get; set; } = true;

    /// <summary>Every window move as (window, desktop), in order.</summary>
    public List<(nint Window, Guid Desktop)> WindowMoves { get; } = [];

    public string? NextMoveError { get; set; }

    public Task<string?> MoveWindowAsync(nint hwnd, Guid desktopId)
    {
        if (NextMoveError is null)
        {
            WindowMoves.Add((hwnd, desktopId));
        }

        return Task.FromResult(NextMoveError);
    }

    public async Task<SwitchResult> MoveWindowAndSwitchAsync(nint hwnd, Guid desktopId, CancellationToken cancellationToken = default)
    {
        string? error = await MoveWindowAsync(hwnd, desktopId);
        return error is null
            ? await SwitchToAsync(desktopId, cancellationToken)
            : new SwitchResult(SwitchOutcome.Failed, TimeSpan.Zero, error);
    }

    public void SetCurrent(Guid id) => _desktops = _desktops.Select(d => d with { IsCurrent = d.Id == id }).ToList();

    public void Remove(int index)
    {
        _desktops.RemoveAt(index);
        _desktops = _desktops.Select((d, i) => d with { Index = i }).ToList();
        if (!_desktops.Any(d => d.IsCurrent))
        {
            _desktops[0] = _desktops[0] with { IsCurrent = true };
        }

        DesktopsChanged?.Invoke(this, EventArgs.Empty);
    }

    public Task<bool> PinWindowAsync(nint hwnd) => Task.FromResult(true);

    public Task RefreshAsync() => Task.CompletedTask;

    public Task ReinitializeAsync() => Task.CompletedTask;

    public void Dispose()
    {
    }
}
