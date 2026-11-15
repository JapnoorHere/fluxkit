using FluxKit.Tools.FluxIsland.Activities;
using Windows.UI;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>
/// The island's colours. The pill is black whatever the app theme, like a phone's island blending into the screen
/// edge, so these are fixed values rather than theme resources. Every moment has one identity colour
/// (<see cref="AccentFor"/>) used for its icon, its glow and its progress, so it is recognised at a glance.
/// </summary>
internal static class IslandPalette
{
    /// <summary>The body: true black, so it reads as part of the screen's edge.</summary>
    public static readonly Color Surface = Color.FromArgb(0xFF, 0x00, 0x00, 0x00);

    /// <summary>The Glass look's body: dark smoked glass, about 90 % opaque, so text keeps its contrast.</summary>
    public static readonly Color GlassSurface = Color.FromArgb(0xE8, 0x16, 0x16, 0x1C);

    /// <summary>The Glass look's brighter rim.</summary>
    public static readonly Color GlassRim = Color.FromArgb(0x7A, 0xFF, 0xFF, 0xFF);

    /// <summary>Raised areas inside the panel (art frames, tiles).</summary>
    public static readonly Color Raised = Color.FromArgb(0xFF, 0x1A, 0x1A, 0x1F);

    /// <summary>A chip at rest, on hover and pressed.</summary>
    public static readonly Color Chip = Color.FromArgb(0x1C, 0xFF, 0xFF, 0xFF);

    public static readonly Color ChipHover = Color.FromArgb(0x2E, 0xFF, 0xFF, 0xFF);

    public static readonly Color ChipPressed = Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF);

    /// <summary>Dividers and tracks.</summary>
    public static readonly Color Hairline = Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF);

    /// <summary>The top of the edge light, fading to nothing further down.</summary>
    public static readonly Color EdgeLight = Color.FromArgb(0x3D, 0xFF, 0xFF, 0xFF);

    /// <summary>Kept for older callers: the faint 1 px inner edge.</summary>
    public static readonly Color Highlight = Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF);

    public static readonly Color TextPrimary = Color.FromArgb(0xFF, 0xF5, 0xF5, 0xF7);

    public static readonly Color TextSecondary = Color.FromArgb(0xFF, 0xA1, 0xA1, 0xAA);

    public static readonly Color TextTertiary = Color.FromArgb(0xFF, 0x6B, 0x6B, 0x74);

    public static readonly Color Green = Color.FromArgb(0xFF, 0x30, 0xD1, 0x58);

    public static readonly Color Red = Color.FromArgb(0xFF, 0xFF, 0x45, 0x3A);

    public static readonly Color Orange = Color.FromArgb(0xFF, 0xFF, 0x9F, 0x0A);

    public static readonly Color Yellow = Color.FromArgb(0xFF, 0xFF, 0xD6, 0x0A);

    public static readonly Color Cyan = Color.FromArgb(0xFF, 0x3C, 0xCD, 0xEB);

    public static readonly Color Blue = Color.FromArgb(0xFF, 0x0A, 0x84, 0xFF);

    public static readonly Color Violet = Color.FromArgb(0xFF, 0x8A, 0x84, 0xFF);

    public static readonly Color Pink = Color.FromArgb(0xFF, 0xFF, 0x37, 0x5F);

    /// <summary>The identity colour of each moment. Music uses its album's colour instead, when there is art.</summary>
    public static Color AccentFor(ActivityKind kind) => kind switch
    {
        ActivityKind.Media => Pink,
        ActivityKind.Timer or ActivityKind.TimerDone or ActivityKind.Stopwatch => Orange,
        ActivityKind.Shelf => Violet,
        ActivityKind.PluggedIn => Green,
        ActivityKind.LowBattery or ActivityKind.Offline or ActivityKind.MicMuted => Red,
        ActivityKind.BatterySaver => Yellow,
        ActivityKind.Online or ActivityKind.AudioDevice or ActivityKind.DeviceConnected => Cyan,
        ActivityKind.Download or ActivityKind.DownloadDone or ActivityKind.DesktopSwitched => Blue,
        ActivityKind.Screenshot or ActivityKind.Copied => Violet,
        _ => TextPrimary,
    };

    /// <summary>
    /// How strongly the glow shows for a moment: alerts flare, things that last glow softly, and quiet
    /// status moments (volume, lock keys) don't glow at all.
    /// </summary>
    public static float GlowFor(IslandActivity activity) => activity.Kind switch
    {
        ActivityKind.Volume or ActivityKind.Brightness or ActivityKind.CapsLock or ActivityKind.NumLock or ActivityKind.Unplugged => 0,
        _ when activity.IsAlert => 1,
        _ => 0.45f,
    };

    /// <summary><paramref name="color"/> with a new alpha.</summary>
    public static Color WithAlpha(Color color, byte alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);
}
