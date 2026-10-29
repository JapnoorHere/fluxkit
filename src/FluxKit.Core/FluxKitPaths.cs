namespace FluxKit.Core;

/// <summary>
/// Where FluxKit keeps its files. Everything lives under %LOCALAPPDATA%\FluxKit; nothing is written anywhere else
/// (apart from the HKCU Run value for "Launch at startup").
/// </summary>
public static class FluxKitPaths
{
    public static string Root { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FluxKit");

    public static string SettingsFile => Path.Combine(Root, "settings.json");

    public static string LogsFolder => Path.Combine(Root, "Logs");

    /// <summary>The drop log written by the DropProbe test app.</summary>
    public static string DropProbeLog => Path.Combine(Root, "DropProbe", "drops.jsonl");
}
