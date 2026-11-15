using System.Globalization;
using System.Text.RegularExpressions;

namespace FluxKit.Tools.FluxIsland.Activities;

/// <summary>
/// Reads a line typed into the command bar. It understands timers ("5m", "1h20", "timer 10", "90s"), sums
/// ("= 12*8", "15% 4"), colours ("#3a7bff", "rgb(58,123,255)"), notes ("note buy milk"), and a few verbs
/// ("stopwatch", "lap", "pause", "next", "mute", "lock", "screenshot", "screen off", "glance"…).
/// Matching is forgiving about case and spaces. Returns null for anything it doesn't understand.
/// </summary>
public static partial class CommandParser
{
    /// <summary>The longest timer the bar will start.</summary>
    public static readonly TimeSpan MaxTimer = TimeSpan.FromHours(24);

    /// <summary>Notes longer than this are cut.</summary>
    public const int MaxNoteLength = 280;

    private static readonly Dictionary<string, CommandKind> Verbs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["focus"] = CommandKind.StartFocus,
        ["pomodoro"] = CommandKind.StartFocus,
        ["stopwatch"] = CommandKind.StartStopwatch,
        ["sw"] = CommandKind.StartStopwatch,
        ["lap"] = CommandKind.Lap,
        ["stop"] = CommandKind.StopTimer,
        ["cancel"] = CommandKind.StopTimer,
        ["play"] = CommandKind.PlayPause,
        ["pause"] = CommandKind.PlayPause,
        ["next"] = CommandKind.NextTrack,
        ["skip"] = CommandKind.NextTrack,
        ["prev"] = CommandKind.PreviousTrack,
        ["previous"] = CommandKind.PreviousTrack,
        ["back"] = CommandKind.PreviousTrack,
        ["mute"] = CommandKind.ToggleMic,
        ["unmute"] = CommandKind.ToggleMic,
        ["mic"] = CommandKind.ToggleMic,
        ["lock"] = CommandKind.Lock,
        ["screenshot"] = CommandKind.Screenshot,
        ["snip"] = CommandKind.Screenshot,
        ["screen off"] = CommandKind.ScreenOff,
        ["sleep screen"] = CommandKind.ScreenOff,
        ["display off"] = CommandKind.ScreenOff,
        ["clear shelf"] = CommandKind.ClearShelf,
    };

    private static readonly string[] Pages = ["music", "timer", "shelf", "glance", "notes"];

    public static IslandCommand? Parse(string? input)
    {
        string text = Regex.Replace(input ?? string.Empty, @"\s+", " ").Trim();
        if (text.Length == 0)
        {
            return null;
        }

        if (Verbs.TryGetValue(text, out CommandKind verb))
        {
            return new IslandCommand(verb);
        }

        if (Pages.Contains(text, StringComparer.OrdinalIgnoreCase))
        {
            return new IslandCommand(CommandKind.OpenPage) { Text = text.ToLowerInvariant() };
        }

        if (StartsWithWord(text, "note", out string note) || StartsWithWord(text, "n", out note))
        {
            note = note.Trim();
            return note.Length == 0 ? null : new IslandCommand(CommandKind.AddNote) { Text = note.Length > MaxNoteLength ? note[..MaxNoteLength] : note };
        }

        if (ParseColor(text) is { } rgb)
        {
            return new IslandCommand(CommandKind.ShowColor) { Color = rgb };
        }

        string timerText = StartsWithWord(text, "timer", out string rest) || StartsWithWord(text, "t", out rest) ? rest : text;
        if (ParseDuration(timerText) is { } duration)
        {
            return new IslandCommand(CommandKind.StartTimer) { Duration = duration };
        }

        string sum = text.StartsWith('=') ? text[1..] : text;
        if ((text.StartsWith('=') || Calculator.LooksLikeMaths(sum)) && Calculator.Evaluate(sum) is { } value)
        {
            return new IslandCommand(CommandKind.Calculate) { Number = value, Text = sum.Trim() };
        }

        return null;
    }

    /// <summary>
    /// "5m", "5 min", "90s", "1h20", "1h 20m", "1:30" (minutes:seconds) or a bare "25" (minutes). Null if it isn't a
    /// duration, or is zero or longer than <see cref="MaxTimer"/>.
    /// </summary>
    public static TimeSpan? ParseDuration(string text)
    {
        text = text.Trim().ToLowerInvariant();
        if (text.Length == 0)
        {
            return null;
        }

        TimeSpan total;
        if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int bareMinutes))
        {
            total = TimeSpan.FromMinutes(bareMinutes);
        }
        else if (MinutesSeconds().Match(text) is { Success: true } clock)
        {
            int seconds = int.Parse(clock.Groups[2].Value, CultureInfo.InvariantCulture);
            if (seconds >= 60)
            {
                return null;
            }

            total = TimeSpan.FromMinutes(int.Parse(clock.Groups[1].Value, CultureInfo.InvariantCulture)) + TimeSpan.FromSeconds(seconds);
        }
        else if (DurationParts().Match(text) is { Success: true } parts && parts.Length == text.Length)
        {
            int hours = Group(parts, "h");
            int minutes = Group(parts, "m");
            int seconds = Group(parts, "s");

            // "1h20" means 1 hour 20 minutes: a trailing number after hours counts as minutes.
            if (parts.Groups["tail"].Success)
            {
                minutes += int.Parse(parts.Groups["tail"].Value, CultureInfo.InvariantCulture);
            }

            total = new TimeSpan(hours, minutes, seconds);
        }
        else
        {
            return null;
        }

        return total > TimeSpan.Zero && total <= MaxTimer ? total : null;
    }

    /// <summary>"#3a7bff", "#38f", "3a7bff" or "rgb(58, 123, 255)" as 0xRRGGBB; null otherwise.</summary>
    public static int? ParseColor(string text)
    {
        text = text.Trim();
        if (HexColor().Match(text) is { Success: true } hex)
        {
            string digits = hex.Groups[1].Value;
            if (digits.Length == 3)
            {
                digits = string.Concat(digits.Select(c => new string(c, 2)));
            }

            // Without a #, it must mix digits and letters: "facade" stays a word and "255" stays a number.
            return !text.StartsWith('#') && !(digits.Any(char.IsAsciiDigit) && digits.Any(char.IsAsciiLetter))
                ? null
                : int.Parse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        if (RgbColor().Match(text) is { Success: true } rgb)
        {
            int[] channels = [.. Enumerable.Range(1, 3).Select(i => int.Parse(rgb.Groups[i].Value, CultureInfo.InvariantCulture))];
            return channels.All(c => c <= 255) ? (channels[0] << 16) | (channels[1] << 8) | channels[2] : null;
        }

        return null;
    }

    private static bool StartsWithWord(string text, string word, out string rest)
    {
        if (text.Length > word.Length && text.StartsWith(word + " ", StringComparison.OrdinalIgnoreCase))
        {
            rest = text[(word.Length + 1)..];
            return true;
        }

        rest = string.Empty;
        return false;
    }

    private static int Group(Match match, string name) =>
        match.Groups[name].Success ? int.Parse(match.Groups[name].Value, CultureInfo.InvariantCulture) : 0;

    [GeneratedRegex(@"^(\d{1,4}):(\d{2})$")]
    private static partial Regex MinutesSeconds();

    [GeneratedRegex(@"^(?:(?<h>\d{1,2})\s*(?:h|hr|hrs|hour|hours)\s*)?(?:(?<m>\d{1,4})\s*(?:m|min|mins|minute|minutes)\s*)?(?:(?<s>\d{1,5})\s*(?:s|sec|secs|second|seconds))?(?<tail>(?<=h\s*|hr\s*|hrs\s*|hour\s*|hours\s*)\d{1,2})?$")]
    private static partial Regex DurationParts();

    [GeneratedRegex(@"^#?([0-9a-fA-F]{6}|[0-9a-fA-F]{3})$")]
    private static partial Regex HexColor();

    [GeneratedRegex(@"^rgb\s*\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})\s*\)$", RegexOptions.IgnoreCase)]
    private static partial Regex RgbColor();
}
