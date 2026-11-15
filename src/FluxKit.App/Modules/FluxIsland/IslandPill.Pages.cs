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

        // Leave room under the content for the page dots.
        double dots = pages.Count > 1 ? PageDotsRoom : 0;
        var element = (FrameworkElement)view;
        element.Margin = new Thickness(0, 0, 0, dots);
        Size size = view.PreferredSize;
        return (element, $"panel-{page}-{view.State}", new Size(size.Width, size.Height + dots));
    }

    private IIslandPage PageView(PanelPage page) => page switch
    {
        PanelPage.Media => MediaView,
        PanelPage.Shelf => ShelfView,
        PanelPage.Glance => GlanceView,
        PanelPage.Notes => NotesView,
        _ => TimerView,
    };

    /// <summary>
    /// Every page that is turned on, so each can always be reached from the rail. Music and the shelf show an empty
    /// state when there is nothing in them.
    /// </summary>
    private List<PanelPage> AvailablePages()
    {
        List<PanelPage> pages = [];
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

        if (_scene.Notes.Count > 0)
        {
            pages.Add(PanelPage.Notes);
        }

        return pages;
    }

    /// <summary>Opens on what the pill was showing: a running timer, the shelf, the music, else the clock and timers.</summary>
    private PanelPage DefaultPage(List<PanelPage> pages) => _scene.Primary?.Kind switch
    {
        _ when _dragging && pages.Contains(PanelPage.Shelf) => PanelPage.Shelf,
        ActivityKind.Timer or ActivityKind.TimerDone or ActivityKind.Stopwatch => PanelPage.Timer,
        ActivityKind.Shelf when pages.Contains(PanelPage.Shelf) => PanelPage.Shelf,
        _ => _scene.Media is not null && pages.Contains(PanelPage.Media) ? PanelPage.Media : PanelPage.Timer,
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

    /// <summary>The page dots: the current page is a short bar, the others small dots.</summary>
    private void ShowRail(List<PanelPage> pages, PanelPage selected)
    {
        PanelRail.Visibility = pages.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        (Button Tab, Border Dot, PanelPage Page)[] tabs = [(MediaTab, MediaDot, PanelPage.Media), (TimerTab, TimerDot, PanelPage.Timer), (ShelfTab, ShelfDot, PanelPage.Shelf), (GlanceTab, GlanceDot, PanelPage.Glance), (NotesTab, NotesDot, PanelPage.Notes)];
        foreach ((Button tab, Border dot, PanelPage tabPage) in tabs)
        {
            bool isSelected = tabPage == selected;
            tab.Visibility = pages.Contains(tabPage) ? Visibility.Visible : Visibility.Collapsed;
            dot.Width = isSelected ? 16 : 6;
            dot.Background = new SolidColorBrush(isSelected ? IslandPalette.TextPrimary : IslandPalette.WithAlpha(IslandPalette.TextPrimary, 0x55));
        }
    }

    /// <summary>Moves to the next or previous page (a sideways swipe, PageUp or PageDown), wrapping round.</summary>
    private void StepPage(int direction)
    {
        List<PanelPage> pages = AvailablePages();
        if (_mode != PillMode.Panel || pages.Count < 2 || _page is not { } page)
        {
            return;
        }

        int index = (pages.IndexOf(page) + direction + pages.Count) % pages.Count;
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
