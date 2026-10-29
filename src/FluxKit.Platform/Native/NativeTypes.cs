using System.Runtime.InteropServices;

namespace FluxKit.Platform.Native;

/// <summary>Win32 POINT (windef.h).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct POINT
{
    public int X;
    public int Y;
}

/// <summary>Win32 MSG (winuser.h), used by the message loops.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct MSG
{
    public nint hwnd;
    public uint message;
    public nint wParam;
    public nint lParam;
    public uint time;
    public POINT pt;
    public uint lPrivate;
}

/// <summary>Win32 WNDCLASSEXW (winuser.h), used to register FluxKit's hidden windows.</summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct WNDCLASSEX
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
