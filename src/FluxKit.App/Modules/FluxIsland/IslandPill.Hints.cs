using FluxKit.Tools.FluxIsland;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>
/// Teaching without a manual: a short tip appears under the pill the first time each gesture is useful (the bubble
/// can be swapped, an alert can be waved away, the wheel changes the volume, the panel has more pages). Each tip
/// shows once; the window saves which ones were seen. The first-run tour uses the same tip.
/// </summary>
public sealed partial class IslandPill
{
    private const double HintGap = 10;
    private static readonly TimeSpan HintDuration = TimeSpan.FromSeconds(4);

    private IslandHints _hintsShown;
    private DispatcherQueueTimer? _hintTimer;
    private Size _size;

    /// <summary>A one-time tip was shown; the window records it so it never shows again.</summary>
    public event EventHandler<IslandHints>? HintSeen;

    /// <summary>Shows <paramref name="text"/> under the pill for a few seconds (or until <see cref="HideHint"/>).</summary>
    /// <param name="step">For the tour: which step this is (1-based) and how many there are, shown as dots.</param>
    public void ShowHint(string text, TimeSpan? duration = null, (int Index, int Count)? step = null)
    {
        HintText.Text = text;
        HintBadge.Show(step is null ? "\xE82F" : "\xE7E7", step is null ? IslandPalette.Yellow : IslandPalette.Blue);
        HintSteps.Visibility = step is null ? Visibility.Collapsed : Visibility.Visible;
        if (step is { } at)
        {
            HintSteps.Text = string.Concat(Enumerable.Range(1, at.Count).Select(i => i == at.Index ? "\u25CF" : "\u25CB"));
        }

        Hint.Visibility = Visibility.Visible;
        Hint.Measure(new Size(StageWidth, double.PositiveInfinity));
        PlaceHint();
        IslandMotion.Enter(Hint, TimeSpan.Zero, Springs);
        _hintTimer ??= OneShot(HintDuration, HideHint);
        _hintTimer.Stop();
        _hintTimer.Interval = duration ?? HintDuration;
        _hintTimer.Start();
        MorphTo(_size);
    }

    public void HideHint()
    {
        _hintTimer?.Stop();
        if (Hint.Visibility == Visibility.Visible)
        {
            Hint.Visibility = Visibility.Collapsed;
            MorphTo(_size);
        }
    }

    /// <summary>Offers the tip that fits what is on screen, if it hasn't been seen yet. Never in previews.</summary>
    private void OfferHint()
    {
        if (Commands is null || !_active || Hint.Visibility == Visibility.Visible)
        {
            return;
        }

        IslandHints seen = _scene.Settings.SeenHints | _hintsShown;
        (IslandHints hint, string key)? offer = _mode switch
        {
            PillMode.Compact when _scene.Secondary is not null && !seen.HasFlag(IslandHints.Bubble) => (IslandHints.Bubble, "Island_HintBubble"),
            PillMode.Compact when _scene.Primary is { IsAlert: true } && !seen.HasFlag(IslandHints.Dismiss) => (IslandHints.Dismiss, "Island_HintDismiss"),
            PillMode.Peek when _scene.Settings.ScrollForVolume && !seen.HasFlag(IslandHints.Volume) => (IslandHints.Volume, "Island_HintVolume"),
            PillMode.Panel when PanelRail.Visibility == Visibility.Visible && !seen.HasFlag(IslandHints.Pages) => (IslandHints.Pages, "Island_HintPages"),
            _ => null,
        };

        if (offer is { } tip)
        {
            _hintsShown |= tip.hint;
            ShowHint(Strings.Get(tip.key));
            HintSeen?.Invoke(this, tip.hint);
        }
    }

    /// <summary>Puts the tip just under the pill, wherever its bottom edge is now.</summary>
    private void PlaceHint() => Hint.Margin = new Thickness(0, TopInset + _size.Height + HintGap, 0, 0);

    /// <summary>The tip's area in stage pixels, for the window's click region.</summary>
    private Rect HintBounds()
    {
        Size hint = Hint.DesiredSize;
        return new Rect((StageWidth - hint.Width) / 2, TopInset + _size.Height + HintGap, hint.Width, hint.Height);
    }
}
