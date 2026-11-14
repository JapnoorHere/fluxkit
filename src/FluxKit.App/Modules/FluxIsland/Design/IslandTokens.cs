namespace FluxKit.App.Modules.FluxIsland.Design;

/// <summary>
/// The island's measurements in one place: a 4 px spacing grid, corner radii and a type ramp. Panels and the pill use
/// these instead of literal numbers, so the whole island stays consistent and can be tuned from here.
/// </summary>
internal static class IslandTokens
{
    // Spacing, on a 4 px grid.
    public const double Space1 = 4;
    public const double Space2 = 8;
    public const double Space3 = 12;
    public const double Space4 = 16;
    public const double Space5 = 20;
    public const double Space6 = 24;

    // Corner radii. The pill's corners grow from compact to panel as it opens.
    public const float CompactRadius = 18;
    public const float PanelRadius = 30;
    public const double ChipRadius = 16;
    public const double TileRadius = 14;
    public const double ArtRadius = 12;

    // Type ramp (Segoe UI Variable). Numbers that change use tabular figures so they don't jiggle.
    public const double Caption = 11;
    public const double Body = 13;
    public const double Title = 15;
    public const double Display = 34;
    public const double Hero = 44;

    // Text emphasis on the dark surface: primary, secondary and tertiary.
    public const byte TextHigh = 0xFF;
    public const byte TextMedium = 0xA3;
    public const byte TextLow = 0x66;

    // Chip and control sizes.
    public const double ChipHeight = 32;
    public const double IconButton = 36;
}
