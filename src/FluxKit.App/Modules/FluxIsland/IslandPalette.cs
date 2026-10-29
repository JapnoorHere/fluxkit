using Windows.UI;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>
/// The island's colours. The pill is always near-black whatever the app theme (it sits on any wallpaper), so these
/// are fixed values rather than theme resources. Accents are tuned to read on the dark surface.
/// </summary>
internal static class IslandPalette
{
    public static readonly Color Surface = Color.FromArgb(0xFF, 0x0B, 0x0B, 0x0F);

    /// <summary>The 1 px inner edge, white at 8 %.</summary>
    public static readonly Color Highlight = Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF);

    public static readonly Color TextPrimary = Color.FromArgb(0xFF, 0xF5, 0xF5, 0xF7);

    public static readonly Color TextSecondary = Color.FromArgb(0xFF, 0xA1, 0xA1, 0xAA);

    public static readonly Color Green = Color.FromArgb(0xFF, 0x30, 0xD1, 0x58);

    public static readonly Color Red = Color.FromArgb(0xFF, 0xFF, 0x45, 0x3A);

    public static readonly Color Orange = Color.FromArgb(0xFF, 0xFF, 0x9F, 0x0A);

    public static readonly Color Cyan = Color.FromArgb(0xFF, 0x3C, 0xCD, 0xEB);

    public static readonly Color Violet = Color.FromArgb(0xFF, 0x8A, 0x84, 0xFF);
}
