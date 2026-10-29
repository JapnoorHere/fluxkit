using System.Runtime.InteropServices;

namespace FluxKit.Platform.Native;

/// <summary>Hand-written Win32 / COM imports used across the platform layer.</summary>
internal static partial class NativeMethods
{
    public const int S_OK = 0;
    public const int CLSCTX_LOCAL_SERVER = 0x4;
    public const int COINIT_APARTMENTTHREADED = 0x2;
    public const int RPC_E_DISCONNECTED = unchecked((int)0x80010108);
    public const int RPC_E_SERVER_DIED_DNE = unchecked((int)0x800706BA);

    [LibraryImport("ole32.dll")]
    public static partial int CoInitializeEx(nint reserved, int coInit);

    [LibraryImport("ole32.dll")]
    public static partial void CoUninitialize();

    [LibraryImport("ole32.dll")]
    public static partial int CoCreateInstance(in Guid clsid, nint outer, int clsContext, in Guid iid, out nint ppv);

    [LibraryImport("combase.dll")]
    public static unsafe partial char* WindowsGetStringRawBuffer(nint hstring, out uint length);

    [LibraryImport("combase.dll")]
    public static partial int WindowsDeleteString(nint hstring);

    public static unsafe string? ConsumeHString(nint hstring)
    {
        if (hstring == 0)
        {
            return null;
        }

        try
        {
            char* buffer = WindowsGetStringRawBuffer(hstring, out uint length);
            return new string(buffer, 0, (int)length);
        }
        finally
        {
            _ = WindowsDeleteString(hstring);
        }
    }
}
