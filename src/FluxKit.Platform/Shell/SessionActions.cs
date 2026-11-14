using System.Runtime.InteropServices;

namespace FluxKit.Platform.Shell;

/// <summary>Quick things the island can do for the user: lock the PC, turn the screens off, take a screenshot.</summary>
public static partial class SessionActions
{
    private const uint WmSysCommand = 0x0112;
    private const nint ScMonitorPower = 0xF170;
    private const nint MonitorOff = 2;
    private static readonly nint HwndBroadcast = 0xFFFF;

    /// <summary>Locks the PC, the same as Win+L.</summary>
    public static bool Lock() => LockWorkStation();

    /// <summary>Turns the screens off until the mouse or keyboard is used. The PC keeps running.</summary>
    public static bool SleepDisplays() => PostMessage(HwndBroadcast, WmSysCommand, ScMonitorPower, MonitorOff);

    /// <summary>Opens the Snipping Tool's capture bar, the same as Win+Shift+S.</summary>
    public static bool StartScreenshot() => ShellLauncher.OpenSystemLink("ms-screenclip:");

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool LockWorkStation();

    [LibraryImport("user32.dll", EntryPoint = "PostMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PostMessage(nint hwnd, uint msg, nint wParam, nint lParam);
}
