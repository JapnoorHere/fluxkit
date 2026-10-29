// VdProbe: Phase 1 hypothesis H7.
// Usage: VdProbe            -> list layouts, desktops, validate against the registry
//        VdProbe switch N   -> switch to desktop N (1-based) and verify via the registry
//        VdProbe move-window HWND N -> move another process's window to desktop N, verify with the public API
using FluxKit.Platform.VirtualDesktop;
using FluxKit.Platform.VirtualDesktop.Interop;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        Console.WriteLine($"OS: {Environment.OSVersion.Version}");

        IReadOnlyList<VirtualDesktopInfo> registry = RegistryDesktopReader.Read();
        Console.WriteLine($"Registry: {registry.Count} desktops");
        foreach (VirtualDesktopInfo d in registry)
        {
            Console.WriteLine($"  [{d.Index + 1}] {d.Id:B} {d.Name}{(d.IsCurrent ? "  (current)" : "")}");
        }

        using ShellComSession? session = ShellComSession.TryCreate(out string diagnostics);
        Console.WriteLine($"COM probe: {diagnostics}");
        if (session is null)
        {
            Console.WriteLine("RESULT: no COM layout resolved -> keyboard fallback required");
            return 2;
        }

        IReadOnlyList<Guid> comIds = session.GetDesktopIds();
        Guid comCurrent = session.GetCurrentDesktopId();
        bool valid = session.GetCount() == registry.Count
            && comIds.SequenceEqual(registry.Select(d => d.Id))
            && comCurrent == RegistryDesktopReader.ReadCurrentId();
        Console.WriteLine($"COM: layout={session.Layout.Name} count={session.GetCount()} current={comCurrent:B} canPin={session.CanPin}");
        Console.WriteLine($"VALIDATION: {(valid ? "PASS" : "FAIL")}");

        if (args is ["switch", var n] && int.TryParse(n, out int target) && target >= 1 && target <= comIds.Count)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            bool switched = session.SwitchTo(comIds[target - 1]);
            Guid now = Guid.Empty;
            while (sw.ElapsedMilliseconds < 1000 && (now = RegistryDesktopReader.ReadCurrentId()) != comIds[target - 1])
            {
                Thread.Sleep(20);
            }

            Console.WriteLine($"SWITCH to {target}: called={switched} registryConfirmed={now == comIds[target - 1]} in {sw.ElapsedMilliseconds} ms");
        }

        if (args is ["move-window", var h, var m] && long.TryParse(h, out long hwnd) && int.TryParse(m, out int desk) && desk >= 1 && desk <= comIds.Count)
        {
            string? error = session.MoveWindow((nint)hwnd, comIds[desk - 1]);
            Guid? actual = PublicVirtualDesktopManager.GetWindowDesktopId((nint)hwnd);
            bool ok = error is null && actual == comIds[desk - 1];
            Console.WriteLine($"MOVE-WINDOW 0x{hwnd:X} to {desk}: error={error ?? "none"} publicApiConfirms={actual == comIds[desk - 1]}");
            return ok ? 0 : 3;
        }

        return valid ? 0 : 1;
    }
}
