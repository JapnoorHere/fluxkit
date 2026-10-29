using System.Runtime.InteropServices;
using FluxKit.Platform.Native;
using static FluxKit.Platform.Native.Kernel32;
using static FluxKit.Platform.Native.User32;
using ComTypes = System.Runtime.InteropServices.ComTypes;

namespace FluxKit.Platform.Windows;

/// <summary>What an OLE drag carries, as seen by <see cref="DragPayloadProbe"/>.</summary>
public enum DragPayload
{
    /// <summary>Not known yet (the drag hasn't passed over the probe).</summary>
    Unknown,

    /// <summary>Files or shell items, including attachments dragged out of apps such as Outlook.</summary>
    Files,

    /// <summary>Anything else: selected text, links, web images, rich content.</summary>
    Other,
}

/// <summary>
/// Finds out what an OLE drag carries without taking part in it. While asked to, it keeps a small, almost fully
/// transparent drop-target window under the cursor. When the drag passes over it, OLE hands it the data object;
/// the probe only checks which formats are offered, never reads the data, and always answers "no drop".
/// The window then hides and the drag carries on to the real target.
/// </summary>
public sealed partial class DragPayloadProbe : IDisposable
{
    private const int Size = 96;
    private const string ClassName = "FluxKit.DragPayloadProbe";
    private const uint WsPopup = 0x80000000;
    private const uint WsExLayered = 0x80000;
    private const uint WsExToolWindow = 0x80;
    private const uint WsExTopmost = 0x8;
    private const uint WsExNoActivate = 0x08000000;
    private const uint LwaAlpha = 0x2;
    private const uint SwpNoSize = 0x1;
    private const uint SwpNoZOrder = 0x4;
    private const uint SwpNoActivate = 0x10;
    private const uint SwpShowWindow = 0x40;
    private const uint SwpHideWindow = 0x80;
    private const uint SwpAsyncWindowPos = 0x4000;
    private const short CfHdrop = 15;
    private static readonly nint HwndTopmost = -1;

    private readonly DropTarget _target;
    private Thread? _thread;
    private uint _threadId;
    private nint _hwnd;
    private int _result;

    public DragPayloadProbe() => _target = new DropTarget(this);

    /// <summary>The payload of the drag that last passed over the probe since <see cref="Begin"/>.</summary>
    public DragPayload Result => (DragPayload)Volatile.Read(ref _result);

    /// <summary>Shows the probe centred on a point in physical pixels and forgets any earlier result.</summary>
    public void Begin(int x, int y)
    {
        EnsureStarted();
        Volatile.Write(ref _result, (int)DragPayload.Unknown);
        if (_hwnd != 0)
        {
            _ = SetWindowPos(_hwnd, HwndTopmost, x - (Size / 2), y - (Size / 2), Size, Size, SwpNoActivate | SwpShowWindow | SwpAsyncWindowPos);
        }
    }

    /// <summary>Keeps the probe under the cursor; OLE only looks for a new target when the cursor moves.</summary>
    public void MoveTo(int x, int y)
    {
        if (_hwnd != 0)
        {
            _ = SetWindowPos(_hwnd, 0, x - (Size / 2), y - (Size / 2), 0, 0, SwpNoActivate | SwpNoSize | SwpNoZOrder | SwpAsyncWindowPos);
        }
    }

    public void End()
    {
        if (_hwnd != 0)
        {
            _ = SetWindowPos(_hwnd, 0, 0, 0, 0, 0, SwpNoActivate | SwpNoSize | SwpNoZOrder | SwpHideWindow | SwpAsyncWindowPos);
        }
    }

    public void Dispose()
    {
        if (_thread is { } thread)
        {
            _ = PostThreadMessage(_threadId, WmQuit, 0, 0);
            thread.Join(TimeSpan.FromSeconds(2));
            _thread = null;
        }
    }

    private void EnsureStarted()
    {
        if (_thread is not null)
        {
            return;
        }

        using var ready = new ManualResetEventSlim(false);
        _thread = new Thread(() => Run(ready)) { IsBackground = true, Name = "FluxKit.DragProbe" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        ready.Wait(TimeSpan.FromSeconds(2));
    }

    private void Run(ManualResetEventSlim ready)
    {
        _threadId = GetCurrentThreadId();
        _ = SetThreadDpiAwarenessContext(-4); // per-monitor v2: positions are physical pixels, like the input hook
        _ = OleInitialize(0);
        nint module = GetModuleHandle(null);
        var wc = new WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc = GetProcAddress(GetModuleHandle("user32.dll"), "DefWindowProcW"),
            hInstance = module,
            lpszClassName = ClassName,
        };
        _ = RegisterClassEx(ref wc);
        _hwnd = CreateWindowEx(WsExLayered | WsExToolWindow | WsExTopmost | WsExNoActivate, ClassName, string.Empty, WsPopup, 0, 0, Size, Size, 0, 0, module, 0);
        if (_hwnd != 0)
        {
            // Alpha 1 of 255: invisible, but still hit-testable, so OLE treats it as a drop target.
            _ = SetLayeredWindowAttributes(_hwnd, 0, 1, LwaAlpha);
            _ = RegisterDragDrop(_hwnd, _target);
        }

        ready.Set();
        while (GetMessage(out MSG msg, 0, 0, 0) > 0)
        {
            _ = TranslateMessage(msg);
            _ = DispatchMessage(msg);
        }

        if (_hwnd != 0)
        {
            _ = RevokeDragDrop(_hwnd);
            _ = DestroyWindow(_hwnd);
            _hwnd = 0;
        }

        OleUninitialize();
    }

    private static DragPayload Classify(ComTypes.IDataObject data)
    {
        bool Has(short format)
        {
            var f = new ComTypes.FORMATETC { cfFormat = format, dwAspect = ComTypes.DVASPECT.DVASPECT_CONTENT, lindex = -1, tymed = ComTypes.TYMED.TYMED_HGLOBAL };
            return data.QueryGetData(ref f) == 0;
        }

        if (Has(CfHdrop) || Has(Format("Shell IDList Array")))
        {
            return DragPayload.Files;
        }

        // Virtual files (e.g. an Outlook attachment) count as files. Browsers also describe links and web images
        // as virtual files, but those always come with a URL, so they don't.
        return Has(Format("FileGroupDescriptorW")) && !Has(Format("UniformResourceLocatorW")) ? DragPayload.Files : DragPayload.Other;
    }

    private static short Format(string name) => unchecked((short)RegisterClipboardFormat(name));

    /// <summary>The OLE drop target behind the probe window. It never accepts a drop.</summary>
    [ComVisible(true)]
    private sealed class DropTarget(DragPayloadProbe owner) : IDropTarget
    {
        public int DragEnter(ComTypes.IDataObject data, int keyState, long point, ref int effect)
        {
            DragPayload payload;
            try
            {
                payload = Classify(data);
            }
            catch (COMException)
            {
                payload = DragPayload.Other;
            }

            Volatile.Write(ref owner._result, (int)payload);
            effect = 0;
            return 0;
        }

        public int DragOver(int keyState, long point, ref int effect)
        {
            effect = 0;
            return 0;
        }

        public int DragLeave() => 0;

        public int Drop(ComTypes.IDataObject data, int keyState, long point, ref int effect)
        {
            effect = 0;
            return 0;
        }
    }

#pragma warning disable SYSLIB1054 // COM interface and string-struct marshalling require classic DllImport.
    [DllImport("ole32.dll")]
    private static extern int RegisterDragDrop(nint hwnd, [MarshalAs(UnmanagedType.Interface)] IDropTarget target);

#pragma warning restore SYSLIB1054

    [LibraryImport("ole32.dll")]
    private static partial int RevokeDragDrop(nint hwnd);

    [LibraryImport("ole32.dll")]
    private static partial int OleInitialize(nint reserved);

    [LibraryImport("ole32.dll")]
    private static partial void OleUninitialize();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetLayeredWindowAttributes(nint hwnd, uint key, byte alpha, uint flags);

    [LibraryImport("user32.dll")]
    private static partial nint SetThreadDpiAwarenessContext(nint context);

    [LibraryImport("user32.dll", EntryPoint = "RegisterClipboardFormatW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint RegisterClipboardFormat(string name);

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint GetProcAddress(nint module, string name);
}

/// <summary>The OLE IDropTarget interface (oleidl.h).</summary>
[ComImport]
[Guid("00000122-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDropTarget
{
    [PreserveSig]
    int DragEnter([MarshalAs(UnmanagedType.Interface)] ComTypes.IDataObject data, int keyState, long point, ref int effect);

    [PreserveSig]
    int DragOver(int keyState, long point, ref int effect);

    [PreserveSig]
    int DragLeave();

    [PreserveSig]
    int Drop([MarshalAs(UnmanagedType.Interface)] ComTypes.IDataObject data, int keyState, long point, ref int effect);
}
