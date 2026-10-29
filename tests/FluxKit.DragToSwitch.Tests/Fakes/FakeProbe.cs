using FluxKit.Platform.Windows;
using FluxKit.Tools.DragToSwitch.Interaction;

namespace FluxKit.DragToSwitch.Tests.Fakes;

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
