using System.Runtime.InteropServices;

namespace FluxKit.Platform.Native;

/// <summary>kernel32.dll imports used by more than one file.</summary>
internal static partial class Kernel32
{
    [LibraryImport("kernel32.dll")]
    public static partial uint GetCurrentThreadId();

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint GetModuleHandle(string? name);
}
