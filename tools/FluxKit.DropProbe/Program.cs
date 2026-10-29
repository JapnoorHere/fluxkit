// FluxKit DropProbe: a plain Windows drop target used to verify real file delivery.
// Flags: --title <text> --desktop <guid> --bounds x,y,w,h --log <path>
// Every drop is appended to the log as one JSON line. It always reports Copy, so sources are never deleted.
using System.Text.Json;
using FluxKit.Platform.VirtualDesktop.Interop;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        string Arg(string name, string fallback)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }

        string logPath = Arg("--log", Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FluxKit", "DropProbe", "drops.jsonl"));
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

        ApplicationConfiguration.Initialize();
        var form = new ProbeForm(Arg("--title", "FluxKit DropProbe"), logPath);

        string[] bounds = Arg("--bounds", "").Split(',', StringSplitOptions.RemoveEmptyEntries);
        if (bounds.Length == 4)
        {
            form.StartPosition = FormStartPosition.Manual;
            form.Bounds = new Rectangle(int.Parse(bounds[0]), int.Parse(bounds[1]), int.Parse(bounds[2]), int.Parse(bounds[3]));
        }

        if (Guid.TryParse(Arg("--desktop", ""), out Guid desktop))
        {
            form.Shown += (_, _) => form.Log("moved", new { desktopId = desktop, ok = PublicVirtualDesktopManager.MoveOwnWindowToDesktop(form.Handle, desktop) });
        }

        Application.Run(form);
    }
}

internal sealed class ProbeForm : Form
{
    private readonly string _logPath;
    private readonly ListBox _list = new() { Dock = DockStyle.Fill, IntegralHeight = false };

    public ProbeForm(string title, string logPath)
    {
        _logPath = logPath;
        Text = title;
        Size = new Size(520, 380);
        AllowDrop = true;
        TopMost = true; // guarantee the probe, not another app, is under the cursor at drop time
        var hint = new Label { Dock = DockStyle.Top, Height = 36, Text = "Drop files here. Received paths are listed below and logged.", TextAlign = ContentAlignment.MiddleCenter };
        Controls.Add(_list);
        Controls.Add(hint);
        _list.AllowDrop = true;
        _list.DragEnter += OnDragEnter;
        _list.DragDrop += OnDragDrop;
        DragEnter += OnDragEnter;
        DragDrop += OnDragDrop;
        Shown += (_, _) => Log("ready", new { bounds = new[] { Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height } });

        // Windows does not keep a window moved to another desktop above that desktop's windows;
        // re-assert topmost whenever our desktop becomes the visible one.
        var raise = new System.Windows.Forms.Timer { Interval = 100 };
        raise.Tick += (_, _) =>
        {
            Guid? mine = PublicVirtualDesktopManager.GetWindowDesktopId(Handle);
            if (mine is not null && mine == FluxKit.Platform.VirtualDesktop.RegistryDesktopReader.ReadCurrentId())
            {
                TopMost = false;
                TopMost = true;
            }
        };
        raise.Start();
    }

    public void Log(string evt, object payload)
    {
        var line = new Dictionary<string, object?>
        {
            ["event"] = evt,
            ["ts"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            ["pid"] = Environment.ProcessId,
            ["desktopId"] = PublicVirtualDesktopManager.GetWindowDesktopId(Handle),
            ["data"] = payload,
        };
        File.AppendAllText(_logPath, JsonSerializer.Serialize(line) + Environment.NewLine);
    }

    private void OnDragEnter(object? sender, DragEventArgs e) =>
        e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Copy : DragDropEffects.None;

    private void OnDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] paths)
        {
            return;
        }

        e.Effect = DragDropEffects.Copy;
        foreach (string path in paths)
        {
            _list.Items.Add($"{DateTime.Now:HH:mm:ss}  {path}");
        }

        Log("drop", new { effect = "Copy", paths });
    }
}
