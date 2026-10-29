using FluxKit.Platform.Windows;

namespace FluxKit.Utilities.DragToSwitch.Interaction;

/// <summary>Visual state of a single card.</summary>
public enum CardVisualState
{
    Rest,
    Dwelling,
    Switching,
    Switched,
    Error,
}

/// <summary>What the user is dragging in a session.</summary>
public enum DragKind
{
    /// <summary>A file (OLE drag); FluxKit switches desktops and the user drops into an app there.</summary>
    File,

    /// <summary>An app window dragged by its title bar; FluxKit moves the window along when switching.</summary>
    Window,
}

/// <summary>
/// The selector UI as seen by the coordinator. Implementations marshal to their UI thread and must never block.
/// All geometry is in physical pixels and comes from <see cref="SelectorLayout"/>.
/// </summary>
public interface ISelectorView
{
    /// <summary>Shows the expanded selector; <paramref name="kind"/> picks the wording (file vs. window).</summary>
    void Show(IReadOnlyList<CardModel> cards, SelectorGeometry geometry, DragToSwitchSettings settings, DragKind kind);

    /// <summary>Updates the cards in place (desktops added/removed/renamed during a session).</summary>
    void UpdateCards(IReadOnlyList<CardModel> cards, SelectorGeometry geometry);

    /// <summary>Starts/stops the dwell animation for a card; <paramref name="delay"/> is the full dwell duration.</summary>
    void SetCardState(int? index, CardVisualState state, TimeSpan delay, string? message = null);

    /// <summary>Shows "Release outside the selector to drop" while the cursor is over the selector.</summary>
    void SetCursorOverSelector(bool over);

    void Collapse(CardModel destination);

    void Expand();

    void Hide();
}

/// <summary>Window/system queries the coordinator needs; abstracted for tests.</summary>
public interface IWindowProbe
{
    WindowInfo? GetWindowAt(int x, int y);

    bool IsForegroundInMoveSizeOrMenu();

    /// <summary>The top-level window being dragged by its title bar (a move, not a resize), or 0.</summary>
    nint GetWindowBeingMoved();

    /// <summary>True while a real OLE drag (files, links, images) is running for the window the drag started in.</summary>
    bool IsFileDragInProgress(nint sourceWindow);

    /// <summary>Starts finding out what the running OLE drag carries (see <see cref="DragPayloadProbe"/>).</summary>
    void BeginPayloadCheck(int x, int y);

    /// <summary>Keeps the payload check under the cursor.</summary>
    void MovePayloadCheck(int x, int y);

    /// <summary>What the drag carries, or <see cref="DragPayload.Unknown"/> while the check is still running.</summary>
    DragPayload PayloadCheckResult { get; }

    void EndPayloadCheck();

    bool IsFullScreenAppRunning();

    int DragThreshold { get; }

    (PixelRect WorkArea, double Scale) GetMonitorAt(int x, int y);
}

/// <summary>Production probe backed by Win32.</summary>
public sealed class Win32WindowProbe : IWindowProbe, IDisposable
{
    private readonly DragPayloadProbe _payload = new();

    public int DragThreshold { get; } = Math.Max(4, WindowHelpers.DragThresholdPixels());

    public DragPayload PayloadCheckResult => _payload.Result;

    public void BeginPayloadCheck(int x, int y) => _payload.Begin(x, y);

    public void MovePayloadCheck(int x, int y) => _payload.MoveTo(x, y);

    public void EndPayloadCheck() => _payload.End();

    public void Dispose() => _payload.Dispose();

    public WindowInfo? GetWindowAt(int x, int y) => WindowHelpers.GetWindowAt(x, y);

    public bool IsForegroundInMoveSizeOrMenu() => WindowHelpers.IsForegroundInMoveSizeOrMenu();

    public nint GetWindowBeingMoved() => WindowHelpers.IsResizeCursor() ? 0 : WindowHelpers.GetWindowInMoveSize();

    public bool IsFileDragInProgress(nint sourceWindow) => WindowHelpers.IsOleDragInProgress(sourceWindow);

    public bool IsFullScreenAppRunning() => WindowHelpers.IsFullScreenAppRunning();

    public (PixelRect WorkArea, double Scale) GetMonitorAt(int x, int y) => WindowHelpers.GetMonitorAt(x, y);
}

/// <summary>How a drag session ended. FluxKit never knows whether a drop was delivered.</summary>
public enum SessionEnd
{
    /// <summary>The mouse button was released; the target app (if any) handled the drop.</summary>
    Released,

    /// <summary>The user pressed Esc, which cancels the OLE drag.</summary>
    Cancelled,

    /// <summary>FluxKit ended the session (disabled, desktops changed, timeout, shutdown).</summary>
    Aborted,
}

/// <summary>Summary of the last session for the Troubleshooting panel; contains no file names.</summary>
public sealed record SessionSummary(DateTimeOffset StartedAt, string SourceProcess, int? FromDesktop, int? ToDesktop, int? SwitchMs, SessionEnd End, string? Detail);
