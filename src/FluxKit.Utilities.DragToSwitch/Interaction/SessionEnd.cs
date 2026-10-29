namespace FluxKit.Utilities.DragToSwitch.Interaction;

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
