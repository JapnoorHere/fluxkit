using System.Runtime.InteropServices;
using static FluxKit.Platform.Native.User32;

namespace FluxKit.Platform.Input;

/// <summary>Synthesizes mouse and keyboard input with SendInput.</summary>
public static partial class InputSynthesizer
{
    /// <summary>Tag placed in dwExtraInfo so FluxKit's own hooks can recognise input it injected ("FLUX").</summary>
    public const nint FluxKitSignature = 0x464C5558;

    public const ushort VkControl = 0x11;
    public const ushort VkLeftWindows = 0x5B;
    public const ushort VkLeft = 0x25;
    public const ushort VkRight = 0x27;
    public const ushort VkEscape = 0x1B;

    private const uint INPUT_MOUSE = 0;
    private const uint INPUT_KEYBOARD = 1;
    private const uint MOUSEEVENTF_MOVE = 0x0001;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_VIRTUALDESK = 0x4000;
    private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    public static void MoveTo(int x, int y, nint extraInfo = 0)
    {
        int left = GetSystemMetrics(76), top = GetSystemMetrics(77);
        int width = Math.Max(2, GetSystemMetrics(78)), height = Math.Max(2, GetSystemMetrics(79));
        var input = MouseInput(
            (x - left) * 65535 / (width - 1),
            (y - top) * 65535 / (height - 1),
            MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK,
            extraInfo);
        Send(input);
    }

    public static void LeftDown(nint extraInfo = 0) => Send(MouseInput(0, 0, MOUSEEVENTF_LEFTDOWN, extraInfo));

    public static void LeftUp(nint extraInfo = 0) => Send(MouseInput(0, 0, MOUSEEVENTF_LEFTUP, extraInfo));

    /// <summary>Presses the keys in order, then releases them in reverse order, as one atomic batch.</summary>
    public static void KeyCombo(nint extraInfo, params ushort[] keys)
    {
        var inputs = new INPUT[keys.Length * 2];
        for (int i = 0; i < keys.Length; i++)
        {
            inputs[i] = KeyInput(keys[i], up: false, extraInfo);
            inputs[(keys.Length * 2) - 1 - i] = KeyInput(keys[i], up: true, extraInfo);
        }

        Send(inputs);
    }

    public static void Key(ushort vk, bool up, nint extraInfo = 0) => Send(KeyInput(vk, up, extraInfo));

    private static INPUT MouseInput(int dx, int dy, uint flags, nint extraInfo) => new()
    {
        type = INPUT_MOUSE,
        u = new InputUnion { mi = new MOUSEINPUT { dx = dx, dy = dy, dwFlags = flags, dwExtraInfo = extraInfo } },
    };

    private static INPUT KeyInput(ushort vk, bool up, nint extraInfo)
    {
        uint flags = up ? KEYEVENTF_KEYUP : 0;
        if (vk is VkLeft or VkRight or VkLeftWindows)
        {
            flags |= KEYEVENTF_EXTENDEDKEY;
        }

        return new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = flags, dwExtraInfo = extraInfo } },
        };
    }

    private static void Send(params INPUT[] inputs)
    {
        uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent != inputs.Length)
        {
            throw new InvalidOperationException($"SendInput injected {sent}/{inputs.Length} events (error {Marshal.GetLastPInvokeError()}).");
        }
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial uint SendInput(uint count, [In] INPUT[] inputs, int size);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public nint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public nint dwExtraInfo;
    }
}
