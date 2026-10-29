using FluxKit.App.Controls;
using FluxKit.App.Shell;
using FluxKit.Core.Tools;
using FluxKit.Tools.DragToSwitch;
using FluxKit.Tools.DragToSwitch.Interaction;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.ApplicationModel.DataTransfer;

namespace FluxKit.App.Modules.DragToSwitch;

/// <summary>Settings page for Drag-to-Switch: a live picker preview beside the settings groups.</summary>
public sealed partial class DragToSwitchPage : Page
{
    private readonly AppHost _host = AppHost.Current;
    private bool _loading = true;

    public DragToSwitchPage()
    {
        InitializeComponent();
        CopyDiagnosticsButton.Content = Strings.Get("DragToSwitch_CopyDiagnostics");
        Load();
        Loaded += (_, _) =>
        {
            _host.StatusChanged += OnStatusChanged;
            _host.ThemeChanged += OnThemeChanged;
            _host.Desktops.DesktopsChanged += OnDesktopsChanged;
        };
        Unloaded += (_, _) =>
        {
            _host.StatusChanged -= OnStatusChanged;
            _host.ThemeChanged -= OnThemeChanged;
            _host.Desktops.DesktopsChanged -= OnDesktopsChanged;
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
        DelayText.Text = Strings.Format("DragToSwitch_DelayFormat", s.HoverDelayMs);
        SourcesCombo.SelectedIndex = (int)s.Sources;
        NumberKeysToggle.IsOn = s.NumberKeysEnabled;
        MoveWindowsToggle.IsOn = s.MoveWindowsEnabled;
        SingleDesktopToggle.IsOn = s.ShowWithSingleDesktop;
        PositionCombo.SelectedIndex = (int)s.OverlayPosition;
        SizeCombo.SelectedIndex = (int)s.OverlaySize;
        PreviewCombo.SelectedIndex = (int)s.PreviewStyle;
        OverlayThemeCombo.SelectedIndex = (int)s.OverlayTheme;
        AnimationsToggle.IsOn = s.AnimationsEnabled;
        CollapseToggle.IsOn = s.CollapseAfterSwitch;
        ReturnToggle.IsOn = s.ReturnToOriginOnCancel;
        AnimationsRow.Description = Strings.Get(new Windows.UI.ViewManagement.UISettings().AnimationsEnabled
            ? "DragToSwitch_AnimationsOn"
            : "DragToSwitch_AnimationsOffInWindows");
        SourcesRow.Description = SourcesDescription(s.Sources);
        RefreshStatus();
        RefreshPreview(s);
        _loading = false;
    }

    private void RefreshStatus()
    {
        ToolState state = _host.DragToSwitch.State;
        bool enabled = _host.Registry.IsEnabled(DragToSwitchTool.Id);
        OptionsPanel.Opacity = enabled ? 1 : 0.5;
        OptionsPanel.IsHitTestVisible = enabled;
        int count = _host.Desktops.Desktops.Count;

        Header.StatusTone = StatusTones.For(state.Status);
        Header.StatusText = state.Status switch
        {
            ToolStatus.Running => Strings.Format("DragToSwitch_StatusReadyFormat", count),
            ToolStatus.Degraded => Strings.Get("DragToSwitch_StatusLimited"),
            ToolStatus.Faulted => Strings.Get("DragToSwitch_StatusStopped"),
            ToolStatus.Starting => Strings.Get("DragToSwitch_StatusStarting"),
            _ => Strings.Get("DragToSwitch_StatusOff"),
        };

        // The InfoBar only appears when something needs the user's attention.
        (bool open, InfoBarSeverity severity, string title, string message) = state.Status switch
        {
            ToolStatus.Degraded => (true, InfoBarSeverity.Warning, Strings.Get("DragToSwitch_StatusLimited"), state.Detail ?? string.Empty),
            ToolStatus.Faulted => (true, InfoBarSeverity.Error, Strings.Get("DragToSwitch_StatusStopped"), state.Detail ?? Strings.Get("DragToSwitch_StoppedDetail")),
            _ when state.IsActive && count < 2 => (true, InfoBarSeverity.Informational, Strings.Get("DragToSwitch_OneDesktopTitle"), Strings.Get("DragToSwitch_OneDesktopText")),
            _ => (false, InfoBarSeverity.Informational, string.Empty, string.Empty),
        };
        StatusBar.IsOpen = open;
        StatusBar.Severity = severity;
        StatusBar.Title = title;
        StatusBar.Message = message;

        bool canMove = _host.Desktops.CanMoveWindows;
        MoveWindowsToggle.IsEnabled = canMove;
        MoveWindowsRow.Description = Strings.Get(canMove ? "DragToSwitch_MoveWindowsOn" : "DragToSwitch_MoveWindowsLimited");

        BackendText.Text = _host.Desktops.BackendDescription;
        DesktopsText.Text = count == 0
            ? Strings.Get("DragToSwitch_DesktopsNone")
            : Strings.Format(
                "DragToSwitch_DesktopsFormat",
                count,
                string.Join(", ", _host.Desktops.Desktops.Select(d => d.IsCurrent ? Strings.Format("DragToSwitch_DesktopCurrentFormat", d.Name) : d.Name)));
        LastSessionText.Text = LastSessionDescription(_host.DragToSwitch.LastSession);
    }

    private static string LastSessionDescription(SessionSummary? last)
    {
        if (last is null)
        {
            return Strings.Get("DragToSwitch_LastDragNone");
        }

        string outcome = Strings.Get(last.End switch
        {
            SessionEnd.Released => "DragToSwitch_EndDropped",
            SessionEnd.Cancelled => "DragToSwitch_EndCancelled",
            _ => "DragToSwitch_EndStopped",
        });
        string time = last.StartedAt.ToLocalTime().ToString("t", System.Globalization.CultureInfo.CurrentCulture);
        return last.ToDesktop is int to
            ? Strings.Format("DragToSwitch_LastDragSwitchedFormat", time, last.SourceProcess, last.FromDesktop, to, last.SwitchMs, outcome)
            : Strings.Format("DragToSwitch_LastDragStayedFormat", time, last.SourceProcess, outcome);
    }

    private void RefreshPreview(DragToSwitchSettings settings) =>
        Preview.Update(_host.Desktops.Desktops, settings, _host.ResolveOverlayTheme());

    private void OnStatusChanged(object? sender, EventArgs e) => RefreshStatus();

    private void OnThemeChanged(object? sender, EventArgs e) => RefreshPreview(Current);

    private void OnDesktopsChanged(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(() =>
    {
        RefreshStatus();
        RefreshPreview(Current);
    });

    private async void OnEnabledToggled(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        try
        {
            await _host.SetToolEnabledAsync(DragToSwitchTool.Id, EnabledToggle.IsOn);
        }
        catch (Exception ex)
        {
            _host.Log.Error(ex, "Turning Drag-to-Switch on or off failed");
        }

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
        DelayText.Text = Strings.Format("DragToSwitch_DelayFormat", delay);
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
            s.OverlaySize = (OverlaySize)Math.Max(0, SizeCombo.SelectedIndex);
            s.PreviewStyle = (PreviewStyle)Math.Max(0, PreviewCombo.SelectedIndex);
            s.OverlayTheme = (OverlayTheme)Math.Max(0, OverlayThemeCombo.SelectedIndex);
            s.AnimationsEnabled = AnimationsToggle.IsOn;
            s.CollapseAfterSwitch = CollapseToggle.IsOn;
            s.ReturnToOriginOnCancel = ReturnToggle.IsOn;
        });
        SourcesRow.Description = SourcesDescription((DragSources)Math.Max(0, SourcesCombo.SelectedIndex));
    }

    private static string SourcesDescription(DragSources sources) =>
        Strings.Get(sources == DragSources.AnyApp ? "DragToSwitch_SourcesAnyAppText" : "DragToSwitch_SourcesExplorerText");

    private void Save(Action<DragToSwitchSettings> apply)
    {
        if (_loading)
        {
            return;
        }

        // Read returns a fresh copy, so the preview can show the edit before the save finishes.
        DragToSwitchSettings edited = Current;
        apply(edited);
        RefreshPreview(edited);
        _ = _host.Settings.UpdateAsync<DragToSwitchSettings>(DragToSwitchSettings.Section, s =>
        {
            apply(s);
            return s;
        });
    }

    private void OnPreviewRequested(object? sender, EventArgs e) => _host.Selector?.ShowPreview(Current);

    private async void OnRedetect(object sender, RoutedEventArgs e)
    {
        try
        {
            await _host.Desktops.ReinitializeAsync();
            if (_host.Registry.IsEnabled(DragToSwitchTool.Id))
            {
                await _host.SetToolEnabledAsync(DragToSwitchTool.Id, false);
                await _host.SetToolEnabledAsync(DragToSwitchTool.Id, true);
            }
        }
        catch (Exception ex)
        {
            _host.Log.Error(ex, "Checking virtual desktop support again failed");
        }

        RefreshStatus();
        RefreshPreview(Current);
    }

    private void OnOpenLogs(object sender, RoutedEventArgs e) => AppHost.OpenFolder(_host.LogDirectory);

    private async void OnCopyDiagnostics(object sender, RoutedEventArgs e)
    {
        var package = new DataPackage();
        package.SetText(Diagnostics.Build(_host));
        Clipboard.SetContent(package);
        CopyDiagnosticsButton.Content = Strings.Get("Common_Copied");
        await Task.Delay(2000);
        CopyDiagnosticsButton.Content = Strings.Get("DragToSwitch_CopyDiagnostics");
    }
}
