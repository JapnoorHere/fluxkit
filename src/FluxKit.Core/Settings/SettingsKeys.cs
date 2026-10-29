namespace FluxKit.Core.Settings;

/// <summary>The section keys in settings.json. Build keys here instead of joining strings at each call site.</summary>
public static class SettingsKeys
{
    /// <summary>Shell-wide preferences (<see cref="GlobalSettings"/>).</summary>
    public const string Global = "global";

    /// <summary>
    /// Prefix of every tool's section. It says "utilities" because that is what existing settings files use;
    /// changing it would reset everyone's tool settings.
    /// </summary>
    public const string ToolPrefix = "utilities/";

    /// <summary>The section for the tool with this id, e.g. "utilities/drag-to-switch".</summary>
    public static string Tool(string id) => ToolPrefix + id;
}
