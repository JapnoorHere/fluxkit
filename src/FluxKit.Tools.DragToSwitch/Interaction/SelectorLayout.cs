using FluxKit.Platform.Windows;

namespace FluxKit.Tools.DragToSwitch.Interaction;

/// <summary>One desktop card as shown in the selector.</summary>
public sealed record CardModel(Guid DesktopId, int Number, string Name, string? WallpaperPath, bool IsCurrent);

/// <summary>Computed geometry of the selector, in physical pixels. Pure and deterministic.</summary>
public sealed record SelectorGeometry(PixelRect Overlay, IReadOnlyList<PixelRect> Cards, PixelRect Pill, double Scale, bool AnchoredBottom)
{
    /// <summary>Size constants in device-independent pixels (the spec in Docs/PLAN.md §8.2).</summary>
    public const int Padding = 16;
    public const int HeaderHeight = 40;
    public const int Gap = 12;
    public const int EdgeMargin = 16;
    public const int CursorOffset = 48;
    public const int PillWidth = 260;
    public const int PillHeight = 44;
    public const int MinCardWidth = 112;

    /// <summary>
    /// Card size in DIPs. With a preview, the height is a 16:9 preview of the card width (minus 6 px insets)
    /// plus a 6 px accent-bar row and a 64 px label area, so text never touches the card edge.
    /// </summary>
    public static (int Width, int Height) CardSize(OverlaySize size, PreviewStyle preview) => (size, preview) switch
    {
        (_, PreviewStyle.None) => (size == OverlaySize.Compact ? 160 : 208, 72),
        (OverlaySize.Compact, _) => (160, 160),
        _ => (208, 188),
    };
}

public static class SelectorLayout
{
    /// <summary>Lays out <paramref name="count"/> cards on the monitor work area.</summary>
    public static SelectorGeometry Compute(int count, OverlaySize size, PreviewStyle preview, OverlayPosition position, PixelRect workArea, double scale, int cursorX, int cursorY)
    {
        count = Math.Max(1, count);
        (int cardW, int cardH) = SelectorGeometry.CardSize(size, preview);

        // Shrink cards if they would not fit in 90% of the work area width.
        double maxWidthDip = workArea.Width * 0.9 / scale;
        double neededDip = (SelectorGeometry.Padding * 2) + (count * cardW) + ((count - 1) * SelectorGeometry.Gap);
        if (neededDip > maxWidthDip)
        {
            cardW = Math.Max(SelectorGeometry.MinCardWidth, (int)((maxWidthDip - (SelectorGeometry.Padding * 2) - ((count - 1) * SelectorGeometry.Gap)) / count));
        }

        int width = Px((SelectorGeometry.Padding * 2) + (count * cardW) + ((count - 1) * SelectorGeometry.Gap), scale);
        int height = Px((SelectorGeometry.Padding * 2) + SelectorGeometry.HeaderHeight + cardH, scale);
        int margin = Px(SelectorGeometry.EdgeMargin, scale);

        int left, top;
        bool bottom;
        switch (position)
        {
            case OverlayPosition.TopCenter:
                left = workArea.Left + ((workArea.Width - width) / 2);
                top = workArea.Top + margin;
                bottom = false;
                break;
            case OverlayPosition.NearCursor:
                left = cursorX - (width / 2);
                top = cursorY - Px(SelectorGeometry.CursorOffset, scale) - height;
                bottom = true;
                if (top < workArea.Top + margin)
                {
                    top = cursorY + Px(SelectorGeometry.CursorOffset, scale);
                    bottom = false;
                }

                break;
            default:
                left = workArea.Left + ((workArea.Width - width) / 2);
                top = workArea.Bottom - margin - height;
                bottom = true;
                break;
        }

        left = Math.Clamp(left, workArea.Left + margin, Math.Max(workArea.Left + margin, workArea.Right - margin - width));
        top = Math.Clamp(top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - height));
        var overlay = new PixelRect(left, top, left + width, top + height);

        var cards = new List<PixelRect>(count);
        int cardTop = top + Px(SelectorGeometry.Padding + SelectorGeometry.HeaderHeight, scale);
        for (int i = 0; i < count; i++)
        {
            int cardLeft = left + Px(SelectorGeometry.Padding + (i * (cardW + SelectorGeometry.Gap)), scale);
            cards.Add(new PixelRect(cardLeft, cardTop, cardLeft + Px(cardW, scale), cardTop + Px(cardH, scale)));
        }

        int pillW = Px(SelectorGeometry.PillWidth, scale), pillH = Px(SelectorGeometry.PillHeight, scale);
        int pillLeft = left + ((width - pillW) / 2);
        int pillTop = bottom ? overlay.Bottom - pillH : overlay.Top;
        return new SelectorGeometry(overlay, cards, new PixelRect(pillLeft, pillTop, pillLeft + pillW, pillTop + pillH), scale, bottom);
    }

    /// <summary>True when the point is inside the edge zone that summons the selector (EdgeSummon trigger).</summary>
    public static bool IsInSummonZone(OverlayPosition position, PixelRect workArea, double scale, int x, int y)
    {
        int zone = Px(64, scale);
        return position == OverlayPosition.TopCenter ? y <= workArea.Top + zone : y >= workArea.Bottom - zone;
    }

    private static int Px(double dip, double scale) => (int)Math.Round(dip * scale);
}
