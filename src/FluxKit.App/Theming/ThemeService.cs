using FluxKit.Core.Settings;
using FluxKit.Utilities.DragToSwitch;
using Microsoft.UI.Xaml;

namespace FluxKit.App.Theming;

/// <summary>Turns the theme settings into the theme each window should use.</summary>
internal static class ThemeService
{
    public static ElementTheme ForApp(AppTheme theme) => theme switch
    {
        AppTheme.Light => ElementTheme.Light,
        AppTheme.Dark => ElementTheme.Dark,
        _ => ElementTheme.Default,
    };

    /// <summary>The picker's theme: its own setting, or the app's theme when set to follow FluxKit.</summary>
    public static ElementTheme ForOverlay(OverlayTheme theme, ElementTheme appTheme) => theme switch
    {
        OverlayTheme.Light => ElementTheme.Light,
        OverlayTheme.Dark => ElementTheme.Dark,
        OverlayTheme.System => ElementTheme.Default,
        _ => appTheme,
    };
}
