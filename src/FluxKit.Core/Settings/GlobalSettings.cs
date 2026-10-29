namespace FluxKit.Core.Settings;

public enum AppTheme
{
    System,
    Light,
    Dark,
}

public enum LogVerbosity
{
    ErrorsOnly,
    Normal,
    Verbose,
}

/// <summary>Shell-wide preferences stored in the "global" section.</summary>
public sealed class GlobalSettings : ISettingsSection
{
    public const string Section = SettingsKeys.Global;

    public AppTheme Theme { get; set; } = AppTheme.System;

    public bool LaunchAtStartup { get; set; }

    public bool OpenSettingsOnLaunch { get; set; }

    public LogVerbosity LogLevel { get; set; } = LogVerbosity.Normal;

    /// <summary>Verbose logging automatically reverts to Normal after this instant.</summary>
    public DateTimeOffset? VerboseUntilUtc { get; set; }

    public bool FirstRunCompleted { get; set; }

    public WindowPlacement MainWindow { get; set; } = new();

    public IEnumerable<string> Normalize()
    {
        MainWindow ??= new WindowPlacement();
        if (MainWindow.Width is < 760 or > 10000)
        {
            MainWindow.Width = 1040;
            yield return "mainWindow.width reset";
        }

        if (MainWindow.Height is < 520 or > 10000)
        {
            MainWindow.Height = 700;
            yield return "mainWindow.height reset";
        }
    }
}

public sealed class WindowPlacement
{
    public int? X { get; set; }

    public int? Y { get; set; }

    public int Width { get; set; } = 1040;

    public int Height { get; set; } = 700;

    public bool Maximized { get; set; }
}
