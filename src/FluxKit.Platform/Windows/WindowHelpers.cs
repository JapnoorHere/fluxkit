using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using FluxKit.Platform.Native;
using static FluxKit.Platform.Native.User32;

namespace FluxKit.Platform.Windows;

/// <summary>Win32 helpers for overlay windows, monitors and foreground state.</summary>
public static partial class WindowHelpers
{
    private const int GwlExStyle = -20;
    private const int GwlStyle = -16;
    private const long WsCaption = 0x00C00000;
    private const long WsExTopmost = 0x8;
    private const long WsExToolWindow = 0x80;
    private const long WsExAppWindow = 0x40000;
    private const long WsExNoActivate = 0x08000000;
    private const long WsExLayered = 0x80000;
    private const long WsExTransparent = 0x20;
    private const uint LwaAlpha = 0x2;
    private const uint SwpNoSize = 0x1;
    private const uint SwpNoMove = 0x2;
    private const uint SwpNoActivate = 0x10;
    private const uint SwpShowWindow = 0x40;
    private const int SwHide = 0;
    private const int SwShowNoActivate = 4;
    private const uint MonitorDefaultToNearest = 2;
    private const uint MonitorDefaultToPrimary = 1;
    private const uint GaRoot = 2;
    private const uint GuiInMoveSize = 0x2;
    private const uint GuiInMenuMode = 0x4;
    private static readonly nint HwndTopmost = -1;

    /// <summary>Makes a window a non-activating, topmost tool window that never appears in Alt+Tab or the taskbar.</summary>
    public static void MakeOverlay(nint hwnd)
    {
        long style = GetWindowLongPtr(hwnd, GwlExStyle);
        style = (style | WsExNoActivate | WsExToolWindow | WsExTopmost) & ~WsExAppWindow;
        _ = SetWindowLongPtr(hwnd, GwlExStyle, (nint)style);
        RemoveBorder(hwnd);
    }

    /// <summary>
    /// Makes an overlay ignore the mouse completely: every click and hover goes to the windows under it. For purely
    /// decorative windows, such as the island's light halo.
    /// </summary>
    public static void MakeClickThrough(nint hwnd)
    {
        long style = GetWindowLongPtr(hwnd, GwlExStyle);
        _ = SetWindowLongPtr(hwnd, GwlExStyle, (nint)(style | WsExLayered | WsExTransparent));
        _ = SetLayeredWindowAttributes(hwnd, 0, 255, LwaAlpha);
    }

    /// <summary>Shows and positions a window without activating it, just under <paramref name="above"/> in the z-order.</summary>
    public static void ShowBelow(nint hwnd, nint above, PixelRect bounds) =>
        _ = SetWindowPos(hwnd, above, bounds.Left, bounds.Top, bounds.Width, bounds.Height, SwpNoActivate | SwpShowWindow);

    /// <summary>
    /// Turns off Windows 11's own corner rounding and 1 px window outline for an overlay that shapes itself with a
    /// region: otherwise the outline shows as thin lines at the window's edges. Safe to call again whenever the window
    /// is shown or moved.
    /// </summary>
    public static void RemoveBorder(nint hwnd)
    {
        int square = 1; // DWMWCP_DONOTROUND: the region does the rounding.
        _ = DwmSetWindowAttribute(hwnd, 33, ref square, sizeof(int));
        int noBorder = unchecked((int)0xFFFFFFFE); // DWMWA_COLOR_NONE
        _ = DwmSetWindowAttribute(hwnd, 34, ref noBorder, sizeof(int)); // DWMWA_BORDER_COLOR
    }

    /// <summary>Shows and positions a window without activating it.</summary>
    public static void ShowNoActivate(nint hwnd, PixelRect bounds)
    {
        _ = SetWindowPos(hwnd, HwndTopmost, bounds.Left, bounds.Top, bounds.Width, bounds.Height, SwpNoActivate | SwpShowWindow);
        _ = ShowWindow(hwnd, SwShowNoActivate);
    }

    /// <summary>Re-asserts topmost z-order (needed after a desktop switch).</summary>
    public static void BringToTopNoActivate(nint hwnd) =>
        _ = SetWindowPos(hwnd, HwndTopmost, 0, 0, 0, 0, SwpNoActivate | SwpNoMove | SwpNoSize | SwpShowWindow);

    public static void Hide(nint hwnd) => _ = ShowWindow(hwnd, SwHide);

    /// <summary>Gives keyboard focus to one of FluxKit's own windows, e.g. when it is opened from the tray.</summary>
    public static void BringToForeground(nint hwnd) => _ = SetForegroundWindow(hwnd);

    /// <summary>
    /// Lets an overlay take keyboard focus (and gives it focus), or makes it non-activating again. Used while the
    /// island's panel is opened from the keyboard. Returns the window that had focus before, to hand it back later.
    /// </summary>
    public static nint TakeFocus(nint hwnd)
    {
        nint previous = GetForegroundWindow();
        long style = GetWindowLongPtr(hwnd, GwlExStyle);
        _ = SetWindowLongPtr(hwnd, GwlExStyle, (nint)(style & ~WsExNoActivate));
        _ = SetForegroundWindow(hwnd);
        return previous == hwnd ? 0 : previous;
    }

    /// <summary>Makes an overlay non-activating again and gives focus back to <paramref name="previous"/>, if it still exists.</summary>
    public static void ReleaseFocus(nint hwnd, nint previous)
    {
        long style = GetWindowLongPtr(hwnd, GwlExStyle);
        _ = SetWindowLongPtr(hwnd, GwlExStyle, (nint)(style | WsExNoActivate));
        if (previous != 0 && IsWindow(previous) && GetForegroundWindow() == hwnd)
        {
            _ = SetForegroundWindow(previous);
        }
    }

    /// <summary>The window's DPI (96 = 100 % scaling).</summary>
    public static uint GetDpi(nint hwnd) => GetDpiForWindow(hwnd);

    public static (int X, int Y) GetCursorPosition()
    {
        _ = GetCursorPos(out POINT p);
        return (p.X, p.Y);
    }

    /// <summary>Work area (excluding taskbar) and DPI scale of the monitor nearest a point.</summary>
    public static (PixelRect WorkArea, double Scale) GetMonitorAt(int x, int y)
    {
        nint monitor = MonitorFromPoint(new POINT { X = x, Y = y }, MonitorDefaultToNearest);
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        _ = GetMonitorInfo(monitor, ref info);
        double scale = GetDpiForMonitor(monitor, 0, out uint dpiX, out _) == 0 ? dpiX / 96.0 : 1.0;
        return (new PixelRect(info.rcWork.Left, info.rcWork.Top, info.rcWork.Right, info.rcWork.Bottom), scale);
    }

    public static WindowInfo? GetWindowAt(int x, int y)
    {
        nint hwnd = WindowFromPoint(new POINT { X = x, Y = y });
        if (hwnd == 0)
        {
            return null;
        }

        nint root = GetAncestor(hwnd, GaRoot);
        _ = GetWindowThreadProcessId(root, out uint pid);
        string className = ReadClassName(root);
        string processName;
        try
        {
            using var process = Process.GetProcessById((int)pid);
            processName = process.ProcessName;
        }
        catch (ArgumentException)
        {
            processName = string.Empty;
        }

        return new WindowInfo(root, processName, className);
    }

    private static unsafe string ReadClassName(nint hwnd)
    {
        char* buffer = stackalloc char[256];
        int len = GetClassName(hwnd, buffer, 256);
        return new string(buffer, 0, Math.Max(0, len));
    }

    /// <summary>True when the foreground thread is moving/resizing a window or tracking a menu.</summary>
    public static bool IsForegroundInMoveSizeOrMenu()
    {
        var info = new GUITHREADINFO { cbSize = Marshal.SizeOf<GUITHREADINFO>() };
        return GetGUIThreadInfo(0, ref info) && (info.flags & (GuiInMoveSize | GuiInMenuMode)) != 0;
    }

    /// <summary>
    /// The top-level window the foreground thread is moving or resizing (title-bar drag), or 0 when none.
    /// Use <see cref="IsResizeCursor"/> to tell a resize from a move.
    /// </summary>
    public static nint GetWindowInMoveSize()
    {
        var info = new GUITHREADINFO { cbSize = Marshal.SizeOf<GUITHREADINFO>() };
        if (!GetGUIThreadInfo(0, ref info) || (info.flags & GuiInMoveSize) == 0 || info.hwndMoveSize == 0)
        {
            return 0;
        }

        return GetAncestor(info.hwndMoveSize, GaRoot);
    }

    /// <summary>
    /// True while an OLE drag-and-drop (<c>DoDragDrop</c>) is running on the thread that owns
    /// <paramref name="sourceWindow"/> or on the foreground thread. During <c>DoDragDrop</c> OLE gives mouse capture
    /// to its own <c>CLIPBRDWNDCLASS</c> window (verified on build 26300); text selection, scrollbar drags and
    /// rubber-band selection keep capture in the app's own window.
    /// </summary>
    public static bool IsOleDragInProgress(nint sourceWindow)
    {
        uint sourceThread = sourceWindow == 0 ? 0 : GetWindowThreadProcessId(sourceWindow, out _);
        return (sourceThread != 0 && CaptureIsOleTracker(sourceThread)) || CaptureIsOleTracker(0);
    }

    private static bool CaptureIsOleTracker(uint threadId)
    {
        var info = new GUITHREADINFO { cbSize = Marshal.SizeOf<GUITHREADINFO>() };
        return GetGUIThreadInfo(threadId, ref info) && info.hwndCapture != 0 && ReadClassName(info.hwndCapture) == "CLIPBRDWNDCLASS";
    }

    /// <summary>True while the cursor is one of the system resize arrows (a window edge is being dragged).</summary>
    public static bool IsResizeCursor()
    {
        var info = new CURSORINFO { cbSize = Marshal.SizeOf<CURSORINFO>() };
        return GetCursorInfo(ref info) && Array.IndexOf(ResizeCursors.Value, info.hCursor) >= 0;
    }

    // IDC_SIZENWSE, IDC_SIZENESW, IDC_SIZEWE, IDC_SIZENS: shared system cursors, so the handles are stable.
    private static readonly Lazy<nint[]> ResizeCursors = new(() => [LoadCursor(0, 32642), LoadCursor(0, 32643), LoadCursor(0, 32644), LoadCursor(0, 32645)]);

    /// <summary>True while a full-screen game or presentation is running (selector should stay hidden).</summary>
    public static bool IsFullScreenAppRunning() =>
        SHQueryUserNotificationState(out int state) == 0 && state is 3 or 4;

    /// <summary>
    /// True when <paramref name="hwnd"/> covers the whole primary monitor without a caption: a game, a video or a
    /// browser in full screen. A maximized window keeps its caption, so it does not count even with an auto-hide taskbar.
    /// </summary>
    public static bool IsFullScreenOnPrimary(nint hwnd)
    {
        if (hwnd == 0 || ReadClassName(hwnd) is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd")
        {
            return false;
        }

        nint monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (monitor != MonitorFromPoint(default, MonitorDefaultToPrimary) || !GetWindowRect(hwnd, out RECT window))
        {
            return false;
        }

        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        bool coversMonitor = GetMonitorInfo(monitor, ref info)
            && window.Left <= info.rcMonitor.Left && window.Top <= info.rcMonitor.Top
            && window.Right >= info.rcMonitor.Right && window.Bottom >= info.rcMonitor.Bottom;
        return coversMonitor && (GetWindowLongPtr(hwnd, GwlStyle) & WsCaption) != WsCaption;
    }

    /// <summary>Full bounds (including the taskbar area) and DPI scale of the primary monitor.</summary>
    public static (PixelRect Bounds, double Scale) GetPrimaryMonitor() => GetMonitorBounds(MonitorFromPoint(default, MonitorDefaultToPrimary));

    /// <summary>Full bounds and DPI scale of the monitor the mouse pointer is on.</summary>
    public static (PixelRect Bounds, double Scale) GetPointerMonitor()
    {
        _ = GetCursorPos(out POINT p);
        return GetMonitorBounds(MonitorFromPoint(p, MonitorDefaultToNearest));
    }

    private static (PixelRect Bounds, double Scale) GetMonitorBounds(nint monitor)
    {
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        _ = GetMonitorInfo(monitor, ref info);
        double scale = GetDpiForMonitor(monitor, 0, out uint dpiX, out _) == 0 ? dpiX / 96.0 : 1.0;
        return (new PixelRect(info.rcMonitor.Left, info.rcMonitor.Top, info.rcMonitor.Right, info.rcMonitor.Bottom), scale);
    }

    /// <summary>
    /// Limits where the window is drawn and takes clicks to a rounded rectangle in window pixels; clicks outside it
    /// reach the windows below. Pass null to remove the limit.
    /// </summary>
    public static void SetHitRegion(nint hwnd, PixelRect? rect, int cornerRadius)
    {
        nint region = rect is { } r ? CreateRoundRectRgn(r.Left, r.Top, r.Right + 1, r.Bottom + 1, cornerRadius * 2, cornerRadius * 2) : 0;

        // On success the system owns the region, so it is only deleted when the call fails.
        if (SetWindowRgn(hwnd, region, true) == 0 && region != 0)
        {
            _ = DeleteObject(region);
        }
    }

    /// <summary>Removes the Windows 11 rounded corners and the 1 px border, for windows that draw their own shape.</summary>
    public static void RemoveFrame(nint hwnd)
    {
        int corner = 1; // DWMWCP_DONOTROUND
        _ = DwmSetWindowAttribute(hwnd, 33, ref corner, sizeof(int));
        int border = unchecked((int)0xFFFFFFFE); // DWMWA_COLOR_NONE
        _ = DwmSetWindowAttribute(hwnd, 34, ref border, sizeof(int));

        // Without a frame, DWM treats the client area as opaque; a "sheet of glass" frame lets it blend the alpha.
        var glass = new MARGINS { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        _ = DwmExtendFrameIntoClientArea(hwnd, ref glass);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MARGINS
    {
        public int Left;
        public int Right;
        public int Top;
        public int Bottom;
    }

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmExtendFrameIntoClientArea(nint hwnd, ref MARGINS margins);

    /// <summary>The id of the process that owns <paramref name="hwnd"/>, or 0.</summary>
    public static uint GetProcessId(nint hwnd)
    {
        if (hwnd == 0)
        {
            return 0;
        }

        _ = GetWindowThreadProcessId(hwnd, out uint pid);
        return pid;
    }

    public static int DragThresholdPixels() => Math.Max(GetSystemMetrics(68), GetSystemMetrics(69)); // SM_CXDRAG / SM_CYDRAG

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GUITHREADINFO
    {
        public int cbSize;
        public uint flags;
        public nint hwndActive;
        public nint hwndFocus;
        public nint hwndCapture;
        public nint hwndMenuOwner;
        public nint hwndMoveSize;
        public nint hwndCaret;
        public RECT rcCaret;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CURSORINFO
    {
        public int cbSize;
        public uint flags;
        public nint hCursor;
        public POINT ptScreenPos;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorInfo(ref CURSORINFO info);

    [LibraryImport("user32.dll", EntryPoint = "LoadCursorW")]
    private static partial nint LoadCursor(nint instance, nint cursorId);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetLayeredWindowAttributes(nint hwnd, uint key, byte alpha, uint flags);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static partial long GetWindowLongPtr(nint hwnd, int index);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static partial nint SetWindowLongPtr(nint hwnd, int index, nint value);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(nint hwnd, int cmd);

    [LibraryImport("user32.dll")]
    private static partial nint MonitorFromPoint(POINT point, uint flags);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetMonitorInfo(nint monitor, ref MONITORINFO info);

    [LibraryImport("shcore.dll")]
    private static partial int GetDpiForMonitor(nint monitor, int type, out uint dpiX, out uint dpiY);

    [LibraryImport("user32.dll")]
    private static partial nint WindowFromPoint(POINT point);

    [LibraryImport("user32.dll")]
    private static partial nint GetAncestor(nint hwnd, uint flags);

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(nint hwnd, out uint pid);

    [LibraryImport("user32.dll", EntryPoint = "GetClassNameW")]
    private static unsafe partial int GetClassName(nint hwnd, char* name, int max);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetGUIThreadInfo(uint threadId, ref GUITHREADINFO info);

    [LibraryImport("shell32.dll")]
    private static partial int SHQueryUserNotificationState(out int state);

    [LibraryImport("user32.dll")]
    private static partial nint MonitorFromWindow(nint hwnd, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowRect(nint hwnd, out RECT rect);

    [LibraryImport("user32.dll")]
    private static partial int SetWindowRgn(nint hwnd, nint region, [MarshalAs(UnmanagedType.Bool)] bool redraw);

    [LibraryImport("gdi32.dll")]
    private static partial nint CreateRoundRectRgn(int left, int top, int right, int bottom, int widthEllipse, int heightEllipse);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteObject(nint handle);

    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsWindow(nint hwnd);

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);
}
