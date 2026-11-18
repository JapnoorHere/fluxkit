using FluxKit.App.Modules.FluxIsland.Design;
using FluxKit.Tools.FluxIsland.Abstractions;
using FluxKit.Tools.FluxIsland.Activities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>The timers page: quick timers and a focus cycle, a running timer on a dial, or the stopwatch with laps.</summary>
public sealed partial class TimerPanel : UserControl, IIslandPage
{
    private const string PauseIcon = "\xF8AE";
    private const string PlayIcon = "\xF5B0";
    private const string AddIcon = "\xE710";
    private const string LapIcon = "\xE7C1";
    private const string StopwatchIcon = "\xE121";
    private const string TimerIcon = "\xE916";
    private const int MaxLapsShown = 3;
    private static readonly TimeSpan OneMinuteMore = TimeSpan.FromMinutes(1);
    private static readonly Size PageSize = new(420, 176);

    private bool _wasActive;
    private Showing _showing;
    private bool _preferStopwatch;

    private enum Showing
    {
        Idle,
        Timer,
        Stopwatch,
    }

    public TimerPanel()
    {
        InitializeComponent();
        var secondary = new SolidColorBrush(IslandPalette.TextSecondary);
        Date.Foreground = secondary;
        BatteryText.Foreground = secondary;
        QuickLabel.Foreground = secondary;
        LapsText.Foreground = secondary;
        Label.Foreground = new SolidColorBrush(IslandPalette.Orange);
        Dial.SetColor(IslandPalette.Orange);
        Digits.TextBrush = new SolidColorBrush(IslandPalette.Orange);
        PauseButton.Background = new SolidColorBrush(IslandPalette.Tint(IslandPalette.Orange));
        PauseButton.Foreground = new SolidColorBrush(IslandPalette.Orange);
        CancelButton.Background = new SolidColorBrush(IslandPalette.RaisedHover);
        Tile(OneMinute, "1", Strings.Get("Island_UnitMin"));
        Tile(FiveMinutes, "5", Strings.Get("Island_UnitMin"));
        Tile(TenMinutes, "10", Strings.Get("Island_UnitMin"));
        Tile(FocusButton, "25", Strings.Get("Island_UnitFocus"));
        Tile(StopwatchButton, null, Strings.Get("Island_UnitStopwatch"), StopwatchIcon);

        OneMinute.Click += (_, _) => Commands?.StartTimer(TimeSpan.FromMinutes(1));
        FiveMinutes.Click += (_, _) => Commands?.StartTimer(TimeSpan.FromMinutes(5));
        TenMinutes.Click += (_, _) => Commands?.StartTimer(TimeSpan.FromMinutes(10));
        FocusButton.Click += (_, _) => Commands?.StartFocus();
        StopwatchButton.Click += (_, _) => Commands?.StartStopwatch();
        SwitchButton.Click += (_, _) =>
        {
            _preferStopwatch = !_preferStopwatch;
            SwitchChanged?.Invoke(this, EventArgs.Empty);
        };
        PauseButton.Click += (_, _) =>
        {
            if (_showing == Showing.Stopwatch)
            {
                Commands?.PauseOrResumeStopwatch();
            }
            else
            {
                Commands?.PauseOrResumeTimer();
            }
        };
        ExtraButton.Click += (_, _) =>
        {
            if (_showing == Showing.Stopwatch)
            {
                Commands?.LapStopwatch();
            }
            else
            {
                Commands?.AddTimerTime(OneMinuteMore);
            }
        };
        CancelButton.Click += (_, _) =>
        {
            if (_showing == Showing.Stopwatch)
            {
                Commands?.ResetStopwatch();
            }
            else
            {
                Commands?.CancelTimer();
            }
        };
        foreach (Button button in new[] { OneMinute, FiveMinutes, TenMinutes, FocusButton, StopwatchButton, SwitchButton, PauseButton, ExtraButton, CancelButton })
        {
            PressFeedback.Attach(button);
        }
    }

    public IIslandCommands? Commands { get; set; }

    /// <summary>The switch button flipped between the timer and the stopwatch; the pill redraws the page.</summary>
    public event EventHandler? SwitchChanged;

    /// <summary>The rows, in the order they should appear when the page opens.</summary>
    UIElement[] IIslandPage.Rows => _wasActive ? [ActiveHeader, ActiveRow] : [IdleHeader, QuickRow];

    Size IIslandPage.PreferredSize => PageSize;

    string IIslandPage.State => _showing.ToString();

    string IIslandPage.Update(IslandScene scene, DateTimeOffset now, bool springs)
    {
        Update(scene, now, springs);
        return _showing switch
        {
            Showing.Timer => IslandPresenter.FormatRemaining(scene.Timer!.RemainingAt(now)),
            Showing.Stopwatch => IslandPresenter.FormatPosition(scene.Stopwatch!.ElapsedAt(now)),
            _ => IslandPresenter.FormatClock(now),
        };
    }

    public void Update(IslandScene scene, DateTimeOffset now, bool springs)
    {
        FocusTimerSnapshot timer = scene.Timer ?? FocusTimerSnapshot.Idle;
        StopwatchSnapshot stopwatch = scene.Stopwatch ?? StopwatchSnapshot.Idle;

        // Whichever runs takes the page. When both run, the switch button flips between them (the timer first).
        bool both = timer.IsActive && stopwatch.IsActive;
        Showing showing = both ? (_preferStopwatch ? Showing.Stopwatch : Showing.Timer)
            : timer.IsActive ? Showing.Timer
            : stopwatch.IsActive ? Showing.Stopwatch
            : Showing.Idle;
        if (!both)
        {
            _preferStopwatch = false;
        }

        SwitchButton.Visibility = both ? Visibility.Visible : Visibility.Collapsed;
        if (both)
        {
            string switchName = Strings.Get(showing == Showing.Timer ? "Island_ShowStopwatch" : "Island_ShowTimer");
            SwitchGlyph.Glyph = showing == Showing.Timer ? StopwatchIcon : TimerIcon;
            ToolTipService.SetToolTip(SwitchButton, switchName);
            AutomationProperties.SetName(SwitchButton, switchName);
        }

        LapsText.Visibility = showing == Showing.Stopwatch && stopwatch.Laps.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        bool active = showing != Showing.Idle;
        IdleHeader.Visibility = active ? Visibility.Collapsed : Visibility.Visible;
        QuickRow.Visibility = active ? Visibility.Collapsed : Visibility.Visible;
        ActiveHeader.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        ActiveRow.Visibility = active ? Visibility.Visible : Visibility.Collapsed;

        if (showing == Showing.Timer)
        {
            Label.Text = timer.Mode switch
            {
                TimerMode.Focus => Strings.Format("Island_FocusRoundFormat", timer.Round),
                TimerMode.Break => Strings.Get("Island_Timer_Break"),
                _ => Strings.Get("Island_Timer_Countdown"),
            };
            if (timer.State == TimerState.Paused)
            {
                Label.Text = Strings.Format("Island_PausedLabelFormat", Label.Text);
            }

            // The first show after starting swaps straight in; after that, only the digits that change roll.
            Digits.SetText(IslandPresenter.FormatRemaining(timer.RemainingAt(now)), springs && _showing == showing);
            Dial.SetGlyph(TimerIcon);
            Dial.SetProgress(1 - timer.ProgressAt(now), springs);
            SetButtons(timer.State == TimerState.Paused, AddIcon, Strings.Get("Island_AddMinute"), Strings.Get("Island_CancelTimer_Label"));
            ExtraButton.IsEnabled = true;
        }
        else if (showing == Showing.Stopwatch)
        {
            Label.Text = stopwatch.Laps.Count > 0
                ? Strings.Format("Island_LapFormat", stopwatch.Laps.Count + 1)
                : Strings.Get("Island_Stopwatch");

            ShowLaps(stopwatch.Laps);
            if (stopwatch.State == TimerState.Paused)
            {
                Label.Text = Strings.Format("Island_PausedLabelFormat", Label.Text);
            }

            Digits.SetText(IslandPresenter.FormatPosition(stopwatch.ElapsedAt(now)), springs && _showing == showing);
            Dial.SetGlyph(StopwatchIcon);
            Dial.SetProgress(stopwatch.ElapsedAt(now).TotalSeconds % 60 / 60, springs);
            SetButtons(stopwatch.State == TimerState.Paused, LapIcon, Strings.Get("Island_Lap"), Strings.Get("Island_ResetStopwatch"));
            ExtraButton.IsEnabled = stopwatch.State == TimerState.Running;
        }
        else
        {
            Clock.Text = IslandPresenter.FormatClock(now);
            Date.Text = IslandPresenter.FormatDate(now);
            FocusText.Text = Strings.Format("Island_FocusFormat", scene.Settings.FocusMinutes);
            Tile(FocusButton, scene.Settings.FocusMinutes.ToString(System.Globalization.CultureInfo.CurrentCulture), Strings.Get("Island_UnitFocus"));
            AutomationProperties.SetName(FocusButton, FocusText.Text);
            ToolTipService.SetToolTip(FocusButton, Strings.Format("Island_FocusTipFormat", scene.Settings.FocusMinutes, scene.Settings.BreakMinutes));
            PowerSnapshot power = scene.Power;
            Battery.Visibility = power.HasBattery ? Visibility.Visible : Visibility.Collapsed;
            if (power.HasBattery)
            {
                CompactContent battery = IslandPresenter.Describe(new IslandActivity(power.IsPluggedIn ? ActivityKind.PluggedIn : ActivityKind.Unplugged) { Value = power.Percent }, now);
                BatteryGlyph.Glyph = battery.Glyph;
                BatteryGlyph.Foreground = new SolidColorBrush(battery.Accent);
                BatteryText.Text = battery.Trail;
            }
        }

        _wasActive = active;
        _showing = showing;
    }

    private void SetButtons(bool paused, string extraGlyph, string extraText, string cancelText)
    {
        PauseGlyph.Glyph = paused ? PlayIcon : PauseIcon;
        PauseText.Text = Strings.Get(paused ? "Island_ResumeTimer" : "Island_PauseTimer");
        ExtraGlyph.Glyph = extraGlyph;
        ExtraText.Text = extraText;
        CancelText.Text = cancelText;

        // The round buttons show only an icon; their names come from the hidden labels.
        AutomationProperties.SetName(PauseButton, PauseText.Text);
        ToolTipService.SetToolTip(PauseButton, PauseText.Text);
        AutomationProperties.SetName(CancelButton, cancelText);
        ToolTipService.SetToolTip(CancelButton, cancelText);
    }

    /// <summary>A bento tile: a big number over a small label (or an icon over a label).</summary>
    private static void Tile(Button tile, string? number, string unit, string? glyph = null)
    {
        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Spacing = 1 };
        if (number is not null)
        {
            var big = new TextBlock { Text = number, FontSize = 22, FontWeight = Microsoft.UI.Text.FontWeights.Bold, FontFamily = new FontFamily("Segoe UI Variable Display"), HorizontalAlignment = HorizontalAlignment.Center };
            Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(big, FontNumeralAlignment.Tabular);
            stack.Children.Add(big);
        }
        else if (glyph is not null)
        {
            stack.Children.Add(new FontIcon { Glyph = glyph, FontSize = 20, Margin = new Thickness(0, 2, 0, 3), Foreground = new SolidColorBrush(IslandPalette.Orange) });
        }

        stack.Children.Add(new TextBlock { Text = unit, FontSize = 9, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, CharacterSpacing = 60, Foreground = new SolidColorBrush(IslandPalette.TextSecondary), HorizontalAlignment = HorizontalAlignment.Center });
        tile.Content = stack;
    }

    /// <summary>
    /// The newest laps, numbered from the first ("Lap 3 0:41   Lap 2 0:38"), with the fastest lap so far in green and
    /// the slowest in orange once there are enough to compare.
    /// </summary>
    private void ShowLaps(IReadOnlyList<TimeSpan> laps)
    {
        LapsText.Inlines.Clear();
        TimeSpan best = laps.Count > 1 ? laps.Min() : TimeSpan.MinValue;
        TimeSpan worst = laps.Count > 2 ? laps.Max() : TimeSpan.MaxValue;
        for (int i = 0; i < Math.Min(laps.Count, MaxLapsShown); i++)
        {
            if (i > 0)
            {
                LapsText.Inlines.Add(new Run { Text = "   " });
            }

            var run = new Run { Text = Strings.Format("Island_LapEntryFormat", laps.Count - i, IslandPresenter.FormatPosition(laps[i])) };
            if (laps[i] == best)
            {
                run.Foreground = new SolidColorBrush(IslandPalette.Green);
            }
            else if (laps[i] == worst)
            {
                run.Foreground = new SolidColorBrush(IslandPalette.Orange);
            }

            LapsText.Inlines.Add(run);
        }
    }
}
