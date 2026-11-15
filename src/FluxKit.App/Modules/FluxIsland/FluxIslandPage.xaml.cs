using FluxKit.App.Controls;
using FluxKit.Core.Tools;
using FluxKit.Tools.FluxIsland;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>Settings page for Flux Island: a live island preview beside the settings groups.</summary>
public sealed partial class FluxIslandPage : Page
{
    private readonly AppHost _host = AppHost.Current;
    private bool _loading = true;

    public FluxIslandPage()
    {
        InitializeComponent();
        Load();
        Loaded += (_, _) => _host.StatusChanged += OnStatusChanged;
        Unloaded += (_, _) => _host.StatusChanged -= OnStatusChanged;
    }

    private FluxIslandSettings Current => _host.Settings.Read<FluxIslandSettings>(FluxIslandSettings.Section);

    private void Load()
    {
        _loading = true;
        FluxIslandSettings s = Current;
        EnabledToggle.IsOn = _host.Registry.IsEnabled(FluxIslandTool.Id);
        MediaToggle.IsOn = s.ShowMedia;
        PowerToggle.IsOn = s.ShowPower;
        NetworkToggle.IsOn = s.ShowNetwork;
        PrivacyToggle.IsOn = s.ShowPrivacy;
        ClipboardToggle.IsOn = s.ShowClipboard;
        VolumeChangesToggle.IsOn = s.ShowVolumeChanges;
        AudioDevicesToggle.IsOn = s.ShowAudioDevices;
        MicMuteToggle.IsOn = s.ShowMicMute;
        DesktopsToggle.IsOn = s.ShowDesktopSwitches;
        DownloadsToggle.IsOn = s.ShowDownloads;
        ScreenshotsToggle.IsOn = s.ShowScreenshots;
        SmartCopyToggle.IsOn = s.SmartCopy;
        BrightnessToggle.IsOn = s.ShowBrightness;
        SaverToggle.IsOn = s.ShowBatterySaver;
        LockKeysToggle.IsOn = s.ShowLockKeys;
        DevicesToggle.IsOn = s.ShowDevices;
        GlanceToggle.IsOn = s.ShowGlance;
        HotkeyBox.Text = s.Hotkey;
        HotkeyRow.Description = Strings.Get("FluxIsland_HotkeyHelp");
        MonitorCombo.SelectedIndex = (int)s.Monitor;
        ClockToggle.IsOn = s.ShowClockWhenIdle;
        ShelfToggle.IsOn = s.ShelfEnabled;
        KeepShelfToggle.IsOn = s.KeepShelf;
        KeepNotesToggle.IsOn = s.KeepNotes;
        VolumeToggle.IsOn = s.ScrollForVolume;
        FocusSlider.Value = s.FocusMinutes;
        BreakSlider.Value = s.BreakMinutes;
        EffectsCombo.SelectedIndex = (int)s.Effects;
        FullScreenToggle.IsOn = s.HideInFullScreen;
        UpdateLengthText(s);
        EffectsRow.Description = Strings.Get(new Windows.UI.ViewManagement.UISettings().AnimationsEnabled
            ? "FluxIsland_EffectsOn"
            : "FluxIsland_EffectsOffInWindows");
        RefreshStatus();
        Preview.Update(s);
        _loading = false;
    }

    private void RefreshStatus()
    {
        ToolState state = _host.FluxIsland.State;
        bool enabled = _host.Registry.IsEnabled(FluxIslandTool.Id);
        OptionsPanel.Opacity = enabled ? 1 : 0.5;
        OptionsPanel.IsHitTestVisible = enabled;

        Header.StatusTone = StatusTones.For(state.Status);
        Header.StatusText = state.Status switch
        {
            ToolStatus.Running => Strings.Get("FluxIsland_StatusReady"),
            ToolStatus.Degraded => Strings.Get("FluxIsland_StatusLimited"),
            ToolStatus.Faulted => Strings.Get("FluxIsland_StatusStopped"),
            ToolStatus.Starting => Strings.Get("FluxIsland_StatusStarting"),
            _ => Strings.Get("FluxIsland_StatusOff"),
        };

        // The InfoBar only appears when something needs the user's attention.
        (bool open, InfoBarSeverity severity, string title, string message) = state.Status switch
        {
            ToolStatus.Degraded => (true, InfoBarSeverity.Warning, Strings.Get("FluxIsland_StatusLimited"), state.Detail ?? string.Empty),
            ToolStatus.Faulted => (true, InfoBarSeverity.Error, Strings.Get("FluxIsland_StatusStopped"), state.Detail ?? Strings.Get("FluxIsland_StoppedDetail")),
            _ => (false, InfoBarSeverity.Informational, string.Empty, string.Empty),
        };
        StatusBar.IsOpen = open;
        StatusBar.Severity = severity;
        StatusBar.Title = title;
        StatusBar.Message = message;
    }

    private void OnStatusChanged(object? sender, EventArgs e) => RefreshStatus();

    private async void OnEnabledToggled(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        try
        {
            await _host.SetToolEnabledAsync(FluxIslandTool.Id, EnabledToggle.IsOn);
        }
        catch (Exception ex)
        {
            _host.Log.Error(ex, "Turning Flux Island on or off failed");
        }

        RefreshStatus();
    }

    private void OnLengthChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        // Setting Minimum in XAML raises ValueChanged inside InitializeComponent, before later controls exist.
        if (BreakText is null)
        {
            return;
        }

        int focus = (int)FocusSlider.Value;
        int breakLength = (int)BreakSlider.Value;
        Save(s =>
        {
            s.FocusMinutes = focus;
            s.BreakMinutes = breakLength;
        });
    }

    private void OnChanged(object sender, object e)
    {
        if (FullScreenToggle is null)
        {
            return; // raised during InitializeComponent
        }

        Save(s =>
        {
            s.ShowMedia = MediaToggle.IsOn;
            s.ShowPower = PowerToggle.IsOn;
            s.ShowNetwork = NetworkToggle.IsOn;
            s.ShowPrivacy = PrivacyToggle.IsOn;
            s.ShowClipboard = ClipboardToggle.IsOn;
            s.ShowVolumeChanges = VolumeChangesToggle.IsOn;
            s.ShowAudioDevices = AudioDevicesToggle.IsOn;
            s.ShowMicMute = MicMuteToggle.IsOn;
            s.ShowDesktopSwitches = DesktopsToggle.IsOn;
            s.ShowDownloads = DownloadsToggle.IsOn;
            s.ShowScreenshots = ScreenshotsToggle.IsOn;
            s.SmartCopy = SmartCopyToggle.IsOn;
            s.ShowBrightness = BrightnessToggle.IsOn;
            s.ShowBatterySaver = SaverToggle.IsOn;
            s.ShowLockKeys = LockKeysToggle.IsOn;
            s.ShowDevices = DevicesToggle.IsOn;
            s.ShowGlance = GlanceToggle.IsOn;
            s.Monitor = (IslandMonitor)Math.Max(0, MonitorCombo.SelectedIndex);
            s.ShowClockWhenIdle = ClockToggle.IsOn;
            s.ShelfEnabled = ShelfToggle.IsOn;
            s.KeepShelf = KeepShelfToggle.IsOn;
            s.KeepNotes = KeepNotesToggle.IsOn;
            s.ScrollForVolume = VolumeToggle.IsOn;
            s.Effects = (IslandEffects)Math.Max(0, EffectsCombo.SelectedIndex);
            s.HideInFullScreen = FullScreenToggle.IsOn;
        });
    }

    private void OnHotkeyKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            e.Handled = true;
            OnHotkeyCommitted(sender, e);
        }
    }

    /// <summary>Saves the shortcut once typing is done. Empty turns it off; something unreadable is put back as it was.</summary>
    private void OnHotkeyCommitted(object sender, RoutedEventArgs e)
    {
        string text = HotkeyBox.Text.Trim();
        if (text.Length == 0)
        {
            HotkeyRow.Description = Strings.Get("FluxIsland_HotkeyOff");
            Save(s => s.Hotkey = string.Empty);
            return;
        }

        if (!Tools.FluxIsland.Activities.HotkeyGesture.TryParse(text, out Tools.FluxIsland.Activities.HotkeyGesture gesture))
        {
            HotkeyRow.Description = Strings.Get("FluxIsland_HotkeyInvalid");
            HotkeyBox.Text = Current.Hotkey;
            return;
        }

        HotkeyBox.Text = gesture.ToString();
        HotkeyRow.Description = Strings.Get("FluxIsland_HotkeyHelp");
        Save(s => s.Hotkey = gesture.ToString());
    }

    private void UpdateLengthText(FluxIslandSettings s)
    {
        FocusText.Text = Strings.Format("FluxIsland_MinutesFormat", s.FocusMinutes);
        BreakText.Text = Strings.Format("FluxIsland_MinutesFormat", s.BreakMinutes);
    }

    private void Save(Action<FluxIslandSettings> apply)
    {
        FluxIslandSettings edited = Current;
        apply(edited);
        UpdateLengthText(edited);
        if (_loading)
        {
            return;
        }

        // Read returns a fresh copy, so the preview can show the edit before the save finishes.
        Preview.Update(edited);
        _ = _host.Settings.UpdateAsync<FluxIslandSettings>(FluxIslandSettings.Section, s =>
        {
            apply(s);
            return s;
        });
    }
}
