// FluxKit DragHarness: end-to-end test of a real OLE file drag across virtual desktops.
//
//   DragHarness --switch <mode> --target <1-based desktop> [--probe <DropProbe.exe>] [--vdprobe <VdProbe.exe>]
//               [--scenario drop|cancel|release-on-overlay|window] [--out <results.json>]
//
// Scenario "window" (with --switch fluxkit) drags the harness window by its title bar instead of a file
// and checks that the window travels to the target desktop.
//
// Modes:  com-direct   another process (VdProbe) switches desktops mid-drag      (H2)
//         manual-keys  Ctrl+Win+Arrow is injected mid-drag                          (H1/H6)
//         fluxkit      FluxKit must be running; the cursor dwells on its selector   (E2E)
//
// Exit code 0 = the exact file arrived in DropProbe on the target desktop (or, for cancel scenarios,
// nothing arrived and the source file still exists).
using System.Diagnostics;
using System.Text.Json;
using FluxKit.Platform.Input;
using FluxKit.Platform.VirtualDesktop;
using FluxKit.Platform.VirtualDesktop.Interop;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        string Arg(string name, string fallback)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }

        var options = new HarnessOptions(
            Arg("--switch", "com-direct"),
            int.Parse(Arg("--target", "2")),
            Arg("--scenario", "drop"),
            Arg("--probe", Sibling("FluxKit.DropProbe")),
            Arg("--vdprobe", Sibling("VdProbe")),
            Arg("--out", Path.Combine(AppContext.BaseDirectory, "harness-result.json")),
            int.Parse(Arg("--dwell", "900")));

        ApplicationConfiguration.Initialize();
        var source = new SourceForm();
        int exitCode = 1;
        source.Shown += async (_, _) =>
        {
            try
            {
                exitCode = await Task.Run(() => new HarnessRun(options, source).Execute());
            }
            finally
            {
                source.Close();
            }
        };
        Application.Run(source);
        return exitCode;
    }

    // Tools are built side by side under tools/ and spikes/; resolve their Debug output by convention.
    private static string Sibling(string project)
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        string folder = project == "VdProbe" ? Path.Combine(root, "spikes", project) : Path.Combine(root, "tools", project);
        string tfm = new DirectoryInfo(AppContext.BaseDirectory).Name;
        return Path.Combine(folder, "bin", "Debug", tfm, project + ".exe");
    }
}

internal sealed record HarnessOptions(string Mode, int Target, string Scenario, string ProbePath, string VdProbePath, string OutPath, int DwellMs);

/// <summary>A small window that starts a real OLE DoDragDrop with a CF_HDROP payload.</summary>
internal sealed class SourceForm : Form
{
    public SourceForm()
    {
        Text = "FluxKit DragHarness source";
        StartPosition = FormStartPosition.Manual;
        Bounds = new Rectangle(900, 200, 360, 240);
        TopMost = true;
        FilePath = Path.Combine(Path.GetTempPath(), $"fluxkit-e2e-{Guid.NewGuid():N}.txt");
        File.WriteAllText(FilePath, "FluxKit end-to-end test payload.");
        var label = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Text = "Drag source\n" + Path.GetFileName(FilePath) };
        label.MouseMove += OnMouseMove;
        Controls.Add(label);
    }

    public string FilePath { get; }

    public DragDropEffects? Result { get; private set; }

    public bool DragStarted { get; private set; }

    public Point Center => Invoke(() => new Point(Bounds.Left + (Bounds.Width / 2), Bounds.Top + (Bounds.Height / 2)));

    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || DragStarted)
        {
            return;
        }

        DragStarted = true;
        var data = new DataObject(DataFormats.FileDrop, new[] { FilePath });
        Result = DoDragDrop(data, DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link);
    }
}

internal sealed class HarnessRun(HarnessOptions options, SourceForm source)
{
    private static readonly Rectangle ProbeBounds = new(200, 200, 520, 380);
    private string _dropWindow = "n/a";

    private static string DescribeWindowAt(Point p)
    {
        nint hwnd = WindowFromPoint(new POINT { X = p.X, Y = p.Y });
        _ = GetWindowThreadProcessId(hwnd, out uint pid);
        var cls = new System.Text.StringBuilder(128);
        _ = GetClassName(hwnd, cls, cls.Capacity);
        string proc;
        try
        {
            proc = Process.GetProcessById((int)pid).ProcessName;
        }
        catch (ArgumentException)
        {
            proc = "?";
        }

        return $"{proc}/{cls}";
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern nint WindowFromPoint(POINT point);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int GetClassName(nint hwnd, System.Text.StringBuilder name, int max);

    public int Execute()
    {
        IReadOnlyList<VirtualDesktopInfo> desktops = RegistryDesktopReader.Read();
        VirtualDesktopInfo origin = desktops.First(d => d.IsCurrent);
        if (options.Target < 1 || options.Target > desktops.Count || desktops[options.Target - 1].IsCurrent)
        {
            return Report(false, "Target must be a different, existing desktop.");
        }

        VirtualDesktopInfo target = desktops[options.Target - 1];
        if (options.Scenario == "window")
        {
            return RunWindowScenario(origin, target, desktops);
        }

        string logPath = Path.Combine(Path.GetTempPath(), $"fluxkit-probe-{Guid.NewGuid():N}.jsonl");
        using Process probe = Process.Start(options.ProbePath,
            ["--title", "DropProbe (target)", "--desktop", target.Id.ToString("B"), "--bounds", $"{ProbeBounds.X},{ProbeBounds.Y},{ProbeBounds.Width},{ProbeBounds.Height}", "--log", logPath])!;
        try
        {
            if (!WaitFor(() => File.Exists(logPath) && File.ReadAllText(logPath).Contains("\"moved\""), 8000))
            {
                return Report(false, "DropProbe did not start or could not move to the target desktop.");
            }

            // 1. Start a real drag from the source window.
            Point start = source.Center;
            InputSynthesizer.MoveTo(start.X, start.Y);
            Thread.Sleep(150);
            InputSynthesizer.LeftDown();
            for (int i = 1; i <= 12; i++)
            {
                InputSynthesizer.MoveTo(start.X - (i * 6), start.Y + (i * 2));
                Thread.Sleep(15);
            }

            if (!WaitFor(() => source.DragStarted, 2000))
            {
                InputSynthesizer.LeftUp();
                return Report(false, "DoDragDrop never started.");
            }

            Thread.Sleep(300);

            // 2. Switch desktops while the drag is active.
            string switchNote = SwitchDuringDrag(origin, target, desktops);
            bool onTarget = WaitFor(() => RegistryDesktopReader.ReadCurrentId() == target.Id, 3000);

            // 3. Finish the scenario.
            Point drop = new(ProbeBounds.X + (ProbeBounds.Width / 2), ProbeBounds.Y + (ProbeBounds.Height / 2));
            if (options.Scenario == "cancel")
            {
                InputSynthesizer.Key(InputSynthesizer.VkEscape, up: false);
                InputSynthesizer.Key(InputSynthesizer.VkEscape, up: true);
                Thread.Sleep(200);
                InputSynthesizer.LeftUp();
            }
            else if (options.Scenario != "release-on-overlay")
            {
                Glide(drop);
                Thread.Sleep(400);
                _dropWindow = DescribeWindowAt(drop);
                InputSynthesizer.LeftUp();
            }
            else
            {
                InputSynthesizer.LeftUp();
            }

            bool delivered = WaitFor(() => File.ReadAllText(logPath).Contains(JsonEncodedPath(source.FilePath)), options.Scenario == "drop" ? 5000 : 1500);
            WaitFor(() => source.Result is not null, 2000);
            bool sourceIntact = File.Exists(source.FilePath);
            bool pass = options.Scenario == "drop" ? delivered && sourceIntact : !delivered && sourceIntact;
            return Report(pass, $"switch={switchNote}; reachedTarget={onTarget}; delivered={delivered}; dragResult={source.Result}; sourceIntact={sourceIntact}; dropWindow={_dropWindow}");
        }
        finally
        {
            try
            {
                probe.Kill();
            }
            catch (InvalidOperationException)
            {
            }

            RunVdProbe(origin.Index + 1);
            try
            {
                File.Delete(source.FilePath);
            }
            catch (IOException)
            {
            }
        }
    }

    /// <summary>
    /// Drags the source window by its title bar through the FluxKit selector and checks that the window
    /// ends up on the target desktop with the user (FluxKit "move app windows").
    /// </summary>
    private int RunWindowScenario(VirtualDesktopInfo origin, VirtualDesktopInfo target, IReadOnlyList<VirtualDesktopInfo> desktops)
    {
        nint hwnd = source.Invoke(() => source.Handle);
        Rectangle bounds = source.Invoke(() => source.Bounds);
        try
        {
            var grip = new Point(bounds.Left + 120, bounds.Top + 14); // inside the caption, away from the buttons
            InputSynthesizer.MoveTo(grip.X, grip.Y);
            Thread.Sleep(150);
            InputSynthesizer.LeftDown();
            for (int i = 1; i <= 12; i++)
            {
                InputSynthesizer.MoveTo(grip.X + (i * 6), grip.Y + (i * 4));
                Thread.Sleep(15);
            }

            Thread.Sleep(300);
            string switchNote = SwitchDuringDrag(origin, target, desktops);
            bool onTarget = WaitFor(() => RegistryDesktopReader.ReadCurrentId() == target.Id, 3000);

            // Keep dragging on the new desktop to prove the move loop survived, then release.
            GetCursorPos(out POINT at);
            Glide(new Point(at.X - 200, at.Y - 300));
            Thread.Sleep(200);
            Rectangle afterMove = source.Invoke(() => source.Bounds);
            InputSynthesizer.LeftUp();
            Thread.Sleep(300);

            Guid? windowDesktop = PublicVirtualDesktopManager.GetWindowDesktopId(hwnd);
            bool windowOnTarget = windowDesktop == target.Id;
            bool stillDragging = afterMove.Location != bounds.Location;
            bool pass = onTarget && windowOnTarget && stillDragging;
            return Report(pass, $"switch={switchNote}; reachedTarget={onTarget}; windowOnTarget={windowOnTarget}; windowFollowedCursorAfterSwitch={stillDragging}");
        }
        finally
        {
            RunVdProbe(origin.Index + 1);
        }
    }

    private string SwitchDuringDrag(VirtualDesktopInfo origin, VirtualDesktopInfo target, IReadOnlyList<VirtualDesktopInfo> desktops)
    {
        switch (options.Mode)
        {
            case "com-direct":
                return $"VdProbe exit={RunVdProbe(target.Index + 1)}";
            case "manual-keys":
                int delta = target.Index - origin.Index;
                ushort arrow = delta > 0 ? InputSynthesizer.VkRight : InputSynthesizer.VkLeft;
                for (int i = 0; i < Math.Abs(delta); i++)
                {
                    InputSynthesizer.KeyCombo(0, InputSynthesizer.VkControl, InputSynthesizer.VkLeftWindows, arrow);
                    Thread.Sleep(250);
                }

                return $"keys x{Math.Abs(delta)}";
            case "fluxkit":
                // Hover the FluxKit selector card for the target desktop, located from the real selector window
                // (cards span the window evenly below the header; constants match SelectorLayout).
                nint selector = 0;
                if (!WaitFor(() => (selector = FindWindow(null, "FluxKit desktop selector")) != 0 && IsWindowVisible(selector), 3000))
                {
                    return "fluxkit selector never appeared";
                }

                Thread.Sleep(250); // let the entrance animation settle
                GetWindowRect(selector, out RECT r);
                double scale = GetDpiForWindow(selector) / 96.0;
                const int gap = 12, padding = 16, header = 40;
                int count = desktops.Count;
                double cardWidth = ((r.Right - r.Left) - (2 * padding * scale) - ((count - 1) * gap * scale)) / count;
                int cardX = r.Left + (int)((padding * scale) + (target.Index * (cardWidth + (gap * scale))) + (cardWidth / 2));
                int cardTop = r.Top + (int)((padding + header) * scale);
                int cardY = cardTop + ((r.Bottom - (int)(padding * scale) - cardTop) / 2);
                Glide(new Point(cardX, cardY));
                Thread.Sleep(options.DwellMs);
                return $"fluxkit selector=[{r.Left},{r.Top},{r.Right},{r.Bottom}] card@{cardX},{cardY}";
            default:
                throw new ArgumentException($"Unknown mode {options.Mode}");
        }
    }

    private static double DpiScale()
    {
        using Graphics g = Graphics.FromHwnd(0);
        return g.DpiX / 96.0;
    }

    private static void Glide(Point to)
    {
        GetCursorPos(out POINT from);
        for (int i = 1; i <= 15; i++)
        {
            InputSynthesizer.MoveTo(from.X + ((to.X - from.X) * i / 15), from.Y + ((to.Y - from.Y) * i / 15));
            Thread.Sleep(20);
        }
    }

    private int RunVdProbe(int desktopNumber)
    {
        using Process p = Process.Start(new ProcessStartInfo(options.VdProbePath, $"switch {desktopNumber}") { CreateNoWindow = true, UseShellExecute = false })!;
        p.WaitForExit(5000);
        return p.ExitCode;
    }

    private int Report(bool pass, string details)
    {
        var result = new { pass, mode = options.Mode, scenario = options.Scenario, target = options.Target, details, ts = DateTime.UtcNow.ToString("O") };
        File.WriteAllText(options.OutPath, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(JsonSerializer.Serialize(result));
        return pass ? 0 : 1;
    }

    private static string JsonEncodedPath(string path) => JsonSerializer.Serialize(path).Trim('"');

    private static bool WaitFor(Func<bool> condition, int timeoutMs)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            try
            {
                if (condition())
                {
                    return true;
                }
            }
            catch (IOException)
            {
            }

            Thread.Sleep(50);
        }

        return false;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT point);

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "FindWindowW", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern nint FindWindow(string? className, string windowName);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool IsWindowVisible(nint hwnd);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint hwnd, out RECT rect);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hwnd);

    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private struct POINT
    {
        public int X;
        public int Y;
    }
}
