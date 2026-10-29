using FluxKit.App.Shell;
using FluxKit.Core.Tools;
using FluxKit.Tools.DragToSwitch;
using FluxKit.Tools.DragToSwitch.Interaction;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.ApplicationModel.DataTransfer;

namespace FluxKit.App.Modules.DragToSwitch;

/// <summary>Settings page for Drag-to-Switch (Docs/PLAN.md §8.3.2).</summary>
public sealed partial class DragToSwitchPage : Page
{
    private readonly AppHost _host = AppHost.Current;
    private readonly Storyboard _dwellPreview = new() { RepeatBehavior = RepeatBehavior.Forever };
    private bool _loading = true;

    public DragToSwitchPage()
    {
        InitializeComponent();
        Load();
        Loaded += (_, _) =>
        {
            _host.StatusChanged += OnStatusChanged;
            _host.Desktops.DesktopsChanged += OnDesktopsChanged;
        };
        Unloaded += (_, _) =>
        {
            _host.StatusChanged -= OnStatusChanged;
            _host.Desktops.DesktopsChanged -= OnDesktopsChanged;
            _dwellPreview.Stop();
        };
    }

    private DragToSwitchSettings Current => _host.Settings.Read<DragToSwitchSettings>(DragToSwitchSettings.Section);

    private void Load()
    {
        _loading = true;
        DragToSwitchSettings s = Current;
        EnabledToggle.IsOn = _host.Registry.IsEnabled(DragToSwitchTool.Id);
        TriggerCombo.SelectedIndex = (int)s.Trigger;
        DelaySlider.Value = s.HoverDelayMs;
        DelayText.Text = $"{s.HoverDelayMs} ms";
        SourcesCombo.SelectedIndex = (int)s.Sources;
        NumberKeysToggle.IsOn = s.NumberKeysEnabled;
        MoveWindowsToggle.IsOn = s.MoveWindowsEnabled;
        SingleDesktopToggle.IsOn = s.ShowWithSingleDesktop;
        PositionCombo.SelectedIndex = (int)s.OverlayPosition;
        SizeRadios.SelectedIndex = (int)s.OverlaySize;
        PreviewCombo.SelectedIndex = (int)s.PreviewStyle;
        OverlayThemeCombo.SelectedIndex = (int)s.OverlayTheme;
        AnimationsToggle.IsOn = s.AnimationsEnabled;
        CollapseToggle.IsOn = s.CollapseAfterSwitch;
        ReturnToggle.IsOn = s.ReturnToOriginOnCancel;
        AnimationsRow.Description = new Windows.UI.ViewManagement.UISettings().AnimationsEnabled
            ? "Animate the picker when it opens, switches and closes."
            : "Animation effects are turned off in Windows, so the picker won't animate.";
        SourcesRow.Description = SourcesDescription(s.Sources);
        RestartDwellPreview(s.HoverDelayMs);
        RefreshStatus();
        _loading = false;
    }

    private void RefreshStatus()
    {
        ToolState state = _host.DragToSwitch.State;
        bool enabled = _host.Registry.IsEnabled(DragToSwitchTool.Id);
        OptionsPanel.Opacity = enabled ? 1 : 0.5;
        OptionsPanel.IsHitTestVisible = enabled;
        int count = _host.Desktops.Desktops.Count;
        (InfoBarSeverity severity, string title, string message) = state.Status switch
        {
            ToolStatus.Running => (InfoBarSeverity.Success, "Ready", $"{count} desktops found."),
            ToolStatus.Degraded => (InfoBarSeverity.Warning, "Limited mode", state.Detail ?? string.Empty),
            ToolStatus.Faulted => (InfoBarSeverity.Error, "Stopped", state.Detail ?? "Open the log folder for details."),
            ToolStatus.Starting => (InfoBarSeverity.Informational, "Starting", string.Empty),
            _ => (InfoBarSeverity.Informational, "Off", "Turn on Drag-to-Switch to use the desktop picker."),
        };
        if (state.IsActive && count < 2)
        {
            (severity, title, message) = (InfoBarSeverity.Informational, "You have only one desktop", "Add another with Ctrl+Win+D. The picker appears once you have two or more.");
        }

        StatusBar.Severity = severity;
        StatusBar.Title = title;
        StatusBar.Message = message;

        bool canMove = _host.Desktops.CanMoveWindows;
        MoveWindowsToggle.IsEnabled = canMove;
        MoveWindowsRow.Description = canMove
            ? "Drag a window by its title bar and hold it over a desktop. The window moves there with you."
            : "Not available in limited mode. Moving windows needs full virtual desktop support.";

        BackendText.Text = _host.Desktops.BackendDescription;
        DesktopsText.Text = count == 0 ? "None" : $"{count}: " + string.Join(", ", _host.Desktops.Desktops.Select(d => d.Name + (d.IsCurrent ? " (current)" : string.Empty)));
        SessionSummary? last = _host.DragToSwitch.LastSession;
        LastSessionText.Text = last is null
            ? "No drags yet."
            : $"{last.StartedAt.ToLocalTime():t} · from {last.SourceProcess} · " +
              (last.ToDesktop is int to ? $"switched from desktop {last.FromDesktop} to desktop {to} in {last.SwitchMs} ms · " : "didn't switch · ") +
              last.End switch { SessionEnd.Released => "dropped", SessionEnd.Cancelled => "cancelled", _ => "stopped by FluxKit" };
    }

    private void OnStatusChanged(object? sender, EventArgs e) => RefreshStatus();

    private void OnDesktopsChanged(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(RefreshStatus);

    private async void OnEnabledToggled(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        await _host.SetToolEnabledAsync(DragToSwitchTool.Id, EnabledToggle.IsOn);
        RefreshStatus();
    }

    private void OnDelayChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        // Setting Minimum in XAML raises ValueChanged inside InitializeComponent, before later controls exist.
        if (DelayText is null)
        {
            return;
        }

        int delay = (int)e.NewValue;
        DelayText.Text = $"{delay} ms";
        RestartDwellPreview(delay);
        Save(s => s.HoverDelayMs = delay);
    }

    private void OnChanged(object sender, object e)
    {
        if (ReturnToggle is null)
        {
            return; // raised during InitializeComponent
        }

        Save(s =>
        {
            s.Trigger = (SelectorTrigger)Math.Max(0, TriggerCombo.SelectedIndex);
            s.Sources = (DragSources)Math.Max(0, SourcesCombo.SelectedIndex);
            s.NumberKeysEnabled = NumberKeysToggle.IsOn;
            s.MoveWindowsEnabled = MoveWindowsToggle.IsOn;
            s.ShowWithSingleDesktop = SingleDesktopToggle.IsOn;
            s.OverlayPosition = (OverlayPosition)Math.Max(0, PositionCombo.SelectedIndex);
            s.OverlaySize = (OverlaySize)Math.Max(0, SizeRadios.SelectedIndex);
            s.PreviewStyle = (PreviewStyle)Math.Max(0, PreviewCombo.SelectedIndex);
            s.OverlayTheme = (OverlayTheme)Math.Max(0, OverlayThemeCombo.SelectedIndex);
            s.AnimationsEnabled = AnimationsToggle.IsOn;
            s.CollapseAfterSwitch = CollapseToggle.IsOn;
            s.ReturnToOriginOnCancel = ReturnToggle.IsOn;
        });
        SourcesRow.Description = SourcesDescription((DragSources)Math.Max(0, SourcesCombo.SelectedIndex));
    }

    private static string SourcesDescription(DragSources sources) => sources == DragSources.AnyApp
        ? "Files dragged from any app show the picker. Selected text, links and web images don't."
        : "The picker appears only for files dragged from File Explorer or the desktop.";

    private void Save(Action<DragToSwitchSettings> apply)
    {
        if (_loading)
        {
            return;
        }

        _ = _host.Settings.UpdateAsync<DragToSwitchSettings>(DragToSwitchSettings.Section, s =>
        {
            apply(s);
            return s;
        });
    }

    private void RestartDwellPreview(int delayMs)
    {
        _dwellPreview.Stop();
        _dwellPreview.Children.Clear();
        var grow = new DoubleAnimation { From = 0, To = 1, Duration = TimeSpan.FromMilliseconds(delayMs) };
        Storyboard.SetTarget(grow, DwellPreviewScale);
        Storyboard.SetTargetProperty(grow, "ScaleX");
        _dwellPreview.Children.Add(grow);
        _dwellPreview.Duration = TimeSpan.FromMilliseconds(delayMs + 700);
        _dwellPreview.Begin();
    }

    private void OnPreview(object sender, RoutedEventArgs e) => _host.Selector?.ShowPreview(Current);

    private async void OnRedetect(object sender, RoutedEventArgs e)
    {
        await _host.Desktops.ReinitializeAsync();
        bool enabled = _host.Registry.IsEnabled(DragToSwitchTool.Id);
        if (enabled)
        {
            await _host.SetToolEnabledAsync(DragToSwitchTool.Id, false);
            await _host.SetToolEnabledAsync(DragToSwitchTool.Id, true);
        }

        RefreshStatus();
    }

    private void OnOpenLogs(object sender, RoutedEventArgs e) => AppHost.OpenFolder(_host.LogDirectory);

    private async void OnCopyDiagnostics(object sender, RoutedEventArgs e)
    {
        var package = new DataPackage();
        package.SetText(Diagnostics.Build(_host));
        Clipboard.SetContent(package);
        CopyDiagnosticsButton.Content = "Copied";
        await Task.Delay(2000);
        CopyDiagnosticsButton.Content = "Copy diagnostic info";
    }
}
