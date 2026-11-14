using System.Globalization;

namespace FluxKit.Tools.FluxIsland.Activities;

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 0x1,
    Control = 0x2,
    Shift = 0x4,
    Windows = 0x8,
}

/// <summary>
/// A keyboard shortcut such as "Ctrl+Alt+I", as typed in settings. The modifier values match Windows'
/// <c>RegisterHotKey</c> and <see cref="Key"/> is a virtual-key code, so the Platform can use them as they are.
/// </summary>
public readonly record struct HotkeyGesture(HotkeyModifiers Modifiers, int Key)
{
    public const string Default = "Ctrl+Alt+I";

    private const int VkSpace = 0x20;
    private const int VkF1 = 0x70;

    /// <summary>
    /// Reads "Ctrl+Alt+I", "Win+Shift+F2" and the like. Letters, digits, F1–F24 and Space are allowed. A shortcut
    /// needs at least one of Ctrl, Alt or Win (Shift alone would block typing), except for F-keys.
    /// </summary>
    public static bool TryParse(string? text, out HotkeyGesture gesture)
    {
        gesture = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        HotkeyModifiers modifiers = HotkeyModifiers.None;
        int key = 0;
        foreach (string raw in text.Split('+', StringSplitOptions.TrimEntries))
        {
            HotkeyModifiers modifier = raw.ToUpperInvariant() switch
            {
                "CTRL" or "CONTROL" => HotkeyModifiers.Control,
                "ALT" => HotkeyModifiers.Alt,
                "SHIFT" => HotkeyModifiers.Shift,
                "WIN" or "WINDOWS" => HotkeyModifiers.Windows,
                _ => HotkeyModifiers.None,
            };

            if (modifier != HotkeyModifiers.None)
            {
                if (modifiers.HasFlag(modifier) || key != 0)
                {
                    return false; // Repeated, or after the key.
                }

                modifiers |= modifier;
                continue;
            }

            if (key != 0 || KeyOf(raw) is not { } parsed)
            {
                return false; // Two keys, or one we don't know.
            }

            key = parsed;
        }

        bool functionKey = key is >= VkF1 and < VkF1 + 24;
        if (key == 0 || (!functionKey && (modifiers & ~HotkeyModifiers.Shift) == HotkeyModifiers.None))
        {
            return false;
        }

        gesture = new HotkeyGesture(modifiers, key);
        return true;
    }

    /// <summary>The shortcut written the standard way, e.g. "Ctrl+Alt+I".</summary>
    public override string ToString()
    {
        List<string> parts = [];
        if (Modifiers.HasFlag(HotkeyModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Windows))
        {
            parts.Add("Win");
        }

        parts.Add(Key switch
        {
            VkSpace => "Space",
            >= VkF1 and < VkF1 + 24 => "F" + (Key - VkF1 + 1).ToString(CultureInfo.InvariantCulture),
            _ => ((char)Key).ToString(),
        });
        return string.Join('+', parts);
    }

    private static int? KeyOf(string name)
    {
        string upper = name.ToUpperInvariant();
        if (upper.Length == 1 && (char.IsAsciiLetterUpper(upper[0]) || char.IsAsciiDigit(upper[0])))
        {
            return upper[0]; // VK codes for A–Z and 0–9 are their ASCII codes.
        }

        if (upper == "SPACE")
        {
            return VkSpace;
        }

        return upper.Length is 2 or 3 && upper[0] == 'F' && int.TryParse(upper.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out int n) && n is >= 1 and <= 24
            ? VkF1 + n - 1
            : null;
    }
}
