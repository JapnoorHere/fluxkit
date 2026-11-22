using FluxKit.Tools.FluxIsland;
using Windows.UI;

namespace FluxKit.App.Modules.FluxIsland.Design;

/// <summary>
/// One of the island's themes, picked in settings: the body's colour from top to bottom, the cards on it, the chip
/// behind a selected icon, the rim light, and a signature accent for empty states. Text stays white on every theme,
/// so contrast never depends on the choice.
/// </summary>
internal sealed record IslandTheme(IslandLook Look, Color Top, Color Bottom, Color Card, Color CardEdge, Color Chip, Color Rim, Color Accent)
{
    private static readonly Color White = Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);

    public static readonly IslandTheme Onyx = new(IslandLook.Onyx, Rgb(0x0B, 0x0B, 0x0D), Rgb(0x05, 0x05, 0x06), Rgb(0x1C, 0x1C, 0x1F), Alpha(White, 0x14), Alpha(White, 0x26), Alpha(White, 0x3D), IslandPalette.Blue);

    public static readonly IslandTheme PureBlack = new(IslandLook.PureBlack, Rgb(0, 0, 0), Rgb(0, 0, 0), Rgb(0x12, 0x12, 0x12), Alpha(White, 0x10), Alpha(White, 0x22), Alpha(White, 0x26), IslandPalette.Blue);

    public static readonly IslandTheme Graphite = new(IslandLook.Graphite, Rgb(0x2C, 0x2C, 0x30), Rgb(0x1A, 0x1A, 0x1D), Rgb(0x3A, 0x3A, 0x3F), Alpha(White, 0x1A), Alpha(White, 0x2E), Alpha(White, 0x4A), IslandPalette.Cyan);

    public static readonly IslandTheme Glass = new(IslandLook.Glass, Color.FromArgb(0xE8, 0x1E, 0x1E, 0x26), Color.FromArgb(0xE8, 0x12, 0x12, 0x18), Alpha(White, 0x14), Alpha(White, 0x22), Alpha(White, 0x2E), Alpha(White, 0x7A), IslandPalette.Cyan);

    public static readonly IslandTheme Mercury = new(IslandLook.Mercury, Rgb(0x5A, 0x5E, 0x69), Rgb(0x0A, 0x0A, 0x0C), Alpha(White, 0x14), Alpha(White, 0x26), Alpha(White, 0x30), Alpha(White, 0x9A), IslandPalette.TextPrimary);

    public static readonly IslandTheme Rose = new(IslandLook.Rose, Rgb(0x4A, 0x22, 0x2E), Rgb(0x1A, 0x0B, 0x10), Rgb(0x5A, 0x2C, 0x39), Alpha(White, 0x1C), Alpha(White, 0x2E), Color.FromArgb(0x70, 0xFF, 0xB3, 0xC6), Rgb(0xFF, 0x6B, 0x8B));

    public static readonly IslandTheme Lavender = new(IslandLook.Lavender, Rgb(0x34, 0x2A, 0x55), Rgb(0x12, 0x0E, 0x22), Rgb(0x42, 0x37, 0x68), Alpha(White, 0x1C), Alpha(White, 0x2E), Color.FromArgb(0x70, 0xC9, 0xB8, 0xFF), Rgb(0xB4, 0x9C, 0xFF));

    public static readonly IslandTheme Peach = new(IslandLook.Peach, Rgb(0x4D, 0x2E, 0x1C), Rgb(0x1A, 0x0F, 0x08), Rgb(0x5E, 0x3A, 0x25), Alpha(White, 0x1C), Alpha(White, 0x2E), Color.FromArgb(0x70, 0xFF, 0xCB, 0xA4), Rgb(0xFF, 0x9F, 0x6B));

    public static readonly IslandTheme Sakura = new(IslandLook.Sakura, Rgb(0x4A, 0x24, 0x40), Rgb(0x1A, 0x0B, 0x16), Rgb(0x5C, 0x2E, 0x50), Alpha(White, 0x1C), Alpha(White, 0x2E), Color.FromArgb(0x70, 0xFF, 0xC2, 0xE6), Rgb(0xFF, 0x8A, 0xD0));

    /// <summary>The theme in use; set when the look setting changes.</summary>
    public static IslandTheme Current { get; private set; } = Onyx;

    public static IslandTheme For(IslandLook look) => look switch
    {
        IslandLook.PureBlack => PureBlack,
        IslandLook.Graphite => Graphite,
        IslandLook.Glass => Glass,
        IslandLook.Mercury => Mercury,
        IslandLook.Rose => Rose,
        IslandLook.Lavender => Lavender,
        IslandLook.Peach => Peach,
        IslandLook.Sakura => Sakura,
        _ => Onyx,
    };

    /// <summary>Makes <paramref name="look"/> the current theme. Returns true when it changed.</summary>
    public static bool Use(IslandLook look)
    {
        IslandTheme next = For(look);
        if (next == Current)
        {
            return false;
        }

        Current = next;
        return true;
    }

    private static Color Rgb(byte r, byte g, byte b) => Color.FromArgb(0xFF, r, g, b);

    private static Color Alpha(Color color, byte alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);
}
