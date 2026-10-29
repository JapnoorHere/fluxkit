using FluxKit.Platform.Windows;

namespace FluxKit.Utilities.DragToSwitch.Interaction;

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
