using FluxKit.Core.Settings;
using FluxKit.Platform.Startup;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FluxKit.App.Shell;

/// <summary>Shell-wide preferences: startup, theme, diagnostics and reset.</summary>
public sealed partial class GeneralPage : Page
{
    private readonly AppHost _host = AppHost.Current;
    private bool _loading;

    public GeneralPage()
    {
        InitializeComponent();
        LogFolderRow.Description = _host.LogDirectory;
        SettingsFileRow.Description = _host.SettingsPath;
        Load();
    }

    private void Load()
    {
        _loading = true;
        GlobalSettings global = _host.Settings.Read<GlobalSettings>(GlobalSettings.Section);
        StartupState state = _host.Startup.GetState();
        StartupToggle.IsOn = state is StartupState.Enabled or StartupState.StalePath or StartupState.DisabledByUser;
        OpenOnLaunchToggle.IsOn = global.OpenSettingsOnLaunch;
        ThemeCombo.SelectedIndex = (int)global.Theme;
        LogCombo.SelectedIndex = (int)global.LogLevel;
        UpdateStartupInfo(state);
        _loading = false;
    }

    private void UpdateStartupInfo(StartupState state)
    {
        (bool open, InfoBarSeverity severity, string message, bool fixable) = state switch
        {
            StartupState.StalePath => (true, InfoBarSeverity.Warning, "FluxKit is set to start from an older copy of the app. Select Fix to use this copy.", true),
            StartupState.DisabledByUser => (true, InfoBarSeverity.Informational, "FluxKit is turned off in Windows Settings › Apps › Startup. Turn it on there to start FluxKit with Windows.", false),
            _ => (false, InfoBarSeverity.Informational, string.Empty, false),
        };
        StartupInfo.IsOpen = open;
        StartupInfo.Severity = severity;
        StartupInfo.Message = message;
        StartupFixButton.Visibility = fixable ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void OnStartupToggled(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        bool on = StartupToggle.IsOn;
        if (on)
        {
            _host.Startup.Enable();
        }
        else
        {
            _host.Startup.Disable();
        }

        await _host.Settings.UpdateAsync<GlobalSettings>(GlobalSettings.Section, s =>
        {
            s.LaunchAtStartup = on;
            return s;
        });
        UpdateStartupInfo(_host.Startup.GetState());
    }

    private void OnStartupFix(object sender, RoutedEventArgs e)
    {
        _host.Startup.Enable();
        UpdateStartupInfo(_host.Startup.GetState());
    }

    private async void OnOpenOnLaunchToggled(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        bool on = OpenOnLaunchToggle.IsOn;
        await _host.Settings.UpdateAsync<GlobalSettings>(GlobalSettings.Section, s =>
        {
            s.OpenSettingsOnLaunch = on;
            return s;
        });
    }

    private async void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || ThemeCombo.SelectedIndex < 0)
        {
            return;
        }

        var theme = (AppTheme)ThemeCombo.SelectedIndex;
        await _host.Settings.UpdateAsync<GlobalSettings>(GlobalSettings.Section, s =>
        {
            s.Theme = theme;
            return s;
        });
    }

    private async void OnLogLevelChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || LogCombo.SelectedIndex < 0)
        {
            return;
        }

        var level = (LogVerbosity)LogCombo.SelectedIndex;
        await _host.Settings.UpdateAsync<GlobalSettings>(GlobalSettings.Section, s =>
        {
            s.LogLevel = level;
            s.VerboseUntilUtc = level == LogVerbosity.Verbose ? DateTimeOffset.UtcNow.AddHours(24) : null;
            return s;
        });
    }

    private void OnOpenLogs(object sender, RoutedEventArgs e) => AppHost.OpenFolder(_host.LogDirectory);

    private async void OnOpenSettingsFile(object sender, RoutedEventArgs e)
    {
        await _host.Settings.FlushAsync();
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"/select,\"{_host.SettingsPath}\"") { UseShellExecute = true });
    }

    private async void OnReset(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            RequestedTheme = ActualTheme,
            Title = "Reset all FluxKit settings?",
            Content = "All tools go back to their default settings, and FluxKit will no longer start when you sign in. Your files aren't affected.",
            PrimaryButtonText = "Reset",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        _host.Startup.Disable();
        await _host.Settings.ResetAsync();
        await _host.Settings.UpdateAsync<GlobalSettings>(GlobalSettings.Section, s =>
        {
            s.FirstRunCompleted = true;
            return s;
        });
        foreach (Core.Utilities.IFluxUtility utility in _host.Registry.All)
        {
            await _host.SetUtilityEnabledAsync(utility.Descriptor.Id, _host.Registry.IsEnabled(utility.Descriptor.Id));
        }

        Load();
    }
}
