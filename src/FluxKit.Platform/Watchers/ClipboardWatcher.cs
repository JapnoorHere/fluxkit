using System.Runtime.InteropServices;
using FluxKit.Platform.Native;
using FluxKit.Platform.Threading;
using Serilog;
using static FluxKit.Platform.Native.Kernel32;
using static FluxKit.Platform.Native.User32;

namespace FluxKit.Platform.Watchers;

/// <summary>
/// Raises <see cref="Copied"/> when something is copied, through a clipboard format listener on a hidden
/// message-only window. By default only the formats on offer are checked and the clipboard is never opened; with
/// <c>readText</c> the text of a text copy is read too. Copies that apps mark as private (password managers) are
/// skipped entirely, as Windows asks clipboard monitors to do.
/// </summary>
public sealed partial class ClipboardWatcher : IDisposable
{
    /// <summary>Longer text is cut to this many characters; nothing longer is ever needed.</summary>
    public const int MaxTextLength = 2048;

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
    private readonly bool _readText;
    private readonly uint[] _privateFormats;
    // Not long.MinValue: "now - MinValue" overflows to a negative number and no copy would ever be reported.
    private long _lastCopyMs = -CoalesceMs;

    public ClipboardWatcher(ILogger log, bool readText = false)
    {
        _log = log.ForContext<ClipboardWatcher>();
        _readText = readText;
        _proc = OnMessage;

        // Documented markers for content that clipboard history and monitors should leave alone.
        _privateFormats = [RegisterClipboardFormat("ExcludeClipboardContentFromMonitorProcessing"), RegisterClipboardFormat("Clipboard Viewer Ignore")];
    }

    private delegate nint WindowProc(nint hwnd, uint msg, nint wParam, nint lParam);

    /// <summary>Raised on the watcher thread after each copy, except private ones.</summary>
    public event EventHandler<ClipboardCopy>? Copied;

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
        if (now - _lastCopyMs >= CoalesceMs && !_privateFormats.Any(f => f != 0 && IsClipboardFormatAvailable(f)))
        {
            _lastCopyMs = now;
            ClipboardContent content = Classify();
            Copied?.Invoke(this, new ClipboardCopy(content, _readText && content == ClipboardContent.Text ? ReadText(hwnd) : null));
        }

        return 0;
    }

    /// <summary>The clipboard's Unicode text, cut to <see cref="MaxTextLength"/>, or null if it can't be had right now.</summary>
    private static string? ReadText(nint owner)
    {
        if (!IsClipboardFormatAvailable(CfUnicodeText) || !OpenClipboard(owner))
        {
            return null;
        }

        try
        {
            nint handle = GetClipboardData(CfUnicodeText);
            nint data = handle == 0 ? 0 : GlobalLock(handle);
            if (data == 0)
            {
                return null;
            }

            try
            {
                int chars = (int)Math.Min((long)GlobalSize(handle) / 2, MaxTextLength);
                string text = Marshal.PtrToStringUni(data, chars);
                int end = text.IndexOf('\0', StringComparison.Ordinal);
                return end >= 0 ? text[..end] : text;
            }
            finally
            {
                _ = GlobalUnlock(handle);
            }
        }
        finally
        {
            _ = CloseClipboard();
        }
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

    [LibraryImport("user32.dll", EntryPoint = "RegisterClipboardFormatW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint RegisterClipboardFormat(string name);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool OpenClipboard(nint owner);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseClipboard();

    [LibraryImport("user32.dll")]
    private static partial nint GetClipboardData(uint format);

    [LibraryImport("kernel32.dll")]
    private static partial nint GlobalLock(nint memory);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalUnlock(nint memory);

    [LibraryImport("kernel32.dll")]
    private static partial nuint GlobalSize(nint memory);

    [LibraryImport("user32.dll", EntryPoint = "DefWindowProcW")]
    private static partial nint DefWindowProc(nint hwnd, uint msg, nint wParam, nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "UnregisterClassW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterClass(string className, nint instance);
}
