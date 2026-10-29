using System.Runtime.InteropServices;

namespace FluxKit.Platform.VirtualDesktop.Interop;

/// <summary>The documented IVirtualDesktopManager (shobjidl_core.h). Stable across Windows versions.</summary>
[ComImport]
[Guid("A5CD92FF-29BE-454C-8D04-D82879FB3F1B")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IVirtualDesktopManager
{
    [return: MarshalAs(UnmanagedType.Bool)]
    bool IsWindowOnCurrentVirtualDesktop(nint topLevelWindow);

    Guid GetWindowDesktopId(nint topLevelWindow);

    void MoveWindowToDesktop(nint topLevelWindow, in Guid desktopId);
}

[ComImport]
[Guid("AA509086-5CA9-4C25-8F95-589D3C07B48A")]
internal class VirtualDesktopManagerClass;

/// <summary>Safe wrapper over the documented virtual desktop API.</summary>
public static class PublicVirtualDesktopManager
{
    /// <summary>Returns the desktop that owns <paramref name="hwnd"/>, or null if it cannot be determined.</summary>
    public static Guid? GetWindowDesktopId(nint hwnd)
    {
        try
        {
            var manager = (IVirtualDesktopManager)new VirtualDesktopManagerClass();
            Guid id = manager.GetWindowDesktopId(hwnd);
            return id == Guid.Empty ? null : id;
        }
        catch (COMException)
        {
            return null;
        }
    }

    /// <summary>Moves a window owned by the calling process to another desktop.</summary>
    public static bool MoveOwnWindowToDesktop(nint hwnd, Guid desktopId)
    {
        try
        {
            var manager = (IVirtualDesktopManager)new VirtualDesktopManagerClass();
            manager.MoveWindowToDesktop(hwnd, desktopId);
            return true;
        }
        catch (Exception ex) when (ex is COMException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }
}
