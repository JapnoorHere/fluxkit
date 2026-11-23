using FluxKit.Tools.FluxIsland;
using Windows.UI;

namespace FluxKit.App.Modules.FluxIsland.Design;

/// <summary>
/// How the island is painted: a material picked in settings (Onyx, Pure black, Graphite, Glass or Mercury), optionally
/// tinted with Windows' accent colour. Holds the body's colour from top to bottom, the cards on it, the chip behind a
/// selected icon, and the rim light. Text stays white on every material, so contrast never depends on the choice.
/// </summary>
internal sealed record IslandTheme(IslandLook Look, Color Top, Color Bottom, Color Card, Color CardEdge, Color Chip, Color Rim)
{
    private static readonly Color White = Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);

    /// <summary>True black with a hint of depth, like a phone's island.</summary>
    public static readonly IslandTheme Onyx = new(IslandLook.Onyx, Rgb(0x0B, 0x0B, 0x0D), Rgb(0x05, 0x05, 0x06), Rgb(0x1C, 0x1C, 0x1F), Alpha(White, 0x14), Alpha(White, 0x26), Alpha(White, 0x3D));

    /// <summary>#000 for OLED screens: unused pixels stay off.</summary>
    public static readonly IslandTheme PureBlack = new(IslandLook.PureBlack, Rgb(0, 0, 0), Rgb(0, 0, 0), Rgb(0x12, 0x12, 0x12), Alpha(White, 0x10), Alpha(White, 0x22), Alpha(White, 0x26));

    /// <summary>Matte: a soft, lighter grey with no shine.</summary>
    public static readonly IslandTheme Graphite = new(IslandLook.Graphite, Rgb(0x2C, 0x2C, 0x30), Rgb(0x1A, 0x1A, 0x1D), Rgb(0x3A, 0x3A, 0x3F), Alpha(White, 0x1A), Alpha(White, 0x2E), Alpha(White, 0x4A));

    /// <summary>Smoked glass that lets a hint of the desktop through, with a brighter rim.</summary>
    public static readonly IslandTheme Glass = new(IslandLook.Glass, Color.FromArgb(0xE8, 0x1E, 0x1E, 0x26), Color.FromArgb(0xE8, 0x12, 0x12, 0x18), Alpha(White, 0x14), Alpha(White, 0x22), Alpha(White, 0x2E), Alpha(White, 0x7A));

    /// <summary>Liquid metal, with a highlight that follows the pointer.</summary>
    public static readonly IslandTheme Mercury = new(IslandLook.Mercury, Rgb(0x5A, 0x5E, 0x69), Rgb(0x0A, 0x0A, 0x0C), Alpha(White, 0x14), Alpha(White, 0x26), Alpha(White, 0x30), Alpha(White, 0x9A));

    /// <summary>The theme in use; set when the material or the accent setting changes.</summary>
    public static IslandTheme Current { get; private set; } = Onyx;

    /// <summary>The material for <paramref name="look"/>. Retired colour tints read as Onyx.</summary>
    public static IslandTheme Material(IslandLook look) => look switch
    {
        IslandLook.PureBlack => PureBlack,
        IslandLook.Graphite => Graphite,
        IslandLook.Glass => Glass,
        IslandLook.Mercury => Mercury,
        _ => Onyx,
    };

    /// <summary>
    /// Makes <paramref name="look"/>, tinted with <paramref name="accent"/> when given, the current theme. Returns true
    /// when it changed.
    /// </summary>
    public static bool Use(IslandLook look, Color? accent)
    {
        IslandTheme material = Material(look);
        IslandTheme next = accent is { } tint ? material.Tinted(tint) : material;
        if (next == Current)
        {
            return false;
        }

        Current = next;
        return true;
    }

    /// <summary>
    /// The material washed with the accent colour: strongest at the top of the body and on the cards, fading towards
    /// the bottom, so it reads as coloured light on the material rather than paint. Alpha is kept, so Glass stays glass.
    /// </summary>
    public IslandTheme Tinted(Color accent) => this with
    {
        Top = Blend(Top, accent, 0.42),
        Bottom = Blend(Bottom, accent, 0.14),
        Card = Blend(Card, accent, 0.28),
        Chip = Color.FromArgb(0x4A, accent.R, accent.G, accent.B),
        Rim = Color.FromArgb(Rim.A, (byte)((accent.R + 255) / 2), (byte)((accent.G + 255) / 2), (byte)((accent.B + 255) / 2)),
    };

    private static Color Blend(Color material, Color accent, double amount) => Color.FromArgb(
        material.A,
        (byte)(material.R + ((accent.R - material.R) * amount)),
        (byte)(material.G + ((accent.G - material.G) * amount)),
        (byte)(material.B + ((accent.B - material.B) * amount)));

    private static Color Rgb(byte r, byte g, byte b) => Color.FromArgb(0xFF, r, g, b);

    private static Color Alpha(Color color, byte alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);
}
