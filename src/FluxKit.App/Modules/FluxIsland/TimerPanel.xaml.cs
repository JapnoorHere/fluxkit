using FluxKit.Tools.FluxIsland.Abstractions;
using FluxKit.Tools.FluxIsland.Activities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>The timer page. It shows what <see cref="Update"/> gives it and sends button presses to <see cref="Commands"/>.</summary>
public sealed partial class TimerPanel : UserControl
{
    private const string PauseIcon = "\xE769";
    private const string PlayIcon = "\xE768";
    private const string AddIcon = "\xE710";
    private const string LapIcon = "\xE7C1";
    private const string StopwatchIcon = "\xE121";
    private const string TimerIcon = "\xE916";
    private const int MaxLapsShown = 3;
    private static readonly TimeSpan OneMinuteMore = TimeSpan.FromMinutes(1);

    private double _progress;
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
        ProgressFill.Background = new SolidColorBrush(IslandPalette.Orange);
        Digits.TextBrush = new SolidColorBrush(IslandPalette.TextPrimary);

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
    internal UIElement[] Rows => _wasActive ? [ActiveHeader, ActiveRow] : [IdleHeader, QuickRow];

    /// <summary>Moves the content right to make room for the page rail.</summary>
    internal void SetInset(double left) => ContentGrid.Margin = new Thickness(left, 0, 0, 0);

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
        ProgressTrack.Visibility = showing == Showing.Timer ? Visibility.Visible : Visibility.Collapsed;

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
            _progress = timer.ProgressAt(now);
            ApplyProgress();
            SetButtons(timer.State == TimerState.Paused, AddIcon, Strings.Get("Island_AddMinute"), Strings.Get("Island_CancelTimer_Label"));
            ExtraButton.IsEnabled = true;
        }
        else if (showing == Showing.Stopwatch)
        {
            Label.Text = stopwatch.Laps.Count > 0
                ? Strings.Format("Island_LapFormat", stopwatch.Laps.Count + 1)
                : Strings.Get("Island_Stopwatch");

            // The newest laps, numbered from the first: "Lap 3 0:41   Lap 2 0:38   Lap 1 0:44".
            LapsText.Text = string.Join("   ", stopwatch.Laps.Take(MaxLapsShown).Select((lap, i) =>
                Strings.Format("Island_LapEntryFormat", stopwatch.Laps.Count - i, IslandPresenter.FormatPosition(lap))));
            if (stopwatch.State == TimerState.Paused)
            {
                Label.Text = Strings.Format("Island_PausedLabelFormat", Label.Text);
            }

            Digits.SetText(IslandPresenter.FormatPosition(stopwatch.ElapsedAt(now)), springs && _showing == showing);
            SetButtons(stopwatch.State == TimerState.Paused, LapIcon, Strings.Get("Island_Lap"), Strings.Get("Island_ResetStopwatch"));
            ExtraButton.IsEnabled = stopwatch.State == TimerState.Running;
        }
        else
        {
            Clock.Text = IslandPresenter.FormatClock(now);
            Date.Text = IslandPresenter.FormatDate(now);
            FocusText.Text = Strings.Format("Island_FocusFormat", scene.Settings.FocusMinutes);
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
    }

    private void OnTrackSizeChanged(object sender, SizeChangedEventArgs e) => ApplyProgress();

    private void ApplyProgress()
    {
        if (ProgressFill.Parent is FrameworkElement track)
        {
            ProgressFill.Width = Math.Clamp(_progress, 0, 1) * track.ActualWidth;
        }
    }
}
