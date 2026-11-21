using System.Numerics;
using FluxKit.App.Modules.FluxIsland.Design;
using FluxKit.Tools.FluxIsland.Activities;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>
/// Physical gestures (plan E and F):
/// <list type="bullet">
/// <item>Flick: grab the moment on the pill and throw it. Down puts it away into its page, up lets it evaporate, sideways
/// snoozes it for ten minutes. While held, the pill follows the pointer on a short leash; let go and the moment keeps
/// its momentum as it leaves.</item>
/// <item>Time scrub: scroll sideways over the pill and it shows today's moments one by one, newest first, with a
/// rubber-band at either end. Click to act on the one showing; leave it and it rolls back.</item>
/// </list>
/// </summary>
public sealed partial class IslandPill
{
    /// <summary>How far the pill follows the pointer while a moment is held, before it resists.</summary>
    private const double Leash = 14;

    /// <summary>How far down a lasting activity must be pulled before it tears off into a droplet.</summary>
    private const double TearDistance = 36;

    private static readonly TimeSpan ScrubLinger = TimeSpan.FromMilliseconds(1600);

    private bool _flickArmed;
    private bool _flicking;
    private Point _flickFrom;
    private Point _flickLast;
    private long _flickLastTick;
    private Vector2 _flickSpeed;
    private ScrubModel? _scrub;
    private bool _previewScrub;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _scrubTimer;

    /// <summary>On press: a moment showing on the compact pill can be grabbed and thrown.</summary>
    private void ArmFlick(PointerRoutedEventArgs e)
    {
        // Alerts can be thrown; music, a timer or a stopwatch can be pulled down and torn off into a droplet.
        _flickArmed = _mode == PillMode.Compact && _scrub is null
            && _scene.Primary is { } held && (held.IsAlert || DetachedActivities.CanDetach(held.Kind));
        _flicking = false;
        if (_flickArmed)
        {
            _flickFrom = _flickLast = e.GetCurrentPoint(null).Position;
            _flickLastTick = Environment.TickCount64;
            _flickSpeed = Vector2.Zero;
            Surface.CapturePointer(e.Pointer);
        }
    }

    /// <summary>While held: the pill follows the pointer on a leash. Returns true when the move belongs to a throw.</summary>
    private bool MoveFlick(PointerRoutedEventArgs e)
    {
        if (!_flickArmed || !_pressed)
        {
            return false;
        }

        Point at = e.GetCurrentPoint(null).Position;
        long tick = Environment.TickCount64;
        float elapsed = Math.Max(1, tick - _flickLastTick);
        var speed = new Vector2((float)((at.X - _flickLast.X) / elapsed), (float)((at.Y - _flickLast.Y) / elapsed));
        _flickSpeed = Vector2.Lerp(_flickSpeed, speed, 0.6f);
        _flickLast = at;
        _flickLastTick = tick;

        double dx = at.X - _flickFrom.X;
        double dy = at.Y - _flickFrom.Y;
        if (!_flicking && Math.Max(Math.Abs(dx), Math.Abs(dy)) < FlickRules.TapSlop)
        {
            return false;
        }

        _flicking = true;
        _peekTimer.Stop();
        Visual stage = ElementCompositionPreview.GetElementVisual(Stage);
        stage.StopAnimation("Translation");
        stage.Properties.InsertVector3("Translation", new Vector3((float)Resist(dx), (float)Resist(dy), 0));
        return true;
    }

    /// <summary>On release: throws the moment if it was flung, else springs back. Returns true when it was a throw or drag.</summary>
    private bool EndFlick(PointerRoutedEventArgs e)
    {
        bool armed = _flickArmed;
        bool flicking = _flicking;
        _flickArmed = false;
        _flicking = false;
        if (armed)
        {
            Surface.ReleasePointerCapture(e.Pointer);
        }

        if (!flicking)
        {
            return false;
        }

        Point at = e.GetCurrentPoint(null).Position;
        // A pause before letting go means no momentum is left.
        Vector2 speed = Environment.TickCount64 - _flickLastTick > 80 ? Vector2.Zero : _flickSpeed;
        Flick flick = FlickRules.Classify(at.X - _flickFrom.X, at.Y - _flickFrom.Y, speed.X, speed.Y);
        _shape.ScaleTo(_hovering ? HoverScale : 1, Springs);
        if (_scene.Primary is { IsAlert: false } lasting && DetachedActivities.CanDetach(lasting.Kind))
        {
            if (at.Y - _flickFrom.Y >= TearDistance)
            {
                TearOff(lasting.Kind);
            }
            else
            {
                SpringStageBack();
            }

            return true;
        }

        if (flick == Flick.None || _scene.Primary is not { IsAlert: true } moment)
        {
            SpringStageBack();
            return true;
        }

        Throw(flick, speed, moment);
        return true;
    }

    /// <summary>The moment leaves the way it was thrown, keeping its speed, then the action runs and the pill settles.</summary>
    private void Throw(Flick flick, Vector2 speed, IslandActivity moment)
    {
        Visual content = ElementCompositionPreview.GetElementVisual(CompactView);
        Compositor compositor = content.Compositor;
        Vector2 direction = flick switch
        {
            Flick.Down => new Vector2(0, 1),
            Flick.Up => new Vector2(0, -1),
            _ => new Vector2(MathF.Sign(speed.X == 0 ? 1 : speed.X), 0),
        };
        float distance = Math.Clamp(speed.Length() * 120, 18, 44);
        CompositionScopedBatch batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        Vector3KeyFrameAnimation leave = compositor.CreateVector3KeyFrameAnimation();
        leave.InsertKeyFrame(1, new Vector3(direction * distance, 0), IslandSprings.EaseIn(compositor));
        leave.Duration = TimeSpan.FromMilliseconds(Springs ? 200 : 1);
        content.StartAnimation("Translation", leave);
        ScalarKeyFrameAnimation fade = compositor.CreateScalarKeyFrameAnimation();
        fade.InsertKeyFrame(1, 0);
        fade.Duration = leave.Duration;
        content.StartAnimation("Opacity", fade);
        if (flick == Flick.Up)
        {
            // Evaporating: it also shrinks as it rises.
            content.CenterPoint = new Vector3((float)CompactView.ActualWidth / 2, (float)CompactView.ActualHeight / 2, 0);
            Vector3KeyFrameAnimation shrink = compositor.CreateVector3KeyFrameAnimation();
            shrink.InsertKeyFrame(1, new Vector3(0.8f, 0.8f, 1));
            shrink.Duration = leave.Duration;
            content.StartAnimation("Scale", shrink);
        }

        batch.End();
        batch.Completed += (_, _) =>
        {
            switch (flick)
            {
                case Flick.Down:
                    // Put away: a drop of it drips into the island, and it stays in Recent (or the Notifications page).
                    MakeLiquidRoom();
                    _shape.Shed(Springs);
                    Commands?.DismissAlert();
                    break;
                case Flick.Up when moment.Kind == ActivityKind.Notification:
                    Commands?.DismissNotification(unchecked((uint)moment.Total));
                    break;
                case Flick.Up:
                    Commands?.DismissAlert();
                    break;
                default:
                    Commands?.SnoozeAlert();
                    break;
            }

            content.StopAnimation("Translation");
            content.StopAnimation("Opacity");
            content.StopAnimation("Scale");
            content.Properties.InsertVector3("Translation", Vector3.Zero);
            content.Opacity = 1;
            content.Scale = Vector3.One;
            SpringStageBack();
        };
    }

    /// <summary>
    /// Pulled far enough down: a drop of the island breaks off under the pointer and becomes a floating droplet that
    /// holds <paramref name="kind"/> (the droplet window pops in where the pointer is).
    /// </summary>
    private void TearOff(ActivityKind kind)
    {
        MakeLiquidRoom();
        _shape.Shed(Springs);
        SpringStageBack();
        (int x, int y) = FluxKit.Platform.Windows.WindowHelpers.GetCursorPosition();
        double scale = XamlRoot?.RasterizationScale ?? 1;
        Commands?.DetachActivity(
            kind,
            x - (int)((DropletWindow.DropletWidth / 2) * scale),
            y - (int)((DropletWindow.DropletHeight / 2) * scale));
    }

    /// <summary>
    /// Sideways scroll over a pill with nothing beside it: steps through today's moments. Returns true when it scrubbed
    /// (otherwise the scroll does what it did before, such as swapping the bubble).
    /// </summary>
    private bool Scrub(int wheelDelta, bool horizontal)
    {
        if (_mode is not (PillMode.Compact or PillMode.Peek) || _scene.Secondary is not null || _scene.Rewind.Count == 0 || wheelDelta == 0)
        {
            return false;
        }

        if (_scrub is null || _scrub.Count != _scene.Rewind.Count)
        {
            _scrub = new ScrubModel(_scene.Rewind.Count);
        }

        int before = _scrub.Index;
        // Scrolling right (or down with Shift) goes back in time.
        _scrub.Scroll((horizontal ? wheelDelta : -wheelDelta) * 0.5);
        Visual stage = ElementCompositionPreview.GetElementVisual(Stage);
        stage.StartAnimation("Translation", IslandSprings.Vector3(stage.Compositor, IslandSprings.Snappy, new Vector3((float)(-_scrub.Overscroll * 0.25), 0, 0)));
        _scrubTimer ??= OneShot(ScrubLinger, EndScrub);
        _scrubTimer.Stop();
        _scrubTimer.Start();
        if (_scrub.Index != before || before < 0)
        {
            _shownKey = null;
            Update();
        }

        return true;
    }

    /// <summary>The moment the scrub is on, or null when not scrubbing.</summary>
    private RewindEntry? ScrubEntry => _scrub is { Index: >= 0 } scrub && scrub.Index < _scene.Rewind.Count ? _scene.Rewind[scrub.Index] : null;

    /// <summary>What the pill shows while scrubbing: the moment as it looked, when it happened, and where in the day it is.</summary>
    private (UIElement View, string Key, Size Size)? BuildScrub(DateTimeOffset now)
    {
        if (ScrubEntry is not { } entry)
        {
            return null;
        }

        CompactContent content = IslandPresenter.Describe(entry.Activity, now);
        string when = ListPanel.Age(entry.At, now);
        content = content with
        {
            Subtitle = content.Subtitle is { } detail ? $"{when} · {detail}" : when,
            Trail = Strings.Format("Island_ScrubPositionFormat", _scrub!.Index + 1, _scrub.Count),
        };
        return FillCompact(content, $"scrub-{_scrub.Index}", twoLines: true, entry.Activity);
    }

    /// <summary>A click while scrubbing acts on the moment showing (open, show in folder…), then the pill rolls back.</summary>
    private bool ClickScrub()
    {
        if (ScrubEntry is not { } entry)
        {
            return false;
        }

        RunAction(entry.Activity);
        EndScrub();
        return true;
    }

    private void EndScrub()
    {
        if (_scrub is null)
        {
            return;
        }

        _scrub = null;
        _scrubTimer?.Stop();
        SpringStageBack();
        _shownKey = null;
        Update();
    }

    private void SpringStageBack()
    {
        Visual stage = ElementCompositionPreview.GetElementVisual(Stage);
        _lean = Vector3.Zero;
        stage.StartAnimation("Translation", IslandSprings.Vector3(stage.Compositor, IslandSprings.Bouncy, Vector3.Zero));
    }

    /// <summary>The leash: follows freely at first, then resists more the further it goes.</summary>
    private static double Resist(double distance) => Math.Sign(distance) * Leash * (1 - Math.Exp(-Math.Abs(distance) / Leash));
}
