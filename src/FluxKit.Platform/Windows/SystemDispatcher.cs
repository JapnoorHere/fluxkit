using System.Runtime.InteropServices;

namespace FluxKit.Platform.Windows;

/// <summary>
/// Gives the current thread a <c>Windows.System.DispatcherQueue</c>. WinUI threads only have the Microsoft.UI one,
/// but the system compositor (used to make a window's backdrop fully transparent) needs the system queue.
/// </summary>
public static partial class SystemDispatcher
{
    private const int DqTypeThreadCurrent = 2;
    private const int DqTatComSta = 2;

    [ThreadStatic]
    private static global::Windows.System.DispatcherQueueController? _controller;

    public static void EnsureQueueForCurrentThread()
    {
        if (_controller is not null || global::Windows.System.DispatcherQueue.GetForCurrentThread() is not null)
        {
            return;
        }

        var options = new DispatcherQueueOptions { Size = Marshal.SizeOf<DispatcherQueueOptions>(), ThreadType = DqTypeThreadCurrent, ApartmentType = DqTatComSta };
        Marshal.ThrowExceptionForHR(CreateDispatcherQueueController(options, out nint controller));
        _controller = global::Windows.System.DispatcherQueueController.FromAbi(controller);
        _ = Marshal.Release(controller);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DispatcherQueueOptions
    {
        public int Size;
        public int ThreadType;
        public int ApartmentType;
    }

    [LibraryImport("CoreMessaging.dll")]
    private static partial int CreateDispatcherQueueController(DispatcherQueueOptions options, out nint controller);
}
