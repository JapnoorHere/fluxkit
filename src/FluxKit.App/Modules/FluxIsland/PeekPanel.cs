using FluxKit.App.Modules.FluxIsland.Design;
using FluxKit.Platform.Windows;
using FluxKit.Tools.FluxIsland.Activities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>
/// The Peek page (plan C): pick any open window and watch it live inside the island, without switching to it. The live
/// view itself is a DWM thumbnail that the island window places over <see cref="ThumbArea"/>; this page only lays out
/// the frame, the window's name and its buttons. Window titles are shown on screen only.
/// </summary>
public sealed partial class PeekPanel : UserControl, IIslandPage
{
    private const double PageWidth = 440;
    private const double ThumbHeight = 220;
    private const int MaxChoices = 6;
    private static readonly TimeSpan ListRefresh = TimeSpan.FromSeconds(2);

    private readonly StackPanel _root = new() { Padding = new Thickness(16, 14, 16, 8), Spacing = 8 };
    private readonly Grid _header = new() { ColumnSpacing = 10 };
    private readonly IconBadge _badge = new() { BadgeSize = 28 };
    private readonly TextBlock _title = new() { FontSize = 15, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 1 };
    private readonly Button _switch = new();
    private readonly Button _change = new();
    private readonly StackPanel _choices = new() { Spacing = 2 };
    private readonly TextBlock _empty = new() { TextWrapping = TextWrapping.Wrap, FontSize = 13 };
    private IReadOnlyList<PeekableWindow> _windows = [];
    private long _listedAt;

    public PeekPanel()
    {
        _badge.Show("\xE7B3", IslandPalette.Cyan);
        _header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _header.Children.Add(_badge);
        Grid.SetColumn(_title, 1);
        _header.Children.Add(_title);
        Grid.SetColumn(_switch, 2);
        _header.Children.Add(_switch);
        Grid.SetColumn(_change, 3);
        _header.Children.Add(_change);
        _title.Foreground = new SolidColorBrush(IslandPalette.TextPrimary);
        _empty.Foreground = new SolidColorBrush(IslandPalette.TextSecondary);
        _switch.Content = Strings.Get("Island_PeekSwitch");
        _change.Content = Strings.Get("Island_PeekChange");
        _switch.Click += (_, _) =>
        {
            if (WindowThumbnail.Exists(Pinned))
            {
                WindowThumbnail.Activate(Pinned);
            }
        };
        _change.Click += (_, _) => Pin(0);

        ThumbArea = new Border
        {
            Height = ThumbHeight,
            CornerRadius = new CornerRadius(14),
            Background = new SolidColorBrush(IslandPalette.WithAlpha(IslandPalette.TextPrimary, 0x0C)),
        };
        _root.Children.Add(_header);
        _root.Children.Add(ThumbArea);
        _root.Children.Add(_choices);
        _root.Children.Add(_empty);
        Content = _root;
        Loaded += (_, _) =>
        {
            _switch.Style = (Style)Resources["IslandChipButtonStyle"];
            _change.Style = (Style)Resources["IslandChipButtonStyle"];
        };
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("ms-appx:///Modules/FluxIsland/IslandStyles.xaml") });
    }

    /// <summary>The window being watched, or 0 while picking one.</summary>
    public nint Pinned { get; private set; }

    /// <summary>Where the live view goes; the island window draws the thumbnail over it.</summary>
    public FrameworkElement ThumbArea { get; }

    /// <summary>The watched window changed (0: none). The island window re-targets its thumbnail.</summary>
    public event EventHandler? PinChanged;

    UIElement[] IIslandPage.Rows => Watching ? [_header, ThumbArea] : [_header, _choices];

    Size IIslandPage.PreferredSize => Watching
        ? new Size(PageWidth, 58 + ThumbHeight + 8)
        : new Size(PageWidth, 58 + (Math.Clamp(_windows.Count, 1, MaxChoices) * 38) + 8);

    string IIslandPage.State => Watching ? "watch" : "pick";

    private bool Watching => Pinned != 0;

    string IIslandPage.Update(IslandScene scene, DateTimeOffset now, bool springs)
    {
        if (Watching && !WindowThumbnail.Exists(Pinned))
        {
            Pin(0); // The window closed.
        }

        ThumbArea.Visibility = Watching ? Visibility.Visible : Visibility.Collapsed;
        _switch.Visibility = _change.Visibility = ThumbArea.Visibility;
        _choices.Visibility = Watching ? Visibility.Collapsed : Visibility.Visible;
        if (Watching)
        {
            _empty.Visibility = Visibility.Collapsed;
            string title = _windows.FirstOrDefault(w => w.Handle == Pinned)?.Title ?? string.Empty;
            _title.Text = title.Length > 0 ? title : Strings.Get("Island_PeekTitle");
            return Strings.Format("Island_PeekNameFormat", _title.Text);
        }

        _title.Text = Strings.Get("Island_PeekPick");
        if (Environment.TickCount64 - _listedAt > ListRefresh.TotalMilliseconds)
        {
            _listedAt = Environment.TickCount64;
            _windows = WindowThumbnail.ListWindows();
            BuildChoices();
        }

        _empty.Text = Strings.Get("Island_PeekEmpty");
        _empty.Visibility = _windows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        return _title.Text;
    }

    /// <summary>One row per window, newest first: click to start watching it.</summary>
    private void BuildChoices()
    {
        _choices.Children.Clear();
        foreach (PeekableWindow window in _windows.Take(MaxChoices))
        {
            var row = new Button
            {
                Content = new TextBlock { Text = window.Title, FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = 1 },
                Height = 36,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 0, 10, 0),
            };
            AutomationProperties.SetName(row, Strings.Format("Island_PeekAtFormat", window.Title));
            row.Click += (_, _) => Pin(window.Handle);
            PressFeedback.Attach(row);
            _choices.Children.Add(row);
        }
    }

    /// <summary>For the <c>peek-live</c> preview: watches the first window in the list.</summary>
    internal void PinFirst()
    {
        if (WindowThumbnail.ListWindows() is [var first, ..])
        {
            Pin(first.Handle);
        }
    }

    private void Pin(nint window)
    {
        Pinned = window;
        _listedAt = 0;
        PinChanged?.Invoke(this, EventArgs.Empty);
    }
}
