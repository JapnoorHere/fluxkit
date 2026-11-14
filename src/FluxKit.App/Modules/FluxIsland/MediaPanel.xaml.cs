using FluxKit.App.Modules.FluxIsland.Design;
using FluxKit.Tools.FluxIsland.Abstractions;
using FluxKit.Tools.FluxIsland.Activities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.UI;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>The music page. It shows the scene's music and sends button presses to <see cref="Commands"/>.</summary>
public sealed partial class MediaPanel : UserControl, IIslandPage
{
    private const string PlayGlyph = "\xE768";
    private const string PauseGlyph = "\xE769";
    private const double TrackThin = 4;
    private const double TrackThick = 7;
    private static readonly Size PlayingSize = new(420, 188);
    private static readonly Size EmptySize = new(420, 140);

    private AlbumArt? _art;
    private MediaInfo? _media;
    private double _progress;

    public MediaPanel()
    {
        InitializeComponent();
        var secondary = new SolidColorBrush(IslandPalette.TextSecondary);
        Artist.Foreground = secondary;
        Elapsed.Foreground = secondary;
        Remaining.Foreground = secondary;
        ArtFallback.Foreground = secondary;
        EmptyGlyph.Foreground = new SolidColorBrush(IslandPalette.Pink);
        EmptyHint.Foreground = secondary;

        PreviousButton.Click += (_, _) => Commands?.PreviousTrack();
        NextButton.Click += (_, _) => Commands?.NextTrack();
        PlayPauseButton.Click += (_, _) => Commands?.PlayPause();
        PressFeedback.Attach(PreviousButton);
        PressFeedback.Attach(PlayPauseButton);
        PressFeedback.Attach(NextButton);
        Track.PointerPressed += OnTrackPressed;
        Track.PointerEntered += (_, _) => SetTrackThickness(TrackThick);
        Track.PointerExited += (_, _) => SetTrackThickness(TrackThin);
        Track.SizeChanged += (_, _) => ApplyProgress();
    }

    public IIslandCommands? Commands { get; set; }

    UIElement[] IIslandPage.Rows => EmptyState.Visibility == Visibility.Visible ? [EmptyState] : [TrackRow, SeekRow, ControlsRow];

    Size IIslandPage.PreferredSize => _media is null ? EmptySize : PlayingSize;

    string IIslandPage.State => _media is null ? "empty" : "playing";

    string IIslandPage.Update(IslandScene scene, DateTimeOffset now, bool springs)
    {
        if (scene.Media is not { } media)
        {
            ShowEmpty(springs);
            return Strings.Get("Island_NothingPlayingName");
        }

        Show(media, now, springs);
        return string.Join(", ", new[] { media.Title, media.Artist }.Where(t => !string.IsNullOrEmpty(t)));
    }

    /// <summary>Shares the pill's art, so each track is decoded once for both the pill and the panel.</summary>
    internal void UseArt(AlbumArt art)
    {
        _art = art;
        art.Changed += (_, _) => ApplyArt();
        ApplyArt();
    }

    private void ShowEmpty(bool springs)
    {
        _media = null;
        Bars.SetPlaying(false, springs);
        ContentGrid.Visibility = Visibility.Collapsed;
        Glow.Visibility = Visibility.Collapsed;
        EmptyState.Visibility = Visibility.Visible;
    }

    private void Show(MediaInfo media, DateTimeOffset now, bool springs)
    {
        _media = media;
        ContentGrid.Visibility = Visibility.Visible;
        Glow.Visibility = Visibility.Visible;
        EmptyState.Visibility = Visibility.Collapsed;
        Title.Text = string.IsNullOrWhiteSpace(media.Title) ? Strings.Get("Island_Media_Untitled") : media.Title;
        Artist.Text = media.Artist;
        Artist.Visibility = string.IsNullOrEmpty(media.Artist) ? Visibility.Collapsed : Visibility.Visible;
        Bars.SetPlaying(media.IsPlaying, springs);
        PlayPauseGlyph.Glyph = media.IsPlaying ? PauseGlyph : PlayGlyph;
        AutomationProperties.SetName(PlayPauseButton, Strings.Get(media.IsPlaying ? "Island_Pause" : "Island_Play"));
        PreviousButton.IsEnabled = media.CanGoPrevious;
        NextButton.IsEnabled = media.CanGoNext;

        bool timed = media.Duration > TimeSpan.Zero;
        SeekRow.Visibility = timed ? Visibility.Visible : Visibility.Collapsed;
        if (timed)
        {
            TimeSpan position = media.EstimatedPosition(now);
            Elapsed.Text = IslandPresenter.FormatPosition(position);
            Remaining.Text = Strings.Format("Island_RemainingFormat", IslandPresenter.FormatPosition(media.Duration - position));
            _progress = position / media.Duration;
            ApplyProgress();
        }
    }

    private void ApplyArt()
    {
        Color accent = _art?.Accent ?? IslandPalette.Pink;
        Art.Source = _art?.Image;
        ArtFallback.Visibility = Art.Source is null ? Visibility.Visible : Visibility.Collapsed;
        GlowStart.Color = IslandPalette.WithAlpha(accent, 0x70);
        GlowEnd.Color = IslandPalette.WithAlpha(accent, 0x30);
        TrackFill.Background = new SolidColorBrush(accent);
        Bars.SetColor(accent);
    }

    /// <summary>The scrubber thickens under the pointer, inviting a drag or a click.</summary>
    private void SetTrackThickness(double height)
    {
        TrackBase.Height = height;
        TrackFill.Height = height;
        TrackBase.CornerRadius = new CornerRadius(height / 2);
        TrackFill.CornerRadius = new CornerRadius(height / 2);
    }

    private void ApplyProgress() => TrackFill.Width = Math.Clamp(_progress, 0, 1) * Track.ActualWidth;

    private void OnTrackPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_media is not { CanSeek: true } media || media.Duration <= TimeSpan.Zero || Track.ActualWidth <= 0)
        {
            return;
        }

        // Jump the bar at once so the click feels instant; the player's own update follows.
        _progress = Math.Clamp(e.GetCurrentPoint(Track).Position.X / Track.ActualWidth, 0, 1);
        ApplyProgress();
        Commands?.Seek(media.Duration * _progress);
        e.Handled = true;
    }
}
