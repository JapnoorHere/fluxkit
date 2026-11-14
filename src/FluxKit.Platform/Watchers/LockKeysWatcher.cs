using System.Runtime.InteropServices;
using FluxKit.Platform.Native;
using FluxKit.Platform.Threading;
using Serilog;
using static FluxKit.Platform.Native.Kernel32;
using static FluxKit.Platform.Native.User32;

namespace FluxKit.Platform.Watchers;

/// <summary>Caps Lock or Num Lock was switched on or off.</summary>
public readonly record struct LockKeyToggle(bool IsCapsLock, bool On);

/// <summary>
/// Raises <see cref="Toggled"/> when Caps Lock or Num Lock changes. It listens with Raw Input on a hidden window,
/// which tells it about key presses without installing a hook or slowing typing down. Every key other than
/// Caps Lock and Num Lock is dropped on arrival: nothing is stored, logged or passed on.
/// </summary>
public sealed partial class LockKeysWatcher : IDisposable
{
    private const string ClassName = "FluxKit.LockKeysWatcher";
    private const uint WmInput = 0x00FF;
    private const uint WmApp = 0x8000;
    private const uint WmLockKeyPressed = WmApp + 1;
    private const uint WmKeyDown = 0x0100;
    private const uint WmSysKeyDown = 0x0104;
    private const uint RidInput = 0x10000003;
    private const uint RimTypeKeyboard = 1;
    private const uint RideVInputSink = 0x00000100;
    private const uint RideVRemove = 0x00000001;
    private const ushort VkCapital = 0x14;
    private const ushort VkNumLock = 0x90;

    private readonly ILogger _log;
    private readonly MessageLoopThread _loop = new("FluxKit.LockKeysWatcher");
    private readonly WindowProc _proc;
    private nint _hwnd;
    private bool _capsDown;
    private bool _numDown;

    public LockKeysWatcher(ILogger log)
    {
        _log = log.ForContext<LockKeysWatcher>();
        _proc = OnMessage;
    }

    private delegate nint WindowProc(nint hwnd, uint msg, nint wParam, nint lParam);

    /// <summary>Raised on the watcher thread after Caps Lock or Num Lock changes, with its new state.</summary>
    public event EventHandler<LockKeyToggle>? Toggled;

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

        // A hidden top-level window (never shown): Raw Input in the background needs a real target window.
        _hwnd = CreateWindowEx(0, ClassName, string.Empty, 0, 0, 0, 0, 0, 0, 0, module, 0);
        RawInputDevice[] keyboard = [new RawInputDevice { UsagePage = 0x01, Usage = 0x06, Flags = RideVInputSink, Target = _hwnd }];
        if (_hwnd == 0 || !RegisterRawInputDevices(keyboard, 1, (uint)Marshal.SizeOf<RawInputDevice>()))
        {
            _log.Warning("Lock key listener could not be added (error {Error})", Marshal.GetLastPInvokeError());
        }
    }

    private void DestroyListener()
    {
        if (_hwnd != 0)
        {
            RawInputDevice[] keyboard = [new RawInputDevice { UsagePage = 0x01, Usage = 0x06, Flags = RideVRemove, Target = 0 }];
            _ = RegisterRawInputDevices(keyboard, 1, (uint)Marshal.SizeOf<RawInputDevice>());
            _ = DestroyWindow(_hwnd);
            _hwnd = 0;
        }

        _ = UnregisterClass(ClassName, GetModuleHandle(null));
    }

    private nint OnMessage(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        if (msg == WmInput)
        {
            OnInput(hwnd, lParam);
            return DefWindowProc(hwnd, msg, wParam, lParam); // Lets Windows clean up the input.
        }

        if (msg == WmLockKeyPressed)
        {
            // Read after Windows has handled the press, so the toggle has flipped by now.
            bool caps = wParam == VkCapital;
            bool on = (GetKeyState(caps ? VkCapital : VkNumLock) & 1) != 0;
            Toggled?.Invoke(this, new LockKeyToggle(caps, on));
            return 0;
        }

        return DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private void OnInput(nint hwnd, nint handle)
    {
        uint headerSize = (uint)Marshal.SizeOf<RawInputHeader>();
        uint size = 0;
        _ = GetRawInputData(handle, RidInput, 0, ref size, headerSize);
        if (size == 0 || size > 256)
        {
            return;
        }

        nint buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            if (GetRawInputData(handle, RidInput, buffer, ref size, headerSize) != size)
            {
                return;
            }

            RawInputHeader header = Marshal.PtrToStructure<RawInputHeader>(buffer);
            if (header.Type != RimTypeKeyboard)
            {
                return;
            }

            RawKeyboard key = Marshal.PtrToStructure<RawKeyboard>(buffer + (int)headerSize);
            if (key.VKey is not (VkCapital or VkNumLock))
            {
                return; // Any other key is ignored and forgotten.
            }

            bool down = key.Message is WmKeyDown or WmSysKeyDown;
            ref bool held = ref key.VKey == VkCapital ? ref _capsDown : ref _numDown;
            if (down && !held)
            {
                _ = PostMessage(hwnd, WmLockKeyPressed, key.VKey, 0);
            }

            held = down;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputDevice
    {
        public ushort UsagePage;
        public ushort Usage;
        public uint Flags;
        public nint Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputHeader
    {
        public uint Type;
        public uint Size;
        public nint Device;
        public nint WParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawKeyboard
    {
        public ushort MakeCode;
        public ushort Flags;
        public ushort Reserved;
        public ushort VKey;
        public uint Message;
        public uint ExtraInformation;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterRawInputDevices([In] RawInputDevice[] devices, uint count, uint size);

    [LibraryImport("user32.dll")]
    private static partial uint GetRawInputData(nint rawInput, uint command, nint data, ref uint size, uint headerSize);

    [LibraryImport("user32.dll")]
    private static partial short GetKeyState(int key);

    [LibraryImport("user32.dll", EntryPoint = "PostMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PostMessage(nint hwnd, uint msg, nint wParam, nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "DefWindowProcW")]
    private static partial nint DefWindowProc(nint hwnd, uint msg, nint wParam, nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "UnregisterClassW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterClass(string className, nint instance);
}
