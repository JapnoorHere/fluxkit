using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace FluxKit.App.Modules.FluxIsland.Design;

/// <summary>
/// The music seek bar. <see cref="Progress"/> shows where playback is; while the user drags, the bar follows the
/// pointer instead and a bubble shows the time under it, and <see cref="SeekRequested"/> fires on release with the
/// chosen point (0–1). Under the pointer the track thickens and the knob springs in.
/// </summary>
public sealed partial class Scrubber : UserControl
{
    private const double Thin = 4;
    private const double Thick = 8;
    private const double KnobSize = 14;

    private double _progress;
    private double _dragAt;
    private bool _dragging;
    private bool _hovering;

    public Scrubber()
    {
        InitializeComponent();
        ElementCompositionPreview.SetIsTranslationEnabled(Knob, true);
        ElementCompositionPreview.GetElementVisual(Knob).Scale = new Vector3(0, 0, 1);
        Hit.PointerEntered += (_, _) => SetHover(true);
        Hit.PointerExited += (_, _) => SetHover(_dragging);
        Hit.PointerPressed += OnPressed;
        Hit.PointerMoved += OnMoved;
        Hit.PointerReleased += OnReleased;
        Hit.PointerCaptureLost += (_, _) => EndDrag(seek: false);
        SizeChanged += (_, _) => Apply();
        SetAccent(IslandPalette.Pink);
    }

    /// <summary>The user let go at a point (0–1): seek there.</summary>
    public event EventHandler<double>? SeekRequested;

    /// <summary>The track's length, for the time bubble while dragging.</summary>
    public TimeSpan Duration { get; set; }

    /// <summary>False when effects are reduced: the knob appears without a spring.</summary>
    public bool Animate { get; set; } = true;

    /// <summary>Where playback is (0–1). Ignored while the user drags.</summary>
    public double Progress
    {
        get => _progress;
        set
        {
            _progress = Math.Clamp(value, 0, 1);
            if (!_dragging)
            {
                Apply();
            }
        }
    }

    public void SetAccent(Color accent) => Fill.Background = new SolidColorBrush(accent);

    private double Shown => _dragging ? _dragAt : _progress;

    private void Apply()
    {
        double width = ActualWidth;
        Fill.Width = Math.Max(0, Shown * width);
        Knob.Margin = new Thickness(Math.Clamp((Shown * width) - (KnobSize / 2), -KnobSize / 2, width - (KnobSize / 2)), 0, 0, 0);
        if (_dragging)
        {
            BubbleText.Text = IslandPresenter.FormatPosition(Duration * _dragAt);
            Bubble.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            double bubble = Bubble.DesiredSize.Width;
            Bubble.Margin = new Thickness(Math.Clamp((_dragAt * width) - (bubble / 2), 0, Math.Max(0, width - bubble)), -30, 0, 0);
        }
    }

    private void SetHover(bool on)
    {
        if (on == _hovering)
        {
            return;
        }

        _hovering = on;
        double height = on ? Thick : Thin;
        Track.Height = height;
        Fill.Height = height;
        Track.CornerRadius = new CornerRadius(height / 2);
        Fill.CornerRadius = new CornerRadius(height / 2);
        Visual knob = ElementCompositionPreview.GetElementVisual(Knob);
        knob.CenterPoint = new Vector3((float)KnobSize / 2, (float)KnobSize / 2, 0);
        float scale = on ? 1 : 0;
        if (Animate)
        {
            knob.StartAnimation("Scale", IslandSprings.Vector3(knob.Compositor, IslandSprings.Bouncy, new Vector3(scale, scale, 1)));
        }
        else
        {
            knob.Scale = new Vector3(scale, scale, 1);
        }
    }

    private void OnPressed(object sender, PointerRoutedEventArgs e)
    {
        if (ActualWidth <= 0)
        {
            return;
        }

        _dragging = Hit.CapturePointer(e.Pointer);
        _dragAt = Math.Clamp(e.GetCurrentPoint(Hit).Position.X / ActualWidth, 0, 1);
        Bubble.Visibility = Duration > TimeSpan.Zero ? Visibility.Visible : Visibility.Collapsed;
        SetHover(true);
        Apply();
        e.Handled = true;
    }

    private void OnMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging || ActualWidth <= 0)
        {
            return;
        }

        _dragAt = Math.Clamp(e.GetCurrentPoint(Hit).Position.X / ActualWidth, 0, 1);
        Apply();
    }

    private void OnReleased(object sender, PointerRoutedEventArgs e)
    {
        // Seek first: releasing the capture raises "capture lost", which would otherwise cancel the drag.
        EndDrag(seek: true);
        Hit.ReleasePointerCapture(e.Pointer);
        e.Handled = true;
    }

    private void EndDrag(bool seek)
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        Bubble.Visibility = Visibility.Collapsed;
        if (seek)
        {
            // Jump at once so the release feels instant; the player's own update follows.
            _progress = _dragAt;
            SeekRequested?.Invoke(this, _dragAt);
        }

        Apply();
        SetHover(false);
    }
}
