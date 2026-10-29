using System.Runtime.InteropServices;
using FluxKit.Platform.Native;
using FluxKit.Platform.Threading;
using Serilog;
using static FluxKit.Platform.Native.Kernel32;
using static FluxKit.Platform.Native.User32;

namespace FluxKit.Platform.Watchers;

/// <summary>
/// Raises <see cref="Copied"/> when something is copied, through a clipboard format listener on a hidden
/// message-only window. Only the formats on offer are checked; the clipboard is never opened or read.
/// </summary>
public sealed partial class ClipboardWatcher : IDisposable
{
    private const string ClassName = "FluxKit.ClipboardWatcher";
    private const uint WmClipboardUpdate = 0x031D;
    private const uint CfText = 1;
    private const uint CfBitmap = 2;
    private const uint CfDib = 8;
    private const uint CfUnicodeText = 13;
    private const uint CfHDrop = 15;
    private const uint CfDibV5 = 17;
    private static readonly nint HwndMessage = -3;

    // Some apps put several formats on the clipboard one after the other; one copy should give one pulse.
    private const long CoalesceMs = 300;

    private readonly ILogger _log;
    private readonly MessageLoopThread _loop = new("FluxKit.ClipboardWatcher");
    private readonly WindowProc _proc;
    private nint _hwnd;
    private long _lastCopyMs = long.MinValue;

    public ClipboardWatcher(ILogger log)
    {
        _log = log.ForContext<ClipboardWatcher>();
        _proc = OnMessage;
    }

    private delegate nint WindowProc(nint hwnd, uint msg, nint wParam, nint lParam);

    /// <summary>Raised on the watcher thread after each copy.</summary>
    public event EventHandler<ClipboardContent>? Copied;

    public void Start() => _loop.Start(CreateListener, DestroyListener);

    public void Dispose() => _loop.Stop(TimeSpan.FromSeconds(1));

    private void CreateListener()
    {
        nint module = GetModuleHandle(null);
        var wc = new WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_proc),
            hInstance = module,
            lpszClassName = ClassName,
        };
        _ = RegisterClassEx(ref wc);
        _hwnd = CreateWindowEx(0, ClassName, string.Empty, 0, 0, 0, 0, 0, HwndMessage, 0, module, 0);
        if (_hwnd == 0 || !AddClipboardFormatListener(_hwnd))
        {
            _log.Warning("Clipboard listener could not be added (error {Error})", Marshal.GetLastPInvokeError());
        }
    }

    private void DestroyListener()
    {
        if (_hwnd != 0)
        {
            _ = RemoveClipboardFormatListener(_hwnd);
            _ = DestroyWindow(_hwnd);
            _hwnd = 0;
        }

        // The class points at this instance's callback, so it goes too; a new watcher registers its own.
        _ = UnregisterClass(ClassName, GetModuleHandle(null));
    }

    private nint OnMessage(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        if (msg != WmClipboardUpdate)
        {
            return DefWindowProc(hwnd, msg, wParam, lParam);
        }

        long now = Environment.TickCount64;
        if (now - _lastCopyMs >= CoalesceMs)
        {
            _lastCopyMs = now;
            Copied?.Invoke(this, Classify());
        }

        return 0;
    }

    private static ClipboardContent Classify()
    {
        if (IsClipboardFormatAvailable(CfHDrop))
        {
            return ClipboardContent.Files;
        }

        if (IsClipboardFormatAvailable(CfDib) || IsClipboardFormatAvailable(CfDibV5) || IsClipboardFormatAvailable(CfBitmap))
        {
            return ClipboardContent.Image;
        }

        return IsClipboardFormatAvailable(CfUnicodeText) || IsClipboardFormatAvailable(CfText) ? ClipboardContent.Text : ClipboardContent.Other;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AddClipboardFormatListener(nint hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RemoveClipboardFormatListener(nint hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsClipboardFormatAvailable(uint format);

    [LibraryImport("user32.dll", EntryPoint = "DefWindowProcW")]
    private static partial nint DefWindowProc(nint hwnd, uint msg, nint wParam, nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "UnregisterClassW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterClass(string className, nint instance);
}
