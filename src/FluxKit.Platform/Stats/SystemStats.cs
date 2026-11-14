using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace FluxKit.Platform.Stats;

/// <summary>One reading of the PC's load. Percentages run from 0 to 100; speeds are bytes per second.</summary>
public readonly record struct SystemReading(double Cpu, double Memory, double Disk, long Upload, long Download);

/// <summary>
/// Reads CPU, memory, system-drive and network use when asked. Nothing runs in the background: the caller samples
/// (the Glance page does so once a second, only while it is on screen). CPU and network figures are measured since
/// the previous <see cref="Sample"/>, so the first reading shows them as zero.
/// </summary>
public sealed partial class SystemStats
{
    private ulong _idle;
    private ulong _total;
    private long _sent;
    private long _received;
    private long _at;

    public SystemReading Sample()
    {
        long now = Environment.TickCount64;
        double seconds = _at == 0 ? 0 : (now - _at) / 1000.0;
        _at = now;
        (long up, long down) = ReadNetwork(seconds);
        return new SystemReading(ReadCpu(), ReadMemory(), ReadDisk(), up, down);
    }

    private double ReadCpu()
    {
        if (!GetSystemTimes(out ulong idle, out ulong kernel, out ulong user))
        {
            return 0;
        }

        // Kernel time includes idle time.
        ulong total = kernel + user;
        ulong idleDelta = idle - _idle;
        ulong totalDelta = total - _total;
        bool first = _total == 0;
        _idle = idle;
        _total = total;
        return first || totalDelta == 0 ? 0 : Math.Clamp(100.0 * (totalDelta - idleDelta) / totalDelta, 0, 100);
    }

    private static double ReadMemory()
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        return GlobalMemoryStatusEx(ref status) ? status.MemoryLoad : 0;
    }

    private static double ReadDisk()
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\");
            return drive.TotalSize == 0 ? 0 : 100.0 * (drive.TotalSize - drive.TotalFreeSpace) / drive.TotalSize;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return 0;
        }
    }

    private (long Up, long Down) ReadNetwork(double seconds)
    {
        long sent = 0;
        long received = 0;
        try
        {
            foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up || adapter.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                {
                    continue;
                }

                IPInterfaceStatistics stats = adapter.GetIPStatistics();
                sent += stats.BytesSent;
                received += stats.BytesReceived;
            }
        }
        catch (NetworkInformationException)
        {
            return (0, 0);
        }

        bool first = seconds <= 0 || (_sent == 0 && _received == 0);
        long up = first ? 0 : (long)(Math.Max(0, sent - _sent) / seconds);
        long down = first ? 0 : (long)(Math.Max(0, received - _received) / seconds);
        _sent = sent;
        _received = received;
        return (up, down);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetSystemTimes(out ulong idle, out ulong kernel, out ulong user);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalMemoryStatusEx(ref MemoryStatusEx status);
}
