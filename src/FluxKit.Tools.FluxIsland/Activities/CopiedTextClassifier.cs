using System.Globalization;
using System.Text.RegularExpressions;

namespace FluxKit.Tools.FluxIsland.Activities;

/// <summary>What copied text turned out to be, so the island can offer the right thing to do with it.</summary>
public enum SmartText
{
    None,
    Plain,
    Link,
    Email,
    Phone,
    Color,
}

/// <summary>
/// Works out what a piece of copied text is, on this PC and without keeping it: a web link, an email address, a phone
/// number, a colour, or just text. Only single, short values count as anything but plain text.
/// </summary>
internal static partial class CopiedTextClassifier
{
    private const int MaxValueLength = 512;

    /// <summary>
    /// The kind, what to show (<paramref name="text"/> tidied up), and what an action would open (a web or mailto link),
    /// or for a colour its RGB value in <c>Rgb</c>.
    /// </summary>
    public static (SmartText Kind, string Display, string? Target, int Rgb) Classify(string? text)
    {
        string value = text?.Trim() ?? string.Empty;
        if (value.Length == 0 || value.Length > MaxValueLength || value.Contains('\n', StringComparison.Ordinal))
        {
            return (SmartText.Plain, string.Empty, null, 0);
        }

        if (AsLink(value) is { } link)
        {
            return (SmartText.Link, link.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? link.Host[4..] : link.Host, link.AbsoluteUri, 0);
        }

        if (EmailPattern().IsMatch(value))
        {
            return (SmartText.Email, value, "mailto:" + value, 0);
        }

        if (AsColor(value) is { } rgb)
        {
            return (SmartText.Color, value.StartsWith('#') ? value.ToUpperInvariant() : value, null, rgb);
        }

        if (PhonePattern().IsMatch(value) && value.Count(char.IsAsciiDigit) is >= 7 and <= 15 && (value.StartsWith('+') || value.Any(c => c is ' ' or '-' or '(' or '.') || value.Count(char.IsAsciiDigit) >= 10))
        {
            return (SmartText.Phone, value, null, 0);
        }

        return (SmartText.Plain, string.Empty, null, 0);
    }

    private static Uri? AsLink(string value)
    {
        if (value.Contains(' ', StringComparison.Ordinal))
        {
            return null;
        }

        string candidate = value.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? "https://" + value : value;
        return Uri.TryCreate(candidate, UriKind.Absolute, out Uri? uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) && uri.Host.Contains('.', StringComparison.Ordinal)
            ? uri
            : null;
    }

    private static int? AsColor(string value)
    {
        Match hex = HexColorPattern().Match(value);
        if (hex.Success)
        {
            string digits = hex.Groups[1].Value;
            if (digits.Length == 3)
            {
                digits = string.Concat(digits.Select(c => new string(c, 2)));
            }

            // #RRGGBBAA keeps its colour and drops the alpha.
            return int.Parse(digits[..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        Match rgb = RgbColorPattern().Match(value);
        if (rgb.Success)
        {
            int[] parts = [.. Enumerable.Range(1, 3).Select(i => int.Parse(rgb.Groups[i].Value, CultureInfo.InvariantCulture))];
            return parts.All(p => p <= 255) ? (parts[0] << 16) | (parts[1] << 8) | parts[2] : null;
        }

        return null;
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[A-Za-z]{2,}$")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"^(\+|\()?[0-9][0-9 \-().]{5,20}[0-9]$")]
    private static partial Regex PhonePattern();

    [GeneratedRegex(@"^#([0-9A-Fa-f]{3}|[0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$")]
    private static partial Regex HexColorPattern();

    [GeneratedRegex(@"^rgba?\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})\s*(?:,\s*[\d.]+\s*)?\)$", RegexOptions.IgnoreCase)]
    private static partial Regex RgbColorPattern();
}
