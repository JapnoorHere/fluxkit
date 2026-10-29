using FluxKit.Platform.VirtualDesktop;
using FluxKit.Platform.Windows;
using FluxKit.Utilities.DragToSwitch;
using FluxKit.Utilities.DragToSwitch.Interaction;

namespace FluxKit.DragToSwitch.Tests;

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

internal sealed class FakeView : ISelectorView
{
    public int ShowCount { get; private set; }

    public int HideCount { get; private set; }

    public bool Collapsed { get; private set; }

    public bool OverSelector { get; private set; }

    public IReadOnlyList<CardModel> Cards { get; private set; } = [];

    public Dictionary<int, (CardVisualState State, string? Message)> CardStates { get; } = [];

    public DragKind? Kind { get; private set; }

    public void Show(IReadOnlyList<CardModel> cards, SelectorGeometry geometry, DragToSwitchSettings settings, DragKind kind)
    {
        ShowCount++;
        Kind = kind;
        Cards = cards;
        Collapsed = false;
    }

    public void UpdateCards(IReadOnlyList<CardModel> cards, SelectorGeometry geometry) => Cards = cards;

    public void SetCardState(int? index, CardVisualState state, TimeSpan delay, string? message = null)
    {
        if (index is int i)
        {
            CardStates[i] = (state, message);
        }
    }

    public void SetCursorOverSelector(bool over) => OverSelector = over;

    public void Collapse(CardModel destination) => Collapsed = true;

    public void Expand() => Collapsed = false;

    public void Hide() => HideCount++;
}

internal sealed class FakeProbe : IWindowProbe
{
    public string ProcessName { get; set; } = "explorer";

    public string ClassName { get; set; } = "CabinetWClass";

    public bool MoveSize { get; set; }

    public bool FullScreen { get; set; }

    public int DragThreshold => 4;

    public WindowInfo? GetWindowAt(int x, int y) => new(1, ProcessName, ClassName);

    public bool IsForegroundInMoveSizeOrMenu() => MoveSize || MovingWindow != 0;

    /// <summary>Non-zero simulates a title-bar drag of that window (a move, not a resize).</summary>
    public nint MovingWindow { get; set; }

    public nint GetWindowBeingMoved() => MovingWindow;

    /// <summary>False simulates a drag that never starts an OLE drag (text selection, scrollbar).</summary>
    public bool OleDrag { get; set; } = true;

    public bool IsFileDragInProgress(nint sourceWindow) => OleDrag;

    /// <summary>What the payload check reports once it has started; Files unless a test says otherwise.</summary>
    public DragPayload Payload { get; set; } = DragPayload.Files;

    public int PayloadChecksStarted { get; private set; }

    public bool PayloadCheckRunning { get; private set; }

    public DragPayload PayloadCheckResult => PayloadCheckRunning ? Payload : DragPayload.Unknown;

    public void BeginPayloadCheck(int x, int y)
    {
        PayloadChecksStarted++;
        PayloadCheckRunning = true;
    }

    public void MovePayloadCheck(int x, int y)
    {
    }

    public void EndPayloadCheck() => PayloadCheckRunning = false;

    public bool IsFullScreenAppRunning() => FullScreen;

    public (PixelRect WorkArea, double Scale) GetMonitorAt(int x, int y) => (new PixelRect(0, 0, 1920, 1040), 1.0);
}
