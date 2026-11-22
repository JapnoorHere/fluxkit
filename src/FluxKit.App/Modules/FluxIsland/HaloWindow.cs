using System.Numerics;
using FluxKit.App.Modules.FluxIsland.Design;
using FluxKit.App.Theming;
using FluxKit.Platform.Windows;
using Microsoft.UI.Composition;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Windows.Graphics;
using Windows.UI;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>
/// The light halo (ADR-015): a pool of the island's colour spilling onto the desktop under it, like a lamp. A separate
/// click-through window just under the island, drawn with one radial gradient: no blur, no per-frame work. Created only
/// when the setting is on, and hidden whenever the island is.
/// </summary>
internal sealed class HaloWindow
{
    /// <summary>The halo's size in view pixels: wide and shallow, spreading out from the top edge.</summary>
    public const double Width = 900;

    public const double Height = 260;

    private readonly Window _window = new();
    private readonly nint _hwnd;
    private readonly SpriteVisual _light;
    private readonly CompositionRadialGradientBrush _brush;
    private readonly CompositionColorGradientStop _core;
    private readonly CompositionColorGradientStop _middle;
    private bool _shown;

    public HaloWindow()
    {
        var root = new Grid();
        _window.Content = root;
        _window.SystemBackdrop = new TransparentBackdrop();
        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
        _window.AppWindow.IsShownInSwitchers = false;
        if (_window.AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
            presenter.IsResizable = false;
            presenter.IsAlwaysOnTop = true;
        }

        WindowHelpers.MakeOverlay(_hwnd);
        WindowHelpers.RemoveFrame(_hwnd);
        WindowHelpers.MakeClickThrough(_hwnd);

        Compositor compositor = ElementCompositionPreview.GetElementVisual(root).Compositor;
        _brush = compositor.CreateRadialGradientBrush();
        _brush.EllipseCenter = new Vector2(0.5f, 0);
        _brush.EllipseRadius = new Vector2(0.5f, 1);
        _core = compositor.CreateColorGradientStop(0, Color.FromArgb(0, 0, 0, 0));
        _middle = compositor.CreateColorGradientStop(0.45f, Color.FromArgb(0, 0, 0, 0));
        _brush.ColorStops.Add(_core);
        _brush.ColorStops.Add(_middle);
        _brush.ColorStops.Add(compositor.CreateColorGradientStop(1, Color.FromArgb(0, 0, 0, 0)));
        _light = compositor.CreateSpriteVisual();
        _light.Brush = _brush;
        _light.RelativeSizeAdjustment = Vector2.One;
        _light.Opacity = 0;
        ElementCompositionPreview.SetElementChildVisual(root, _light);
    }

    /// <summary>
    /// Shows the halo in <paramref name="accent"/> at <paramref name="strength"/> (0 fades it out; the empty
    /// window stays, costing nothing), centred under the island window <paramref name="island"/> whose top centre is at
    /// <paramref name="centreX"/>, <paramref name="top"/>.
    /// </summary>
    public void Show(nint island, int centreX, int top, double scale, Color accent, float strength, bool springs)
    {
        strength = Math.Clamp(strength, 0, 1);
        Compositor compositor = _light.Compositor;
        Fade(_core, IslandPalette.WithAlpha(accent, 0x9A), compositor, springs);
        Fade(_middle, IslandPalette.WithAlpha(accent, 0x3C), compositor, springs);
        if (strength > 0 && !_shown)
        {
            int width = (int)Math.Ceiling(Width * scale);
            int height = (int)Math.Ceiling(Height * scale);
            var bounds = new PixelRect(centreX - (width / 2), top, centreX + (width / 2), top + height);
            _window.AppWindow.MoveAndResize(new RectInt32(bounds.Left, bounds.Top, width, height));
            // Draw only inside the window's edge: at the very edge Windows shows a thin frame line (see the island window).
            int inset = (int)Math.Ceiling(6 * scale);
            WindowHelpers.SetHitRegion(_hwnd, new PixelRect(inset, inset, width - inset, height - inset), 0);
            WindowHelpers.ShowBelow(_hwnd, island, bounds);
            WindowHelpers.RemoveBorder(_hwnd);
            _shown = true;
        }

        if (!springs)
        {
            _light.StopAnimation("Opacity");
            _light.Opacity = strength;
        }
        else
        {
            _light.StartAnimation("Opacity", IslandSprings.Scalar(compositor, IslandSprings.Smooth, strength));
        }
    }

    public void Hide()
    {
        if (_shown)
        {
            _shown = false;
            WindowHelpers.Hide(_hwnd);
        }
    }

    public void Close()
    {
        Hide();
        _window.Close();
    }

    private static void Fade(CompositionColorGradientStop stop, Color to, Compositor compositor, bool springs)
    {
        if (!springs)
        {
            stop.Color = to;
            return;
        }

        ColorKeyFrameAnimation fade = compositor.CreateColorKeyFrameAnimation();
        fade.InsertKeyFrame(1, to);
        fade.Duration = TimeSpan.FromMilliseconds(420);
        stop.StartAnimation("Color", fade);
    }
}
