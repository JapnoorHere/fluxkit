using System.Text.RegularExpressions;
using FluxKit.Core.Settings;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace FluxKit.Core.Logging;

/// <summary>Creates the application logger: daily rolling files, 7 days kept, live level switching.</summary>
public static class LoggingSetup
{
    public static LoggingLevelSwitch LevelSwitch { get; } = new(LogEventLevel.Information);

    public static ILogger Create(string logDirectory)
    {
        Directory.CreateDirectory(logDirectory);
        return new LoggerConfiguration()
            .MinimumLevel.ControlledBy(LevelSwitch)
            .Enrich.WithProperty("Pid", Environment.ProcessId)
            .WriteTo.File(
                Path.Combine(logDirectory, "fluxkit-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                formatProvider: System.Globalization.CultureInfo.InvariantCulture,
                shared: false,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    /// <summary>Applies the user's verbosity, honouring the 24-hour auto-revert for Verbose.</summary>
    public static LogVerbosity Apply(GlobalSettings settings, DateTimeOffset now)
    {
        LogVerbosity effective = settings.LogLevel == LogVerbosity.Verbose && settings.VerboseUntilUtc is { } until && until <= now
            ? LogVerbosity.Normal
            : settings.LogLevel;

        LevelSwitch.MinimumLevel = effective switch
        {
            LogVerbosity.ErrorsOnly => LogEventLevel.Warning,
            LogVerbosity.Verbose => LogEventLevel.Debug,
            _ => LogEventLevel.Information,
        };
        return effective;
    }
}

/// <summary>Removes file-system paths from free text before it is logged or shown in diagnostics.</summary>
public static partial class Redaction
{
    public static string ScrubPaths(string? text) =>
        string.IsNullOrEmpty(text) ? string.Empty : PathPattern().Replace(text, "<path>");

    [GeneratedRegex(@"(?:[A-Za-z]:\\|\\\\)[^\s""'<>|]*", RegexOptions.CultureInvariant)]
    private static partial Regex PathPattern();
}
