using System.Globalization;
using System.Text;
using System.Text.Json;
using FluxKit.Platform.Watchers;

namespace FluxKit.App;

/// <summary>
/// <c>FluxKit.exe island set|done|fail|clear --id ID [--title T] [--detail D] [--progress N] [--glyph G] [--ttl SECONDS]</c>:
/// sends a Live Activity update to the running FluxKit (ADR-016) and exits at once, without any window. In PowerShell,
/// pipe it to <c>Out-Null</c> so each call finishes before the next.
/// </summary>
internal static class LiveActivityCommand
{
    private static readonly string[] Verbs = ["set", "done", "fail", "clear"];

    /// <summary>Returns the process exit code: 0 sent, 1 not delivered, 2 bad arguments.</summary>
    public static int Run(string[] args)
    {
        if (args.Length == 0 || !Verbs.Contains(args[0], StringComparer.OrdinalIgnoreCase))
        {
            return 2;
        }

        Dictionary<string, string> options = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 1; i + 1 < args.Length; i += 2)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
            {
                return 2;
            }

            options[args[i][2..]] = args[i + 1];
        }

        if (!options.TryGetValue("id", out string? id) || id.Length == 0)
        {
            return 2;
        }

        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteString("id", id);
            json.WriteString("state", args[0].ToLowerInvariant() switch
            {
                "done" => "done",
                "fail" => "failed",
                "clear" => "clear",
                _ => "running",
            });
            foreach (string text in new[] { "title", "detail", "glyph" })
            {
                if (options.TryGetValue(text, out string? value))
                {
                    json.WriteString(text, value);
                }
            }

            foreach (string number in new[] { "progress", "ttl" })
            {
                if (options.TryGetValue(number, out string? value) && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
                {
                    json.WriteNumber(number, parsed);
                }
            }

            json.WriteEndObject();
        }

        return LiveActivityClient.Send(Encoding.UTF8.GetString(buffer.ToArray()), TimeSpan.FromSeconds(2)) ? 0 : 1;
    }
}
