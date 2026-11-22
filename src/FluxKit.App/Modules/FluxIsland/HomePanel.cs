using System.Globalization;
using FluxKit.App.Modules.FluxIsland.Design;
using FluxKit.Platform.Stats;
using FluxKit.Tools.FluxIsland.Abstractions;
using FluxKit.Tools.FluxIsland.Activities;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>
/// The Home page: a bento dashboard of what matters now, in one glance. Music on its album's colours, today's date and
/// week, the timer (or quick starts), live system load, and the newest notification or moment. Each tile opens its
/// full page; music and the timer can be played or paused right here. Cards take the theme's colours.
/// </summary>
public sealed partial class HomePanel : UserControl, IIslandPage
{
    private const double PageWidth = 460;
    private const double Gap = 8;
    private static readonly TimeSpan SampleEvery = TimeSpan.FromSeconds(2);

    private readonly Grid _grid = new() { Padding = new Thickness(12, 4, 12, 12), ColumnSpacing = Gap, RowSpacing = Gap };
    private readonly SystemStats _stats = new();
    private readonly DispatcherQueueTimer _sampler;
    private readonly List<Border> _cards = [];

    // Music
    private readonly Border _music;
    private readonly Image _wash = new() { Stretch = Stretch.UniformToFill, Opacity = 0.75 };
    private readonly Image _art = new() { Width = 46, Height = 46, Stretch = Stretch.UniformToFill };
    private readonly TextBlock _songTitle = Text(14, FontWeights.SemiBold);
    private readonly TextBlock _songArtist = Text(12, FontWeights.Normal, secondary: true);
    private readonly FontIcon _playGlyph = new() { FontSize = 14 };
    private readonly Button _play = new() { Width = 36, Height = 36, CornerRadius = new CornerRadius(18), Padding = new Thickness(0) };
    private readonly Rectangle _songProgress = new() { Height = 3, RadiusX = 1.5, RadiusY = 1.5, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly Grid _songTrack = new() { Height = 3 };

    // Calendar
    private readonly TextBlock _day = Text(30, FontWeights.Bold);
    private readonly TextBlock _weekday = Text(11, FontWeights.SemiBold);
    private readonly TextBlock _month = Text(11, FontWeights.Normal, secondary: true);
    private readonly Grid _week = new() { ColumnSpacing = 2 };

    // Timer
    private readonly TextBlock _timerTime = Text(24, FontWeights.SemiBold);
    private readonly TextBlock _timerLabel = Text(11, FontWeights.SemiBold, secondary: true);
    private readonly StackPanel _timerStarts = new() { Orientation = Orientation.Horizontal, Spacing = 6 };
    private readonly Button _timerToggle = new() { Width = 32, Height = 32, CornerRadius = new CornerRadius(16), Padding = new Thickness(0) };
    private readonly FontIcon _timerGlyph = new() { FontSize = 12 };

    // System
    private readonly (Rectangle Bar, TextBlock Value)[] _loads = new (Rectangle, TextBlock)[2];
    private readonly TextBlock _network = Text(11, FontWeights.Normal, secondary: true);

    // Latest
    private readonly FontIcon _latestGlyph = new() { FontSize = 14 };
    private readonly TextBlock _latestText = Text(12.5, FontWeights.Normal);
    private readonly TextBlock _latestWhen = Text(11, FontWeights.Normal, secondary: true);
    private readonly Border _latest;

    private AlbumArt? _albumArt;
    private IslandScene _scene = new();
    private DateOnly _weekShown;

    public HomePanel()
    {
        _grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.35, GridUnitType.Star) });
        _grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(112) });
        _grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(92) });
        _grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(44) });

        _music = Card(BuildMusic(), "media", "Island_HomeMusic");
        _music.Padding = new Thickness(0);
        Place(_music, 0, 0);
        Place(Card(BuildCalendar(), null, "Island_HomeToday"), 0, 1);
        Place(Card(BuildTimer(), "timer", "Island_HomeTimer"), 1, 0);
        Place(Card(BuildSystem(), "glance", "Island_HomeSystem"), 1, 1);
        _latest = Card(BuildLatest(), "latest", "Island_HomeLatest");
        _latest.Padding = new Thickness(12, 0, 12, 0);
        Place(_latest, 2, 0, span: 2);
        Content = _grid;

        _play.Click += (_, _) => Commands?.PlayPause();
        _timerToggle.Click += (_, _) => Commands?.PauseOrResumeTimer();
        foreach (Button button in new[] { _play, _timerToggle })
        {
            PressFeedback.Attach(button);
        }

        _sampler = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _sampler.Interval = SampleEvery;
        _sampler.Tick += (_, _) => Sample();
        Unloaded += (_, _) => _sampler.Stop();
        ApplyTheme(IslandTheme.Current);
    }

    /// <summary>A tile asks to open a page: "media", "timer", "glance" or "latest" (the newest notification or moment).</summary>
    public event EventHandler<string>? PageRequested;

    /// <summary>Unset in previews, where the buttons do nothing.</summary>
    public IIslandCommands? Commands { get; set; }

    UIElement[] IIslandPage.Rows => [.. _cards];

    Size IIslandPage.PreferredSize => new(PageWidth, 4 + 112 + Gap + 92 + Gap + 44 + 12);

    string IIslandPage.State => "home";

    /// <summary>Shares the album art the pill already decoded (its colour wash fills the music tile).</summary>
    internal void UseArt(AlbumArt art)
    {
        _albumArt = art;
        art.Changed += (_, _) => ShowArt();
    }

    /// <summary>Recolours the cards for the theme.</summary>
    internal void ApplyTheme(IslandTheme theme)
    {
        foreach (Border card in _cards)
        {
            card.Background = new SolidColorBrush(theme.Card);
            card.BorderBrush = new SolidColorBrush(theme.CardEdge);
        }

        foreach (Button button in new[] { _play, _timerToggle })
        {
            button.Background = new SolidColorBrush(theme.Chip);
        }
    }

    /// <summary>Samples system load every two seconds, only while Home shows.</summary>
    public void SetLive(bool live)
    {
        if (live && !_sampler.IsRunning)
        {
            Sample();
            _sampler.Start();
        }
        else if (!live)
        {
            _sampler.Stop();
        }
    }

    string IIslandPage.Update(IslandScene scene, DateTimeOffset now, bool springs)
    {
        _scene = scene;
        ShowMusic(scene.Media, now);
        ShowCalendar(now);
        ShowTimer(scene, now);
        ShowLatest(scene, now);
        return Strings.Get("Island_HomeName");
    }

    /// <summary>Shows made-up numbers, for previews (nothing is sampled there).</summary>
    internal void ShowSample(SystemReading reading) => ShowLoad(reading);

    private UIElement BuildMusic()
    {
        var root = new Grid();
        root.Children.Add(_wash);
        var shade = new Border { Background = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 0),
            GradientStops =
            {
                new GradientStop { Color = Color.FromArgb(0xB0, 0, 0, 0), Offset = 0 },
                new GradientStop { Color = Color.FromArgb(0x40, 0, 0, 0), Offset = 1 },
            },
        }, };
        root.Children.Add(shade);

        var content = new Grid { Padding = new Thickness(12), RowSpacing = 8 };
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var top = new Grid { ColumnSpacing = 10 };
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        top.Children.Add(new Border { CornerRadius = new CornerRadius(10), Child = _art, Width = 46, Height = 46 });
        var words = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 1 };
        words.Children.Add(_songTitle);
        words.Children.Add(_songArtist);
        Grid.SetColumn(words, 1);
        top.Children.Add(words);
        _play.Content = _playGlyph;
        _play.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(_play, 2);
        top.Children.Add(_play);
        content.Children.Add(top);

        _songTrack.Children.Add(new Rectangle { Height = 3, RadiusX = 1.5, RadiusY = 1.5, Fill = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)) });
        _songProgress.Fill = new SolidColorBrush(IslandPalette.TextPrimary);
        _songTrack.Children.Add(_songProgress);
        Grid.SetRow(_songTrack, 1);
        content.Children.Add(_songTrack);
        root.Children.Add(content);
        return root;
    }

    private UIElement BuildCalendar()
    {
        var root = new StackPanel { Spacing = 6 };
        var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        head.Children.Add(_day);
        var side = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        _weekday.Foreground = new SolidColorBrush(IslandPalette.Red);
        side.Children.Add(_weekday);
        side.Children.Add(_month);
        head.Children.Add(side);
        root.Children.Add(head);
        for (int i = 0; i < 7; i++)
        {
            _week.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        root.Children.Add(_week);
        return root;
    }

    private UIElement BuildTimer()
    {
        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var words = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 2 };
        words.Children.Add(_timerLabel);
        Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(_timerTime, FontNumeralAlignment.Tabular);
        words.Children.Add(_timerTime);
        words.Children.Add(_timerStarts);
        root.Children.Add(words);
        foreach ((string key, TimeSpan length, bool focus) in new[] { ("Island_HomeFive", TimeSpan.FromMinutes(5), false), ("Island_HomeFocus", TimeSpan.Zero, true) })
        {
            var start = new Button { Content = Strings.Get(key), Height = 26, Padding = new Thickness(10, 0, 10, 0), CornerRadius = new CornerRadius(13), FontSize = 12 };
            start.Click += (_, _) =>
            {
                if (focus)
                {
                    Commands?.StartFocus();
                }
                else
                {
                    Commands?.StartTimer(length);
                }
            };
            start.Loaded += (_, _) => start.Background = new SolidColorBrush(IslandTheme.Current.Chip);
            PressFeedback.Attach(start);
            _timerStarts.Children.Add(start);
        }

        _timerToggle.Content = _timerGlyph;
        _timerToggle.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(_timerToggle, 1);
        root.Children.Add(_timerToggle);
        return root;
    }

    private UIElement BuildSystem()
    {
        var root = new StackPanel { Spacing = 7, VerticalAlignment = VerticalAlignment.Center };
        string[] names = ["Island_HomeCpu", "Island_HomeMemory"];
        for (int i = 0; i < names.Length; i++)
        {
            var row = new Grid { ColumnSpacing = 8 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
            row.Children.Add(Text(11, FontWeights.SemiBold, secondary: true, text: Strings.Get(names[i])));
            var track = new Grid { Height = 6, VerticalAlignment = VerticalAlignment.Center };
            track.Children.Add(new Rectangle { RadiusX = 3, RadiusY = 3, Fill = new SolidColorBrush(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF)) });
            var bar = new Rectangle { RadiusX = 3, RadiusY = 3, HorizontalAlignment = HorizontalAlignment.Left, Width = 0 };
            track.Children.Add(bar);
            Grid.SetColumn(track, 1);
            row.Children.Add(track);
            TextBlock value = Text(11, FontWeights.SemiBold);
            value.HorizontalAlignment = HorizontalAlignment.Right;
            Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(value, FontNumeralAlignment.Tabular);
            Grid.SetColumn(value, 2);
            row.Children.Add(value);
            _loads[i] = (bar, value);
            root.Children.Add(row);
        }

        root.Children.Add(_network);
        return root;
    }

    private UIElement BuildLatest()
    {
        var root = new Grid { ColumnSpacing = 10, VerticalAlignment = VerticalAlignment.Center };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        root.Children.Add(_latestGlyph);
        Grid.SetColumn(_latestText, 1);
        _latestText.VerticalAlignment = VerticalAlignment.Center;
        root.Children.Add(_latestText);
        Grid.SetColumn(_latestWhen, 2);
        _latestWhen.VerticalAlignment = VerticalAlignment.Center;
        root.Children.Add(_latestWhen);
        return root;
    }

    private void ShowMusic(MediaInfo? media, DateTimeOffset now)
    {
        if (media is null)
        {
            _songTitle.Text = Strings.Get("Island_HomeNothingPlaying");
            _songArtist.Text = Strings.Get("Island_HomeNothingPlayingHint");
            _play.Visibility = Visibility.Collapsed;
            _songTrack.Visibility = Visibility.Collapsed;
            ShowArt();
            return;
        }

        _songTitle.Text = string.IsNullOrWhiteSpace(media.Title) ? Strings.Get("Island_Media_Untitled") : media.Title;
        _songArtist.Text = media.Artist;
        _play.Visibility = Visibility.Visible;
        _playGlyph.Glyph = media.IsPlaying ? "\xE769" : "\xE768";
        AutomationProperties.SetName(_play, Strings.Get(media.IsPlaying ? "Island_HomePause" : "Island_HomePlay"));
        bool timed = media.Duration > TimeSpan.Zero;
        _songTrack.Visibility = timed ? Visibility.Visible : Visibility.Collapsed;
        if (timed)
        {
            double share = Math.Clamp(media.EstimatedPosition(now) / media.Duration, 0, 1);
            _songProgress.Width = Math.Max(3, share * ((PageWidth - 24 - Gap) * 1.35 / 2.35 - 24));
        }

        ShowArt();
    }

    private void ShowArt()
    {
        bool playing = _scene.Media is not null && _albumArt?.Image is not null;
        _art.Source = playing ? _albumArt!.Image : null;
        _art.Visibility = playing ? Visibility.Visible : Visibility.Collapsed;
        _wash.Source = _scene.Media is not null ? _albumArt?.Wash : null;
        Color accent = _albumArt?.Accent ?? IslandPalette.Violet;
        _songProgress.Fill = new SolidColorBrush(_scene.Media is not null ? Lighten(accent) : IslandPalette.TextPrimary);
    }

    private void ShowCalendar(DateTimeOffset now)
    {
        DateTime local = now.LocalDateTime;
        CultureInfo culture = CultureInfo.CurrentCulture;
        _day.Text = local.Day.ToString(culture);
        _weekday.Text = local.ToString("dddd", culture).ToUpper(culture);
        _month.Text = local.ToString("MMMM", culture);
        var today = DateOnly.FromDateTime(local);
        if (today == _weekShown)
        {
            return;
        }

        // This week, Monday to Sunday (or as the culture starts it), today in a filled circle.
        _weekShown = today;
        _week.Children.Clear();
        int offset = ((int)local.DayOfWeek - (int)culture.DateTimeFormat.FirstDayOfWeek + 7) % 7;
        DateTime start = local.Date.AddDays(-offset);
        for (int i = 0; i < 7; i++)
        {
            DateTime day = start.AddDays(i);
            bool isToday = day.Date == local.Date;
            var cell = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Spacing = 1 };
            cell.Children.Add(Text(9, FontWeights.SemiBold, secondary: true, text: culture.DateTimeFormat.GetShortestDayName(day.DayOfWeek)[..1]));
            var number = new Border
            {
                Width = 20,
                Height = 20,
                CornerRadius = new CornerRadius(10),
                Background = new SolidColorBrush(isToday ? IslandPalette.Red : Microsoft.UI.Colors.Transparent),
                Child = Text(10.5, isToday ? FontWeights.Bold : FontWeights.Normal, text: day.Day.ToString(culture)),
            };
            ((TextBlock)number.Child).HorizontalAlignment = HorizontalAlignment.Center;
            ((TextBlock)number.Child).VerticalAlignment = VerticalAlignment.Center;
            cell.Children.Add(number);
            Grid.SetColumn(cell, i);
            _week.Children.Add(cell);
        }
    }

    private void ShowTimer(IslandScene scene, DateTimeOffset now)
    {
        FocusTimerSnapshot timer = scene.Timer ?? FocusTimerSnapshot.Idle;
        bool active = timer.IsActive;
        _timerTime.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        _timerToggle.Visibility = _timerTime.Visibility;
        _timerStarts.Visibility = active ? Visibility.Collapsed : Visibility.Visible;
        if (!active)
        {
            _timerLabel.Text = Strings.Get("Island_HomeTimerIdle");
            _timerLabel.Foreground = new SolidColorBrush(IslandPalette.TextSecondary);
            return;
        }

        TimeSpan left = timer.RemainingAt(now);
        _timerTime.Text = left.TotalHours >= 1 ? left.ToString(@"h\:mm\:ss", CultureInfo.CurrentCulture) : left.ToString(@"m\:ss", CultureInfo.CurrentCulture);
        _timerTime.Foreground = new SolidColorBrush(IslandPalette.Orange);
        _timerLabel.Text = Strings.Get(timer.Mode switch
        {
            TimerMode.Focus => "Island_HomeFocusing",
            TimerMode.Break => "Island_HomeBreak",
            _ => "Island_HomeTimer",
        }).ToUpper(CultureInfo.CurrentCulture);
        _timerLabel.Foreground = new SolidColorBrush(IslandPalette.Orange);
        bool running = timer.State == TimerState.Running;
        _timerGlyph.Glyph = running ? "\xE769" : "\xE768";
        AutomationProperties.SetName(_timerToggle, Strings.Get(running ? "Island_HomePause" : "Island_HomePlay"));
    }

    private void ShowLatest(IslandScene scene, DateTimeOffset now)
    {
        if (scene.Inbox is [var toast, ..])
        {
            _latestGlyph.Glyph = "\xE8BD";
            _latestGlyph.Foreground = new SolidColorBrush(IslandPalette.Blue);
            string more = scene.Inbox.Count > 1 ? Strings.Format("Island_HomeMoreFormat", scene.Inbox.Count - 1) : string.Empty;
            _latestText.Text = toast.Body.Length > 0 ? $"{toast.Title} · {toast.Body.ReplaceLineEndings(" ")}{more}" : toast.Title + more;
            _latestWhen.Text = ListPanel.Age(toast.Arrived, now);
            return;
        }

        if (scene.Rewind is [var moment, ..])
        {
            CompactContent content = IslandPresenter.Describe(moment.Activity, now);
            _latestGlyph.Glyph = content.Glyph;
            _latestGlyph.Foreground = new SolidColorBrush(content.Accent);
            _latestText.Text = content.Subtitle is { } detail ? $"{content.Title} · {detail}" : content.Title;
            _latestWhen.Text = ListPanel.Age(moment.At, now);
            return;
        }

        _latestGlyph.Glyph = "\xE73E";
        _latestGlyph.Foreground = new SolidColorBrush(IslandPalette.Green);
        _latestText.Text = Strings.Get("Island_HomeAllQuiet");
        _latestWhen.Text = string.Empty;
    }

    private void Sample() => ShowLoad(_stats.Sample());

    private void ShowLoad(SystemReading reading)
    {
        double[] values = [reading.Cpu, reading.Memory];
        for (int i = 0; i < values.Length; i++)
        {
            (Rectangle bar, TextBlock value) = _loads[i];
            double percent = Math.Clamp(values[i], 0, 100);
            bar.Width = Math.Max(4, percent / 100 * 96);
            bar.Fill = new SolidColorBrush(percent >= 90 ? IslandPalette.Red : percent >= 70 ? IslandPalette.Orange : IslandPalette.Cyan);
            value.Text = string.Create(CultureInfo.CurrentCulture, $"{percent:0}%");
        }

        _network.Text = $"↓ {Speed(reading.Download)}   ↑ {Speed(reading.Upload)}";
    }

    private static string Speed(long bytesPerSecond) => bytesPerSecond switch
    {
        >= 1_000_000 => string.Create(CultureInfo.CurrentCulture, $"{bytesPerSecond / 1_000_000.0:0.0} MB/s"),
        >= 1_000 => string.Create(CultureInfo.CurrentCulture, $"{bytesPerSecond / 1_000.0:0} KB/s"),
        _ => string.Create(CultureInfo.CurrentCulture, $"{bytesPerSecond} B/s"),
    };

    /// <summary>A rounded card that opens <paramref name="page"/> when clicked (null: not clickable).</summary>
    private Border Card(UIElement child, string? page, string nameKey)
    {
        var card = new Border
        {
            CornerRadius = new CornerRadius(16),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12),
            Child = child,
        };
        AutomationProperties.SetName(card, Strings.Get(nameKey));
        if (page is not null)
        {
            card.Tapped += (_, e) =>
            {
                if (e.OriginalSource is not DependencyObject source || !InsideButton(source, card))
                {
                    PageRequested?.Invoke(this, page);
                }
            };
            card.PointerEntered += (_, _) => card.BorderBrush = new SolidColorBrush(Color.FromArgb(0x4A, 0xFF, 0xFF, 0xFF));
            card.PointerExited += (_, _) => card.BorderBrush = new SolidColorBrush(IslandTheme.Current.CardEdge);
        }

        _cards.Add(card);
        return card;
    }

    private static bool InsideButton(DependencyObject source, DependencyObject card)
    {
        for (DependencyObject? node = source; node is not null && node != card; node = VisualTreeHelper.GetParent(node))
        {
            if (node is Button)
            {
                return true;
            }
        }

        return false;
    }

    private void Place(FrameworkElement element, int row, int column, int span = 1)
    {
        Grid.SetRow(element, row);
        Grid.SetColumn(element, column);
        Grid.SetColumnSpan(element, span);
        _grid.Children.Add(element);
    }

    private static TextBlock Text(double size, Windows.UI.Text.FontWeight weight, bool secondary = false, string? text = null) => new()
    {
        Text = text ?? string.Empty,
        FontSize = size,
        FontWeight = weight,
        MaxLines = 1,
        TextTrimming = TextTrimming.CharacterEllipsis,
        Foreground = new SolidColorBrush(secondary ? IslandPalette.TextSecondary : IslandPalette.TextPrimary),
    };

    private static Color Lighten(Color color) => Color.FromArgb(0xFF, (byte)((color.R + 255) / 2), (byte)((color.G + 255) / 2), (byte)((color.B + 255) / 2));
}
