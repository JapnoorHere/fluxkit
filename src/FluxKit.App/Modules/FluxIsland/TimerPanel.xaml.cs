using FluxKit.Tools.FluxIsland.Abstractions;
using FluxKit.Tools.FluxIsland.Activities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>The timer page. It shows what <see cref="Update"/> gives it and sends button presses to <see cref="Commands"/>.</summary>
public sealed partial class TimerPanel : UserControl
{
    private const string PauseIcon = "\xE769";
    private const string PlayIcon = "\xE768";

    private double _progress;
    private bool _wasActive;

    public TimerPanel()
    {
        InitializeComponent();
        var secondary = new SolidColorBrush(IslandPalette.TextSecondary);
        Date.Foreground = secondary;
        BatteryText.Foreground = secondary;
        Label.Foreground = new SolidColorBrush(IslandPalette.Orange);
        ProgressFill.Background = new SolidColorBrush(IslandPalette.Orange);
        Digits.TextBrush = new SolidColorBrush(IslandPalette.TextPrimary);

        OneMinute.Click += (_, _) => Commands?.StartTimer(TimeSpan.FromMinutes(1));
        FiveMinutes.Click += (_, _) => Commands?.StartTimer(TimeSpan.FromMinutes(5));
        TenMinutes.Click += (_, _) => Commands?.StartTimer(TimeSpan.FromMinutes(10));
        FocusButton.Click += (_, _) => Commands?.StartFocus();
        PauseButton.Click += (_, _) => Commands?.PauseOrResumeTimer();
        CancelButton.Click += (_, _) => Commands?.CancelTimer();
        foreach (Button button in new[] { OneMinute, FiveMinutes, TenMinutes, FocusButton, PauseButton, CancelButton })
        {
            PressFeedback.Attach(button);
        }
    }

    public IIslandCommands? Commands { get; set; }

    /// <summary>The rows, in the order they should appear when the page opens.</summary>
    internal UIElement[] Rows => _wasActive ? [ActiveHeader, ActiveRow] : [IdleHeader, QuickRow];

    /// <summary>Moves the content right to make room for the page rail.</summary>
    internal void SetInset(double left) => ContentGrid.Margin = new Thickness(left, 0, 0, 0);

    public void Update(IslandScene scene, DateTimeOffset now, bool springs)
    {
        FocusTimerSnapshot timer = scene.Timer ?? FocusTimerSnapshot.Idle;
        bool active = timer.IsActive;
        IdleHeader.Visibility = active ? Visibility.Collapsed : Visibility.Visible;
        QuickRow.Visibility = active ? Visibility.Collapsed : Visibility.Visible;
        ActiveHeader.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        ActiveRow.Visibility = active ? Visibility.Visible : Visibility.Collapsed;

        if (active)
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
            Digits.SetText(IslandPresenter.FormatRemaining(timer.RemainingAt(now)), springs && _wasActive);
            _progress = timer.ProgressAt(now);
            ApplyProgress();
            bool paused = timer.State == TimerState.Paused;
            PauseGlyph.Glyph = paused ? PlayIcon : PauseIcon;
            PauseText.Text = Strings.Get(paused ? "Island_ResumeTimer" : "Island_PauseTimer");
        }
        else
        {
            Clock.Text = IslandPresenter.FormatClock(now);
            Date.Text = IslandPresenter.FormatDate(now);
            FocusText.Text = Strings.Format("Island_FocusFormat", scene.Settings.FocusMinutes);
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
