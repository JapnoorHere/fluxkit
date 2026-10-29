using System.Runtime.InteropServices;

namespace FluxKit.Platform.Shell;

/// <summary>One entry of the tray context menu.</summary>
public sealed record TrayMenuItem(string Text, Action? Invoke, bool Checked = false, bool IsSeparator = false, bool Enabled = true)
{
    public static TrayMenuItem Separator { get; } = new(string.Empty, null, IsSeparator: true);
}

/// <summary>
/// Native notification-area icon (Shell_NotifyIcon) with a native popup menu.
/// Must be created on a thread with a message pump (the UI thread); callbacks run on that thread.
/// Re-adds itself and raises <see cref="TaskbarRecreated"/> when Explorer restarts.
/// </summary>
public sealed unsafe partial class TrayIcon : IDisposable
{
    private const uint NimAdd = 0, NimModify = 1, NimDelete = 2;
    private const uint NifMessage = 0x1, NifIcon = 0x2, NifTip = 0x4;
    private const uint WmApp = 0x8000, WmNull = 0x0000, WmLButtonUp = 0x0202, WmRButtonUp = 0x0205, WmContextMenu = 0x007B;
    private const uint MfString = 0x0, MfGrayed = 0x1, MfChecked = 0x8, MfSeparator = 0x800;
    private const uint TpmReturnCmd = 0x100, TpmRightButton = 0x2, TpmBottomAlign = 0x20;
    private const uint CallbackMessage = WmApp + 1;
    private const string ClassName = "FluxKit.TrayWindow";

    private readonly WndProc _wndProc;
    private readonly uint _taskbarCreated;
    private readonly nint _hwnd;
    private nint _icon;
    private string _tooltip;
    private bool _added;

    public TrayIcon(string iconPath, string tooltip)
    {
        _tooltip = tooltip;
        _wndProc = WindowProc;
        _taskbarCreated = RegisterWindowMessage("TaskbarCreated");
        nint instance = GetModuleHandle(null);
        var wc = new WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = instance,
            lpszClassName = ClassName,
        };
        _ = RegisterClassEx(ref wc);
        _hwnd = CreateWindowEx(0, ClassName, "FluxKit", 0, 0, 0, 0, 0, 0, 0, instance, 0);
        _icon = LoadIconFile(iconPath);
        Add();
    }

    private delegate nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam);

    /// <summary>Left click on the icon.</summary>
    public event EventHandler? Activated;

    /// <summary>Explorer restarted (icon was re-added). Useful to re-initialize shell integrations.</summary>
    public event EventHandler? TaskbarRecreated;

    /// <summary>Builds the context menu each time it opens, so check marks are always current.</summary>
    public Func<IReadOnlyList<TrayMenuItem>>? MenuProvider { get; set; }

    public void SetIcon(string iconPath)
    {
        nint old = _icon;
        _icon = LoadIconFile(iconPath);
        Update(NifIcon);
        if (old != 0)
        {
            _ = DestroyIcon(old);
        }
    }

    public void SetTooltip(string tooltip)
    {
        _tooltip = tooltip;
        Update(NifTip);
    }

    public void Dispose()
    {
        if (_added)
        {
            NOTIFYICONDATA data = NewData(0);
            _ = Shell_NotifyIcon(NimDelete, ref data);
            _added = false;
        }

        if (_icon != 0)
        {
            _ = DestroyIcon(_icon);
            _icon = 0;
        }

        _ = DestroyWindow(_hwnd);
    }

    private static nint LoadIconFile(string path)
    {
        int size = GetSystemMetrics(49); // SM_CXSMICON, DPI-aware for the process
        return LoadImage(0, path, 1, size, size, 0x10);
    }

    private void Add()
    {
        NOTIFYICONDATA data = NewData(NifMessage | NifIcon | NifTip);
        _added = Shell_NotifyIcon(NimAdd, ref data);
    }

    private void Update(uint flags)
    {
        NOTIFYICONDATA data = NewData(flags);
        _ = Shell_NotifyIcon(NimModify, ref data);
    }

    private NOTIFYICONDATA NewData(uint flags)
    {
        var data = new NOTIFYICONDATA
        {
            cbSize = (uint)sizeof(NOTIFYICONDATA),
            hWnd = _hwnd,
            uID = 1,
            uFlags = flags,
            uCallbackMessage = CallbackMessage,
            hIcon = _icon,
        };
        ReadOnlySpan<char> tip = _tooltip.AsSpan(0, Math.Min(_tooltip.Length, 127));
        tip.CopyTo(new Span<char>(data.szTip, 128));
        return data;
    }

    private nint WindowProc(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        if (msg == CallbackMessage)
        {
            switch ((uint)lParam & 0xFFFF)
            {
                case WmLButtonUp:
                    Activated?.Invoke(this, EventArgs.Empty);
                    return 0;
                case WmRButtonUp:
                case WmContextMenu:
                    ShowMenu();
                    return 0;
            }
        }
        else if (msg == _taskbarCreated && _taskbarCreated != 0)
        {
            Add();
            TaskbarRecreated?.Invoke(this, EventArgs.Empty);
            return 0;
        }

        return DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private void ShowMenu()
    {
        IReadOnlyList<TrayMenuItem> items = MenuProvider?.Invoke() ?? [];
        if (items.Count == 0)
        {
            return;
        }

        nint menu = CreatePopupMenu();
        try
        {
            for (int i = 0; i < items.Count; i++)
            {
                TrayMenuItem item = items[i];
                uint flags = item.IsSeparator ? MfSeparator : MfString | (item.Checked ? MfChecked : 0) | (item.Enabled ? 0 : MfGrayed);
                _ = AppendMenu(menu, flags, (nuint)(i + 1), item.IsSeparator ? null : item.Text);
            }

            _ = GetCursorPos(out POINT pt);
            _ = SetForegroundWindow(_hwnd); // required so the menu closes when focus moves away
            int chosen = TrackPopupMenuEx(menu, TpmReturnCmd | TpmRightButton | TpmBottomAlign, pt.X, pt.Y, _hwnd, 0);
            _ = PostMessage(_hwnd, WmNull, 0, 0);
            if (chosen > 0 && chosen <= items.Count)
            {
                items[chosen - 1].Invoke?.Invoke();
            }
        }
        finally
        {
            _ = DestroyMenu(menu);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEX
    {
        public uint cbSize;
        public uint style;
        public nint lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public nint hInstance;
        public nint hIcon;
        public nint hCursor;
        public nint hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public nint hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NOTIFYICONDATA
    {
        public uint cbSize;
        public nint hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public nint hIcon;
        public fixed char szTip[128];
        public uint dwState;
        public uint dwStateMask;
        public fixed char szInfo[256];
        public uint uTimeoutOrVersion;
        public fixed char szInfoTitle[64];
        public uint dwInfoFlags;
        public Guid guidItem;
        public nint hBalloonIcon;
    }

#pragma warning disable SYSLIB1054 // Struct with string fields needs classic marshalling.
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegisterClassExW")]
    private static extern ushort RegisterClassEx(ref WNDCLASSEX wc);
#pragma warning restore SYSLIB1054

    [LibraryImport("user32.dll", EntryPoint = "CreateWindowExW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint CreateWindowEx(uint exStyle, string className, string title, uint style, int x, int y, int w, int h, nint parent, nint menu, nint instance, nint param);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyWindow(nint hwnd);

    [LibraryImport("user32.dll", EntryPoint = "DefWindowProcW")]
    private static partial nint DefWindowProc(nint hwnd, uint msg, nint wParam, nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint RegisterWindowMessage(string name);

    [LibraryImport("shell32.dll", EntryPoint = "Shell_NotifyIconW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool Shell_NotifyIcon(uint message, ref NOTIFYICONDATA data);

    [LibraryImport("user32.dll", EntryPoint = "LoadImageW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint LoadImage(nint instance, string name, uint type, int cx, int cy, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyIcon(nint icon);

    [LibraryImport("user32.dll")]
    private static partial int GetSystemMetrics(int index);

    [LibraryImport("user32.dll")]
    private static partial nint CreatePopupMenu();

    [LibraryImport("user32.dll", EntryPoint = "AppendMenuW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AppendMenu(nint menu, uint flags, nuint id, string? text);

    [LibraryImport("user32.dll")]
    private static partial int TrackPopupMenuEx(nint menu, uint flags, int x, int y, nint hwnd, nint tpm);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyMenu(nint menu);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(nint hwnd);

    [LibraryImport("user32.dll", EntryPoint = "PostMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PostMessage(nint hwnd, uint msg, nint wParam, nint lParam);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out POINT point);

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint GetModuleHandle(string? name);
}
