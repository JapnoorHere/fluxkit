namespace FluxKit.Tools.DragToSwitch.Interaction;

/// <summary>What the user is dragging in a session.</summary>
public enum DragKind
{
    /// <summary>A file (OLE drag); FluxKit switches desktops and the user drops into an app there.</summary>
    File,

    /// <summary>An app window dragged by its title bar; FluxKit moves the window along when switching.</summary>
    Window,
}
