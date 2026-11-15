using System.Numerics;
using FluxKit.App.Modules.FluxIsland.Design;
using FluxKit.Tools.FluxIsland.Activities;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>Mouse, wheel and keyboard: hover, peek, click to open, the hover lean, the wheel for volume and pages, and the keyboard shortcut.</summary>
public sealed partial class IslandPill
{
    /// <summary>The mouse wheel over the island changes the volume (the tool ignores it when that setting is off).</summary>
    private void OnWheel(object sender, PointerRoutedEventArgs e)
    {
        PointerPointProperties properties = e.GetCurrentPoint(Surface).Properties;
        bool sideways = properties.IsHorizontalMouseWheel || e.KeyModifiers.HasFlag(Windows.System.VirtualKeyModifiers.Shift);
        if (sideways)
        {
            // Tilt the wheel, swipe sideways on a touchpad or Shift+scroll: on the open panel that turns the page, on
            // the pill it swaps the pill and the bubble. A gesture sends a stream of events; act once per gesture.
            long tick = Environment.TickCount64;
            if (tick - _lastSwap > 450)
            {
                int delta = properties.MouseWheelDelta;
                if (_mode == PillMode.Panel && delta != 0)
                {
                    _lastSwap = tick;
                    StepPage(properties.IsHorizontalMouseWheel ? Math.Sign(delta) : -Math.Sign(delta));
                }
                else if (_mode == PillMode.Compact && _scene.Secondary is not null)
                {
                    _lastSwap = tick;
                    Commands?.SwapActivities();
                }
            }

            e.Handled = true;
            return;
        }

        _wheel += properties.MouseWheelDelta;
        int notches = _wheel / WheelNotch;
        if (notches != 0)
        {
            _wheel -= notches * WheelNotch;
            Commands?.StepVolume(notches);
        }

        e.Handled = true;
    }

    /// <summary>
    /// For the keyboard shortcut: opens the command bar with the caret in it, or closes the island if it is open.
    /// While opened this way it stays open when the pointer is elsewhere, until Esc, the shortcut or a click away.
    /// </summary>
    public void ToggleFromKeyboard()
    {
        if (IsOpen)
        {
            SetMode(PillMode.Compact);
            return;
        }

        _keyboard = true;
        _peekTimer.Stop();
        _closeTimer.Stop();
        CommandView.Reset();
        SetMode(PillMode.Command);
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, CommandView.FocusInput);
    }

    /// <summary>Opens the full panel from the keyboard (Down in an empty command bar) and focuses its first button.</summary>
    private void OpenPanelFromKeyboard()
    {
        SetMode(PillMode.Panel);
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            if (_keyboard && FocusManager.FindFirstFocusableElement(Surface) is DependencyObject first)
            {
                _ = FocusManager.TryFocusAsync(first, FocusState.Keyboard);
            }
        });
    }

    /// <summary>Closes a panel the keyboard opened, e.g. when the user clicks into another window.</summary>
    public void CloseKeyboardPanel()
    {
        if (_keyboard)
        {
            SetMode(PillMode.Compact);
        }
    }

    private void ReleaseKeyboard()
    {
        if (_keyboard)
        {
            _keyboard = false;
            KeyboardReleased?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Esc closes the panel and PageUp/PageDown turn pages. Arrows and Tab move between buttons (set up in XAML).</summary>
    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_mode != PillMode.Panel)
        {
            return;
        }

        switch (e.Key)
        {
            case Windows.System.VirtualKey.Escape:
                e.Handled = true;
                SetMode(PillMode.Compact);
                break;
            case Windows.System.VirtualKey.PageDown:
                e.Handled = true;
                StepPage(1);
                break;
            case Windows.System.VirtualKey.PageUp:
                e.Handled = true;
                StepPage(-1);
                break;
        }
    }

    /// <summary>Right-click waves away the alert on screen, or closes the open panel.</summary>
    private void OnRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (IsOnShelfTile(e.OriginalSource as DependencyObject))
        {
            return; // The tile's own menu handles it.
        }

        e.Handled = true;
        if (IsOpen)
        {
            SetMode(PillMode.Compact);
        }
        else if (_scene.Primary is { IsAlert: true })
        {
            Commands?.DismissAlert();
        }
    }

    private static bool IsOnShelfTile(DependencyObject? element)
    {
        for (DependencyObject? at = element; at is not null; at = VisualTreeHelper.GetParent(at))
        {
            if (at is ShelfTile)
            {
                return true;
            }
        }

        return false;
    }

    private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _hovering = true;
        _closeTimer.Stop();
        if (!IsOpen)
        {
            _shape.ScaleTo(HoverScale, Springs);
            _peekTimer.Start();
        }
    }

    /// <summary>
    /// While hovered, the compact pill leans a few pixels toward the pointer, as if drawn to it. The lean is a spring
    /// on the compositor, started only when it changes by at least half a pixel, so moving the mouse costs almost nothing.
    /// </summary>
    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!Springs || IsOpen || Surface.ActualWidth <= 0)
        {
            return;
        }

        Point at = e.GetCurrentPoint(Surface).Position;
        float x = (float)Math.Clamp((at.X / Surface.ActualWidth) - 0.5, -0.5, 0.5) * 2 * MaxLean;
        float y = (float)Math.Clamp((at.Y / Surface.ActualHeight) - 0.5, -0.5, 0.5) * MaxLean;
        LeanTo(new Vector3(x, y, 0));
    }

    private void LeanTo(Vector3 lean)
    {
        if (Vector3.Distance(lean, _lean) < 0.5f)
        {
            return;
        }

        _lean = lean;
        Visual stage = ElementCompositionPreview.GetElementVisual(Stage);
        stage.StartAnimation("Translation", IslandSprings.Vector3(stage.Compositor, IslandSprings.Smooth, lean));
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        _hovering = false;
        _pressed = false;
        _peekTimer.Stop();
        _shape.ScaleTo(1, Springs);
        LeanTo(Vector3.Zero);
        if (_mode != PillMode.Compact)
        {
            _closeTimer.Start();
        }
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (e.GetCurrentPoint(Surface).Properties.IsMiddleButtonPressed)
        {
            // Middle-click waves an alert away, like closing a tab.
            if (_scene.Primary is { IsAlert: true })
            {
                Commands?.DismissAlert();
            }

            e.Handled = true;
            return;
        }

        if (IsOpen)
        {
            return;
        }

        _pressed = true;
        _shape.ScaleTo(PressScale, Springs);
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_pressed)
        {
            return;
        }

        _pressed = false;
        _peekTimer.Stop();
        SetMode(PillMode.Panel);
    }

    private void Unpress()
    {
        if (_pressed)
        {
            _pressed = false;
            _shape.ScaleTo(_hovering ? HoverScale : 1, Springs);
        }
    }
}
