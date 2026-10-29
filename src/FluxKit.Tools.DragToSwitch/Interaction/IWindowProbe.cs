using FluxKit.Platform.Windows;

namespace FluxKit.Tools.DragToSwitch.Interaction;

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
