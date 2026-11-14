using FluxKit.Platform.Shell;
using FluxKit.Platform.Stats;
using FluxKit.Tools.FluxIsland;
using FluxKit.Tools.FluxIsland.Abstractions;
using FluxKit.Tools.FluxIsland.Activities;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>
/// The Glance page. While it is on screen (<see cref="SetLive"/>), it reads the PC's load once a second; otherwise
/// nothing runs. Quick actions act at once; lock and screen-off close the panel first so it isn't left open.
/// </summary>
public sealed partial class GlancePanel : UserControl
{
    private const string MicOnGlyph = "\xE720";
    private const string MicOffGlyph = "\xEC54";

    private readonly SystemStats _stats = new();
    private readonly DispatcherQueueTimer _sampler;
    private bool _micMuted;

    public GlancePanel()
    {
        InitializeComponent();
        var secondary = new SolidColorBrush(IslandPalette.TextSecondary);
        foreach (TextBlock label in new[] { CpuLabel, MemoryLabel, DiskLabel, BatteryLabel, DownText, UpText })
        {
            label.Foreground = secondary;
        }

        DownGlyph.Foreground = new SolidColorBrush(IslandPalette.Cyan);
        UpGlyph.Foreground = new SolidColorBrush(IslandPalette.Violet);
        var track = new SolidColorBrush(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF));
        foreach (ProgressRing ring in new[] { CpuRing, MemoryRing, DiskRing, BatteryRing })
        {
            ring.Background = track;
            ring.Minimum = 0;
            ring.Maximum = 100;
        }

        _sampler = DispatcherQueue.CreateTimer();
        _sampler.Interval = TimeSpan.FromSeconds(1);
        _sampler.Tick += (_, _) => Sample();

        ScreenshotButton.Click += (_, _) => Act(() => SessionActions.StartScreenshot());
        LockButton.Click += (_, _) => Act(() => SessionActions.Lock());
        DisplayOffButton.Click += (_, _) => Act(() => SessionActions.SleepDisplays());
        MicButton.Click += (_, _) => Commands?.ToggleMicMute();
        SettingsButton.Click += (_, _) => Act(() => AppHost.Current.ShowMainWindow(FluxIslandTool.Id));
        foreach (Button button in new[] { ScreenshotButton, LockButton, DisplayOffButton, MicButton, SettingsButton })
        {
            PressFeedback.Attach(button);
        }

        Unloaded += (_, _) => _sampler.Stop();
    }

    /// <summary>Asks the pill to close the panel, before an action that takes the user elsewhere.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>Unset in previews, where the buttons do nothing.</summary>
    public IIslandCommands? Commands { get; set; }

    internal UIElement[] Rows => [Gauges, Actions];

    internal void SetInset(double left) => ContentGrid.Margin = new Thickness(left, 0, 0, 0);

    /// <summary>Starts reading once a second while the page shows, and stops when it doesn't.</summary>
    public void SetLive(bool live)
    {
        if (live && !_sampler.IsRunning)
        {
            Sample();
            _sampler.Start();
        }
        else if (!live && _sampler.IsRunning)
        {
            _sampler.Stop();
        }
    }

    /// <summary>Takes the battery and microphone from the scene; the rest comes from sampling.</summary>
    public void Update(IslandScene scene)
    {
        PowerSnapshot power = scene.Power;
        BatteryGauge.Visibility = power.HasBattery ? Visibility.Visible : Visibility.Collapsed;
        if (power.HasBattery)
        {
            SetGauge(BatteryRing, BatteryText, BatteryLabel, power.Percent, power.IsPluggedIn ? IslandPalette.Green : power.Percent <= 20 ? IslandPalette.Red : IslandPalette.TextPrimary);
        }

        _micMuted = scene.MicMuted;
        MicGlyph.Glyph = _micMuted ? MicOffGlyph : MicOnGlyph;
        MicGlyph.Foreground = new SolidColorBrush(_micMuted ? IslandPalette.Red : IslandPalette.TextPrimary);
        string micName = Strings.Get(_micMuted ? "Island_GlanceUnmuteMic" : "Island_GlanceMuteMic");
        AutomationProperties.SetName(MicButton, micName);
        ToolTipService.SetToolTip(MicButton, micName);
    }

    /// <summary>Shows made-up numbers, for previews (nothing is sampled there).</summary>
    public void ShowSample(SystemReading reading) => Show(reading);

    private static Color LoadColor(double percent) => percent switch
    {
        >= 90 => IslandPalette.Red,
        >= 70 => IslandPalette.Orange,
        _ => IslandPalette.Cyan,
    };

    private static string FormatSpeed(long bytesPerSecond) => bytesPerSecond >= 1024 * 1024
        ? Strings.Format("Island_GlanceMbFormat", bytesPerSecond / (1024.0 * 1024.0))
        : Strings.Format("Island_GlanceKbFormat", bytesPerSecond / 1024.0);

    private static void SetGauge(ProgressRing ring, TextBlock text, TextBlock label, double percent, Color color)
    {
        int rounded = (int)Math.Round(Math.Clamp(percent, 0, 100));
        ring.Value = rounded;
        ring.Foreground = new SolidColorBrush(color);
        text.Text = rounded.ToString(System.Globalization.CultureInfo.CurrentCulture);
        AutomationProperties.SetName(ring, Strings.Format("Island_GlanceGaugeName", label.Text, rounded));
    }

    private void Sample()
    {
        try
        {
            Show(_stats.Sample());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Serilog.Log.Debug(ex, "Reading system load for Glance failed");
        }
    }

    private void Show(SystemReading reading)
    {
        SetGauge(CpuRing, CpuText, CpuLabel, reading.Cpu, LoadColor(reading.Cpu));
        SetGauge(MemoryRing, MemoryText, MemoryLabel, reading.Memory, LoadColor(reading.Memory));
        SetGauge(DiskRing, DiskText, DiskLabel, reading.Disk, LoadColor(reading.Disk));
        DownText.Text = FormatSpeed(reading.Download);
        UpText.Text = FormatSpeed(reading.Upload);
    }

    private void Act(Action action)
    {
        if (Commands is null)
        {
            return; // A preview.
        }

        CloseRequested?.Invoke(this, EventArgs.Empty);
        action();
    }
}
