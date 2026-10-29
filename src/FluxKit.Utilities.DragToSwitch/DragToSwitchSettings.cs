using FluxKit.Core.Settings;

namespace FluxKit.Utilities.DragToSwitch;

public enum SelectorTrigger
{
    /// <summary>Show as soon as a file drag is detected.</summary>
    OnDragStart,

    /// <summary>Show only when the dragged item reaches the selector's screen edge.</summary>
    EdgeSummon,
}

public enum DragSources
{
    ExplorerAndDesktop,
    AnyApp,
}

public enum OverlayPosition
{
    BottomCenter,
    TopCenter,
    NearCursor,
}

public enum OverlaySize
{
    Compact,
    Standard,
}

public enum PreviewStyle
{
    Wallpaper,
    None,
}

public enum OverlayTheme
{
    FollowApp,
    System,
    Light,
    Dark,
}

/// <summary>Persisted settings for Drag-to-Switch ("utilities/drag-to-switch").</summary>
public sealed class DragToSwitchSettings : ISettingsSection
{
    public const string Section = "utilities/drag-to-switch";
    public const int MinHoverDelayMs = 200;
    public const int MaxHoverDelayMs = 1500;
    public const int DefaultHoverDelayMs = 600;

    public bool Enabled { get; set; } = true;

    public SelectorTrigger Trigger { get; set; } = SelectorTrigger.OnDragStart;

    public int HoverDelayMs { get; set; } = DefaultHoverDelayMs;

    public DragSources Sources { get; set; } = DragSources.ExplorerAndDesktop;

    /// <summary>Dragging an app window by its title bar shows the selector and takes the window along.</summary>
    public bool MoveWindowsEnabled { get; set; } = true;

    public bool NumberKeysEnabled { get; set; } = true;

    public bool ShowWithSingleDesktop { get; set; }

    public OverlayPosition OverlayPosition { get; set; } = OverlayPosition.BottomCenter;

    public OverlaySize OverlaySize { get; set; } = OverlaySize.Standard;

    public PreviewStyle PreviewStyle { get; set; } = PreviewStyle.Wallpaper;

    public OverlayTheme OverlayTheme { get; set; } = OverlayTheme.FollowApp;

    public bool AnimationsEnabled { get; set; } = true;

    public bool CollapseAfterSwitch { get; set; } = true;

    public bool ReturnToOriginOnCancel { get; set; }

    public IEnumerable<string> Normalize()
    {
        int clamped = Math.Clamp(HoverDelayMs, MinHoverDelayMs, MaxHoverDelayMs);
        if (clamped != HoverDelayMs)
        {
            HoverDelayMs = clamped;
            yield return $"hoverDelayMs clamped to {clamped}";
        }

        foreach (string fix in FixEnum(Trigger, v => Trigger = v, SelectorTrigger.OnDragStart, "trigger")
            .Concat(FixEnum(Sources, v => Sources = v, DragSources.ExplorerAndDesktop, "sources"))
            .Concat(FixEnum(OverlayPosition, v => OverlayPosition = v, OverlayPosition.BottomCenter, "overlayPosition"))
            .Concat(FixEnum(OverlaySize, v => OverlaySize = v, OverlaySize.Standard, "overlaySize"))
            .Concat(FixEnum(PreviewStyle, v => PreviewStyle = v, PreviewStyle.Wallpaper, "previewStyle"))
            .Concat(FixEnum(OverlayTheme, v => OverlayTheme = v, OverlayTheme.FollowApp, "overlayTheme")))
        {
            yield return fix;
        }
    }

    public DragToSwitchSettings Clone() => (DragToSwitchSettings)MemberwiseClone();

    private static IEnumerable<string> FixEnum<T>(T value, Action<T> set, T fallback, string name) where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            set(fallback);
            yield return $"{name} reset to {fallback}";
        }
    }
}
