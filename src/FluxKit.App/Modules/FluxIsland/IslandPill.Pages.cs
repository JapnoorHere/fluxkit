using System.Numerics;
using FluxKit.App.Modules.FluxIsland.Design;
using FluxKit.Tools.FluxIsland.Activities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>The open panel's pages: which ones exist, which opens first, the page dots, and moving between pages.</summary>
public sealed partial class IslandPill
{
    private (UIElement View, string Key, Size Size) BuildPanel(DateTimeOffset now)
    {
        if (_dragging)
        {
            // Files are being dragged over the island: offer what can be done with them.
            PanelRail.Visibility = Visibility.Collapsed;
            AutomationProperties.SetName(Surface, Strings.Get("Island_DropName"));
            return (DropView, "drop", DropZones.PreferredSize);
        }

        List<PanelPage> pages = AvailablePages();
        if (_page is not { } page || !pages.Contains(page))
        {
            page = DefaultPage(pages);
            _page = page;
        }

        ShowRail(pages, page);
        IIslandPage view = PageView(page);
        if (page == PanelPage.Shelf)
        {
            ShelfView.SetDropping(_dragging);
        }

        AutomationProperties.SetName(Surface, view.Update(_scene, now, Springs));

        // Leave room above the content for the icon bar.
        double bar = pages.Count > 1 ? IconBarRoom : 0;
        var element = (FrameworkElement)view;
        element.Margin = new Thickness(0, bar, 0, 0);
        Size size = view.PreferredSize;
        return (element, $"panel-{page}-{view.State}", new Size(Math.Max(size.Width, IconBarMinWidth), size.Height + bar));
    }

    private IIslandPage PageView(PanelPage page) => page switch
    {
        PanelPage.Home => HomeView,
        PanelPage.Media => MediaView,
        PanelPage.Shelf => ShelfView,
        PanelPage.Glance => GlanceView,
        PanelPage.Notes => NotesView,
        PanelPage.Rewind => RewindView,
        PanelPage.Clips => ClipsView,
        PanelPage.Inbox => InboxView,
        PanelPage.Peek => PeekView,
        PanelPage.Stash => StashView,
        _ => TimerView,
    };

    /// <summary>
    /// Every page that is turned on, so each can always be reached from the rail. Music and the shelf show an empty
    /// state when there is nothing in them.
    /// </summary>
    private List<PanelPage> AvailablePages()
    {
        // Home, the dashboard, always comes first.
        List<PanelPage> pages = [PanelPage.Home];
        if (_scene.Settings.ShowMedia || _scene.Media is not null)
        {
            pages.Add(PanelPage.Media);
        }

        pages.Add(PanelPage.Timer);
        if (_scene.Settings.ShelfEnabled)
        {
            pages.Add(PanelPage.Shelf);
        }

        if (_scene.Settings.ShowGlance)
        {
            pages.Add(PanelPage.Glance);
        }

        // A list stays reachable while it is the page on show, so Undo can bring back what Clear took away.
        if (_scene.Notes.Count > 0 || _page == PanelPage.Notes)
        {
            pages.Add(PanelPage.Notes);
        }

        if (_scene.Rewind.Count > 0 || _page == PanelPage.Rewind)
        {
            pages.Add(PanelPage.Rewind);
        }

        if (_scene.Settings.ClipboardHistory)
        {
            pages.Add(PanelPage.Clips);
        }

        if (_scene.Settings.ShowNotifications || _scene.Inbox.Count > 0)
        {
            pages.Add(PanelPage.Inbox);
        }

        if (_scene.Settings.WindowPeek)
        {
            pages.Add(PanelPage.Peek);
        }

        if (_scene.Stash.Count > 0 || _page == PanelPage.Stash)
        {
            pages.Add(PanelPage.Stash);
        }

        return pages;
    }

    /// <summary>Opens on what the pill was showing: a running timer, the shelf, the music, else the clock and timers.</summary>
    private PanelPage DefaultPage(List<PanelPage> pages) => _scene.Primary?.Kind switch
    {
        _ when _dragging && pages.Contains(PanelPage.Shelf) => PanelPage.Shelf,
        ActivityKind.Timer or ActivityKind.TimerDone or ActivityKind.Stopwatch => PanelPage.Timer,
        ActivityKind.Shelf when pages.Contains(PanelPage.Shelf) => PanelPage.Shelf,
        ActivityKind.Notification when pages.Contains(PanelPage.Inbox) => PanelPage.Inbox,
        ActivityKind.Media when pages.Contains(PanelPage.Media) => PanelPage.Media,
        _ => PanelPage.Home,
    };

    private void ShowPage(PanelPage page)
    {
        if (page != _page)
        {
            List<PanelPage> pages = AvailablePages();
            _pageSlide = _page is { } from ? Math.Sign(pages.IndexOf(page) - pages.IndexOf(from)) : 0;
            _page = page;
            Update();
        }
    }

    /// <summary>A Home tile was clicked: open its full page (the newest notification opens the inbox, else Recent).</summary>
    private void OpenFromHome(string name)
    {
        List<PanelPage> pages = AvailablePages();
        PanelPage? target = name switch
        {
            "media" => PanelPage.Media,
            "timer" => PanelPage.Timer,
            "glance" => PanelPage.Glance,
            "latest" when _scene.Inbox.Count > 0 => PanelPage.Inbox,
            "latest" => PanelPage.Rewind,
            _ => null,
        };
        if (target is { } page && pages.Contains(page))
        {
            ShowPage(page);
        }
    }

    /// <summary>The icon for each page in the icon bar.</summary>
    private static string PageGlyph(PanelPage page) => page switch
    {
        PanelPage.Home => "\xE80F",
        PanelPage.Media => "\xE8D6",
        PanelPage.Timer => "\xE916",
        PanelPage.Shelf => "\xE8B7",
        PanelPage.Glance => "\xE9D9",
        PanelPage.Notes => "\xE70B",
        PanelPage.Rewind => "\xE81C",
        PanelPage.Clips => "\xE77F",
        PanelPage.Inbox => "\xE8BD",
        PanelPage.Peek => "\xE7B3",
        _ => "\xE7B8",
    };

    /// <summary>
    /// The icon bar across the top of the open panel (like a phone's tab bar, at the top): one icon per page, the
    /// current one white on a soft pill, the others dimmed. Click one, or swipe sideways.
    /// </summary>
    private void ShowRail(List<PanelPage> pages, PanelPage selected)
    {
        PanelRail.Visibility = pages.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        (Button Tab, Border Dot, PanelPage Page)[] tabs = [(HomeTab, HomeDot, PanelPage.Home), (MediaTab, MediaDot, PanelPage.Media), (TimerTab, TimerDot, PanelPage.Timer), (ShelfTab, ShelfDot, PanelPage.Shelf), (GlanceTab, GlanceDot, PanelPage.Glance), (NotesTab, NotesDot, PanelPage.Notes), (RewindTab, RewindDot, PanelPage.Rewind), (ClipsTab, ClipsDot, PanelPage.Clips), (InboxTab, InboxDot, PanelPage.Inbox), (PeekTab, PeekDot, PanelPage.Peek), (StashTab, StashDot, PanelPage.Stash)];
        foreach ((Button tab, Border dot, PanelPage tabPage) in tabs)
        {
            bool isSelected = tabPage == selected;
            tab.Visibility = pages.Contains(tabPage) ? Visibility.Visible : Visibility.Collapsed;
            dot.Width = 34;
            dot.Height = 26;
            dot.CornerRadius = new CornerRadius(13);
            dot.Child ??= new FontIcon { Glyph = PageGlyph(tabPage), FontSize = 13 };
            ((FontIcon)dot.Child).Foreground = new SolidColorBrush(isSelected ? IslandPalette.TextPrimary : IslandPalette.TextTertiary);
            dot.Background = new SolidColorBrush(isSelected ? IslandTheme.Current.Chip : Microsoft.UI.Colors.Transparent);
            ToolTipService.SetToolTip(tab, AutomationProperties.GetName(tab));
        }
    }

    /// <summary>Moves to the next or previous page (a sideways swipe, PageUp or PageDown); at the ends it rubber-bands.</summary>
    private void StepPage(int direction)
    {
        List<PanelPage> pages = AvailablePages();
        if (_mode != PillMode.Panel || pages.Count < 2 || _page is not { } page)
        {
            return;
        }

        // Like a phone's pages: no wrapping round; at either end the page leans and springs back.
        int index = pages.IndexOf(page) + direction;
        if (index < 0 || index >= pages.Count)
        {
            if (_shownView is not null)
            {
                IslandMotion.Nudge(_shownView, Springs, direction);
            }

            return;
        }

        ShowPage(pages[index]);
    }

    /// <summary>Plays the entrance when the content changes; returns true when it did.</summary>
    private bool ShowView(UIElement view, string key)
    {
        if (key == _shownKey)
        {
            return false;
        }

        UIElement? previous = _shownView;
        _shownView = view;
        _shownKey = key;

        // Moving between pages slides sideways, in the direction of travel, like swiping on a phone.
        Vector3? slide = null;
        if (previous is IIslandPage && view is IIslandPage && previous != view && _pageSlide != 0)
        {
            slide = new Vector3(PageSlide * _pageSlide, 0, 0);
        }

        _pageSlide = 0;
        if (previous is not null && previous != view)
        {
            IslandMotion.Exit(previous, () => _shownView != previous, to: slide is { } away ? -away : null);
        }

        UIElement[]? rows = (view as IIslandPage)?.Rows;
        if (rows is not null)
        {
            // Panel rows arrive one after the other.
            IslandMotion.Enter(view, TimeSpan.Zero, Springs, slide);
            for (int i = 0; i < rows.Length; i++)
            {
                IslandMotion.Enter(rows[i], IslandMotion.Stagger * (i + 1), Springs);
            }
        }
        else
        {
            IslandMotion.Enter(view, previous is null ? TimeSpan.Zero : IslandMotion.Stagger, Springs);
        }

        return true;
    }
}
