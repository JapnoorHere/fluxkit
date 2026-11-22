using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace FluxKit.Platform.Windows;

/// <summary>A window someone could want to watch: its handle and title (titles are shown on screen only, never logged).</summary>
public sealed record PeekableWindow(nint Handle, string Title);

/// <summary>
/// Window peek (plan C): a live view of another window drawn by Windows itself (a DWM thumbnail) inside a window of
/// ours. It costs FluxKit nothing per frame: DWM composes it. One thumbnail at a time; dispose to remove it.
/// </summary>
public sealed partial class WindowThumbnail : IDisposable
{
    private const uint RectDestination = 0x1;
    private const uint Visible = 0x8;
    private const uint Opacity = 0x4;
    private const uint SourceClientAreaOnly = 0x10;

    private nint _thumbnail;

    /// <summary>The window being shown, or 0.</summary>
    public nint Source { get; private set; }

    /// <summary>Starts showing <paramref name="source"/> inside <paramref name="destination"/> (hidden until <see cref="Place"/>).</summary>
    public bool Attach(nint destination, nint source)
    {
        Detach();
        if (DwmRegisterThumbnail(destination, source, out _thumbnail) != 0)
        {
            _thumbnail = 0;
            return false;
        }

        Source = source;
        return true;
    }

    /// <summary>
    /// Shows the thumbnail fitted inside <paramref name="area"/> (pixels of the destination window), keeping the source's
    /// shape, or hides it. Returns false when the source window has gone.
    /// </summary>
    public bool Place(PixelRect area, bool visible)
    {
        if (_thumbnail == 0)
        {
            return false;
        }

        if (!IsWindow(Source))
        {
            Detach();
            return false;
        }

        PixelRect fitted = area;
        if (visible && DwmQueryThumbnailSourceSize(_thumbnail, out SIZE source) == 0 && source.Cx > 0 && source.Cy > 0)
        {
            double scale = Math.Min(area.Width / (double)source.Cx, area.Height / (double)source.Cy);
            int width = (int)(source.Cx * scale);
            int height = (int)(source.Cy * scale);
            int left = area.Left + ((area.Width - width) / 2);
            int top = area.Top + ((area.Height - height) / 2);
            fitted = new PixelRect(left, top, left + width, top + height);
        }

        var properties = new DwmThumbnailProperties
        {
            Flags = RectDestination | Visible | Opacity | SourceClientAreaOnly,
            Destination = new RECT { Left = fitted.Left, Top = fitted.Top, Right = fitted.Right, Bottom = fitted.Bottom },
            Opacity = 255,
            Visible = visible ? 1 : 0,
            SourceClientAreaOnly = 0,
        };
        return DwmUpdateThumbnailProperties(_thumbnail, in properties) == 0;
    }

    public void Detach()
    {
        if (_thumbnail != 0)
        {
            _ = DwmUnregisterThumbnail(_thumbnail);
            _thumbnail = 0;
        }

        Source = 0;
    }

    public void Dispose() => Detach();

    /// <summary>
    /// The windows on screen worth peeking at: visible, titled, top-level app windows that aren't tool windows, aren't
    /// hidden by Windows (cloaked, e.g. on another desktop) and don't belong to FluxKit. Newest in the z-order first.
    /// </summary>
    public static IReadOnlyList<PeekableWindow> ListWindows()
    {
        var found = new List<PeekableWindow>();
        GCHandle handle = GCHandle.Alloc(found);
        try
        {
            unsafe
            {
                _ = EnumWindows(&Collect, GCHandle.ToIntPtr(handle));
            }
        }
        finally
        {
            handle.Free();
        }

        return found;
    }

    /// <summary>Brings a window to the front, restoring it if it is minimised.</summary>
    public static void Activate(nint window)
    {
        if (IsIconic(window))
        {
            _ = ShowWindow(window, 9); // SW_RESTORE
        }

        _ = SetForegroundWindow(window);
    }

    public static bool Exists(nint window) => window != 0 && IsWindow(window);

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Collect(nint window, nint state)
    {
        var found = (List<PeekableWindow>)GCHandle.FromIntPtr(state).Target!;
        if (!IsWindowVisible(window) || GetWindow(window, 4) != 0) // GW_OWNER
        {
            return 1;
        }

        long exStyle = GetWindowLongPtr(window, -20);
        if ((exStyle & 0x80) != 0) // WS_EX_TOOLWINDOW
        {
            return 1;
        }

        if (DwmGetWindowAttribute(window, 14, out int cloaked, sizeof(int)) == 0 && cloaked != 0) // DWMWA_CLOAKED
        {
            return 1;
        }

        _ = GetWindowThreadProcessId(window, out uint process);
        if (process == (uint)Environment.ProcessId)
        {
            return 1;
        }

        int length = GetWindowTextLength(window);
        if (length == 0)
        {
            return 1;
        }

        found.Add(new PeekableWindow(window, ReadTitle(window, Math.Min(length + 1, 512))));
        return 1;
    }

    private static unsafe string ReadTitle(nint window, int max)
    {
        char* buffer = stackalloc char[max];
        int length = GetWindowText(window, buffer, max);
        return new string(buffer, 0, Math.Max(0, length));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE
    {
        public int Cx;
        public int Cy;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DwmThumbnailProperties
    {
        public uint Flags;
        public RECT Destination;
        public RECT Source;
        public byte Opacity;
        public int Visible;
        public int SourceClientAreaOnly;
    }

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmRegisterThumbnail(nint destination, nint source, out nint thumbnail);

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmUnregisterThumbnail(nint thumbnail);

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmUpdateThumbnailProperties(nint thumbnail, in DwmThumbnailProperties properties);

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmQueryThumbnailSourceSize(nint thumbnail, out SIZE size);

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmGetWindowAttribute(nint window, int attribute, out int value, int size);

    [LibraryImport("user32.dll")]
    private static unsafe partial int EnumWindows(delegate* unmanaged[Stdcall]<nint, nint, int> callback, nint state);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsWindowVisible(nint window);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsWindow(nint window);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsIconic(nint window);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(nint window, int command);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(nint window);

    [LibraryImport("user32.dll")]
    private static partial nint GetWindow(nint window, uint command);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static partial long GetWindowLongPtr(nint window, int index);

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(nint window, out uint process);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowTextLengthW")]
    private static partial int GetWindowTextLength(nint window);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowTextW")]
    private static unsafe partial int GetWindowText(nint window, char* text, int max);
}
