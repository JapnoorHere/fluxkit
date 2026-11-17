using System.Numerics;
using FluxKit.App.Modules.FluxIsland.Design;
using FluxKit.Tools.FluxIsland.Abstractions;
using FluxKit.Tools.FluxIsland.Activities;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.UI;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>The music page. It shows the scene's music and sends button presses to <see cref="Commands"/>.</summary>
public sealed partial class MediaPanel : UserControl, IIslandPage
{
    private const string PlayGlyph = "\xF5B0";
    private const string PauseGlyph = "\xF8AE";
    private static readonly Size PlayingSize = new(420, 214);
    private static readonly Size EmptySize = new(420, 156);

    private AlbumArt? _art;
    private MediaInfo? _media;
    private bool _animate = true;

    public MediaPanel()
    {
        InitializeComponent();
        var secondary = new SolidColorBrush(IslandPalette.TextSecondary);
        Artist.Foreground = secondary;
        Elapsed.Foreground = secondary;
        Remaining.Foreground = secondary;
        ArtFallback.Foreground = secondary;
        EmptyGlyph.Show("\xE8D6", IslandPalette.Pink);
        EmptyHint.Foreground = secondary;
        PlayPauseGlyph.Glyph = PauseGlyph;

        PreviousButton.Click += (_, _) => Commands?.PreviousTrack();
        NextButton.Click += (_, _) => Commands?.NextTrack();
        PlayPauseButton.Click += (_, _) => Commands?.PlayPause();
        PressFeedback.Attach(PreviousButton);
        PressFeedback.Attach(PlayPauseButton);
        PressFeedback.Attach(NextButton);
        Track.SeekRequested += (_, at) =>
        {
            if (_media is { CanSeek: true } media && media.Duration > TimeSpan.Zero)
            {
                Commands?.Seek(media.Duration * at);
            }
        };
    }

    public IIslandCommands? Commands { get; set; }

    UIElement[] IIslandPage.Rows => EmptyState.Visibility == Visibility.Visible ? [EmptyState] : [TrackRow, SeekRow, ControlsRow];

    Size IIslandPage.PreferredSize => _media is null ? EmptySize : PlayingSize;

    string IIslandPage.State => _media is null ? "empty" : "playing";

    string IIslandPage.Update(IslandScene scene, DateTimeOffset now, bool springs)
    {
        _animate = springs;
        Track.Animate = springs;
        if (scene.Media is not { } media)
        {
            ShowEmpty(springs);
            return Strings.Get("Island_NothingPlayingName");
        }

        Show(media, now, springs);
        return string.Join(", ", new[] { media.Title, media.Artist }.Where(t => !string.IsNullOrEmpty(t)));
    }

    /// <summary>Drives the bars from the live output level.</summary>
    internal void SetLevel(float level) => Bars.SetLevel(level);

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
        EmptyState.Visibility = Visibility.Visible;
    }

    private void Show(MediaInfo media, DateTimeOffset now, bool springs)
    {
        _media = media;
        ContentGrid.Visibility = Visibility.Visible;
        EmptyState.Visibility = Visibility.Collapsed;
        Title.Text = string.IsNullOrWhiteSpace(media.Title) ? Strings.Get("Island_Media_Untitled") : media.Title;
        Artist.Text = media.Artist;
        Artist.Visibility = string.IsNullOrEmpty(media.Artist) ? Visibility.Collapsed : Visibility.Visible;
        Bars.SetPlaying(media.IsPlaying, springs);
        string glyph = media.IsPlaying ? PauseGlyph : PlayGlyph;
        if (glyph != PlayPauseGlyph.Glyph)
        {
            PlayPauseGlyph.Glyph = glyph;
            IslandMotion.Pop(PlayPauseGlyph, springs);
        }

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
            Track.Duration = media.Duration;
            Track.Progress = position / media.Duration;
            Track.IsHitTestVisible = media.CanSeek;
        }
    }

    /// <summary>A new track: the art and its colour wash cross-fade in, and the accents take the album's colour.</summary>
    private void ApplyArt()
    {
        Color accent = _art?.Image is null ? IslandPalette.Pink : _art.Accent;
        Art.Source = _art?.Image;
        ArtFallback.Visibility = Art.Source is null ? Visibility.Visible : Visibility.Collapsed;
        GlowStart.Color = IslandPalette.WithAlpha(accent, 0x66);
        Track.SetAccent(accent);
        Bars.SetColor(accent);

        Wash.Source = _art?.Wash;
        Visual wash = ElementCompositionPreview.GetElementVisual(Wash);
        float target = Wash.Source is null ? 0 : 0.9f;
        if (_animate)
        {
            ScalarKeyFrameAnimation fade = wash.Compositor.CreateScalarKeyFrameAnimation();
            fade.InsertKeyFrame(0, 0);
            fade.InsertKeyFrame(1, target, IslandSprings.EaseOut(wash.Compositor));
            fade.Duration = TimeSpan.FromMilliseconds(400);
            wash.StartAnimation("Opacity", fade);
        }
        else
        {
            wash.Opacity = target;
        }

        Glow.Visibility = Wash.Source is null ? Visibility.Visible : Visibility.Collapsed;
    }
}
