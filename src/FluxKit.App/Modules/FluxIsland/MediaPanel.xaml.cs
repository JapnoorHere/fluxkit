using FluxKit.Tools.FluxIsland.Abstractions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>The music panel. It shows what <see cref="Update"/> gives it and sends button presses to <see cref="Commands"/>.</summary>
public sealed partial class MediaPanel : UserControl
{
    private const string PlayGlyph = "\xE768";
    private const string PauseGlyph = "\xE769";

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

        PreviousButton.Click += (_, _) => Commands?.PreviousTrack();
        NextButton.Click += (_, _) => Commands?.NextTrack();
        PlayPauseButton.Click += (_, _) => Commands?.PlayPause();
        PressFeedback.Attach(PreviousButton);
        PressFeedback.Attach(PlayPauseButton);
        PressFeedback.Attach(NextButton);
        Track.PointerPressed += OnTrackPressed;
        Track.SizeChanged += (_, _) => ApplyProgress();
    }

    public IIslandCommands? Commands { get; set; }

    /// <summary>False when effects are reduced: the bars stand still.</summary>
    public bool Animate { get; set; } = true;

    /// <summary>The rows, in the order they should appear when the panel opens.</summary>
    internal UIElement[] Rows => [TrackRow, SeekRow, ControlsRow];

    public void Update(MediaInfo media, DateTimeOffset now)
    {
        _media = media;
        Title.Text = string.IsNullOrWhiteSpace(media.Title) ? Strings.Get("Island_Media_Untitled") : media.Title;
        Artist.Text = media.Artist;
        Artist.Visibility = string.IsNullOrEmpty(media.Artist) ? Visibility.Collapsed : Visibility.Visible;
        Bars.SetPlaying(media.IsPlaying, Animate);

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

    /// <summary>Shares the pill's art, so each track is decoded once for both the pill and the panel.</summary>
    internal void UseArt(AlbumArt art)
    {
        _art = art;
        art.Changed += (_, _) => ApplyArt();
        ApplyArt();
    }

    private void ApplyArt()
    {
        Color accent = _art?.Accent ?? IslandPalette.Violet;
        Art.Source = _art?.Image;
        ArtFallback.Visibility = Art.Source is null ? Visibility.Visible : Visibility.Collapsed;
        GlowStart.Color = Color.FromArgb(0x55, accent.R, accent.G, accent.B);
        TrackFill.Background = new SolidColorBrush(accent);
        Bars.SetColor(accent);
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
