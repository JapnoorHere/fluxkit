using FluxKit.Platform.Shell;
using FluxKit.Tools.FluxIsland;
using FluxKit.Tools.FluxIsland.Activities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>
/// Turning on notifications walks through Windows' requirements one clear step at a time (ADR-014): register FluxKit's
/// identity and restart once, then Windows' own permission. If a step fails the switch goes back off and an InfoBar
/// says why and offers the fix. Nothing is read, and no banner is changed, until every step has passed.
/// </summary>
public sealed partial class NotificationSettings : UserControl
{
    private readonly AppHost _host = AppHost.Current;
    private Func<Task>? _step;
    private bool _loading = true;

    public NotificationSettings()
    {
        InitializeComponent();
        Load();
    }

    /// <summary>Raised after a change is saved, so the page can refresh its preview.</summary>
    public event EventHandler? Changed;

    private static FluxIslandSettings Current => AppHost.Current.Settings.Read<FluxIslandSettings>(FluxIslandSettings.Section);

    private void Load()
    {
        _loading = true;
        FluxIslandSettings s = Current;
        ShowToggle.IsOn = s.ShowNotifications;
        PrivacyCombo.SelectedIndex = (int)s.NotificationPrivacy;
        PresentingToggle.IsOn = s.HideNotificationsWhilePresenting;
        Details.Visibility = s.ShowNotifications ? Visibility.Visible : Visibility.Collapsed;
        BuildApps(s);
        ShowState();
        _loading = false;
    }

    /// <summary>Says what is still needed when the switch is on but notifications can't be read yet.</summary>
    private void ShowState()
    {
        if (!ShowToggle.IsOn)
        {
            HideStep();
            return;
        }

        switch (_host.NotificationIdentity)
        {
            case IdentityState.NeedsRestart:
                ShowStep(InfoBarSeverity.Informational, "FluxIsland_NotificationStepRestart", "FluxIsland_NotificationRestart", _host.RestartAsync);
                break;
            case IdentityState.Active when !Platform.Watchers.NotificationWatcher.IsAllowed:
                ShowStep(InfoBarSeverity.Warning, "FluxIsland_NotificationStepDenied", "FluxIsland_NotificationOpenWindows", OpenWindowsSettingsAsync);
                break;
            default:
                HideStep();
                break;
        }
    }

    private async void OnShowToggled(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        if (!ShowToggle.IsOn)
        {
            Save(s => s.ShowNotifications = false);
            Details.Visibility = Visibility.Collapsed;
            HideStep();
            return;
        }

        bool on = await TurnOnAsync();
        _loading = true;
        ShowToggle.IsOn = on;
        _loading = false;
        Details.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Runs the next step towards reading notifications. Returns whether the switch should stay on.</summary>
    private async Task<bool> TurnOnAsync()
    {
        switch (_host.NotificationIdentity)
        {
            case IdentityState.Unavailable:
                ShowStep(InfoBarSeverity.Error, "FluxIsland_NotificationStepUnavailable", null, null);
                return false;

            case IdentityState.NotRegistered:
                if (!await ConfirmSetupAsync())
                {
                    return false;
                }

                string? error = await _host.RegisterNotificationIdentityAsync();
                if (error is not null)
                {
                    _host.Log.Warning("Registering the notification identity failed: {Error}", error);
                    ShowStep(InfoBarSeverity.Error, "FluxIsland_NotificationStepFailed", null, null);
                    return false;
                }

                // Saved first, so the restarted FluxKit asks Windows' permission and starts reading.
                Save(s => s.ShowNotifications = true);
                await _host.RestartAsync();
                return true;

            case IdentityState.NeedsRestart:
                Save(s => s.ShowNotifications = true);
                ShowState();
                return true;

            default:
                if (!await _host.RequestNotificationAccessAsync())
                {
                    ShowStep(InfoBarSeverity.Warning, "FluxIsland_NotificationStepDenied", "FluxIsland_NotificationOpenWindows", OpenWindowsSettingsAsync);
                    return false;
                }

                HideStep();
                Save(s => s.ShowNotifications = true);
                return true;
        }
    }

    /// <summary>Explains, before anything changes, what turning this on does.</summary>
    private async Task<bool> ConfirmSetupAsync()
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = Strings.Get("FluxIsland_NotificationSetupTitle"),
            Content = new TextBlock { Text = Strings.Get("FluxIsland_NotificationSetupText"), TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = Strings.Get("FluxIsland_NotificationSetupConfirm"),
            CloseButtonText = Strings.Get("FluxIsland_NotificationSetupCancel"),
            DefaultButton = ContentDialogButton.Primary,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private static Task OpenWindowsSettingsAsync()
    {
        ShellLauncher.OpenNotificationPrivacySettings();
        return Task.CompletedTask;
    }

    private void ShowStep(InfoBarSeverity severity, string messageKey, string? buttonKey, Func<Task>? step)
    {
        StepBar.Severity = severity;
        StepBar.Message = Strings.Get(messageKey);
        StepButton.Visibility = buttonKey is null ? Visibility.Collapsed : Visibility.Visible;
        StepButton.Content = buttonKey is null ? null : Strings.Get(buttonKey);
        _step = step;
        StepBar.IsOpen = true;
    }

    private void HideStep()
    {
        StepBar.IsOpen = false;
        _step = null;
    }

    private async void OnStepClicked(object sender, RoutedEventArgs e)
    {
        if (_step is { } step)
        {
            await step();
        }
    }

    private void OnChanged(object sender, object e)
    {
        if (_loading || PrivacyCombo is null)
        {
            return;
        }

        Save(s =>
        {
            s.NotificationPrivacy = (NotificationPrivacy)Math.Max(0, PrivacyCombo.SelectedIndex);
            s.HideNotificationsWhilePresenting = PresentingToggle.IsOn;
        });
    }

    /// <summary>Gives every app its Windows banner back; the island keeps showing their notifications too.</summary>
    private void OnRestoreClicked(object sender, RoutedEventArgs e)
    {
        Save(s =>
        {
            foreach (NotificationAppSettings app in s.NotificationApps.Values)
            {
                app.HideBanner = false;
            }
        });
        BuildApps(Current);
    }

    /// <summary>A row per app: its name, then Show on island, Hide Windows banner and Priority.</summary>
    private void BuildApps(FluxIslandSettings s)
    {
        AppsList.Children.Clear();
        NoAppsText.Visibility = s.NotificationApps.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        RestoreButton.Visibility = s.NotificationApps.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        foreach ((string appId, NotificationAppSettings app) in s.NotificationApps.OrderBy(a => a.Value.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var row = new Grid { ColumnSpacing = 12 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(new TextBlock { Text = app.Name.Length > 0 ? app.Name : appId, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
            AddCheck(row, 1, "FluxIsland_NotificationAppShow", app.Show, (a, on) => a.Show = on);
            AddCheck(row, 2, "FluxIsland_NotificationAppBanner", app.HideBanner, (a, on) => a.HideBanner = on);
            AddCheck(row, 3, "FluxIsland_NotificationAppPriority", app.Priority, (a, on) => a.Priority = on);
            AppsList.Children.Add(row);

            void AddCheck(Grid grid, int column, string key, bool value, Action<NotificationAppSettings, bool> set)
            {
                var check = new CheckBox { Content = Strings.Get(key), IsChecked = value, MinWidth = 0 };
                check.Click += (_, _) =>
                {
                    bool on = check.IsChecked == true;
                    Save(saved =>
                    {
                        if (saved.NotificationApps.TryGetValue(appId, out NotificationAppSettings? stored))
                        {
                            set(stored, on);
                        }
                    });
                };
                Grid.SetColumn(check, column);
                grid.Children.Add(check);
            }
        }
    }

    private void Save(Action<FluxIslandSettings> apply)
    {
        _ = _host.Settings.UpdateAsync<FluxIslandSettings>(FluxIslandSettings.Section, s =>
        {
            apply(s);
            return s;
        });
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
