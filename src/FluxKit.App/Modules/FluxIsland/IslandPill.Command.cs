using FluxKit.Platform.Shell;
using FluxKit.Tools.FluxIsland.Activities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>The Island Command bar: the shortcut opens it, and what is typed runs here.</summary>
public sealed partial class IslandPill
{
    private static readonly Size CommandSize = new(420, 128);

    private (UIElement View, string Key, Size Size) BuildCommand()
    {
        _shape.SetWash(IslandPalette.TextPrimary, on: false, Springs);
        _shape.SetProgress(null, IslandPalette.TextPrimary, Springs);
        CommandView.Animate = Springs;
        PanelRail.Visibility = Visibility.Collapsed;
        AutomationProperties.SetName(Surface, Strings.Get("Island_CommandName"));
        return (CommandView, "command", CommandSize);
    }

    private void WireCommandBar()
    {
        CommandView.Submitted += (_, command) => RunCommand(command);
        CommandView.Dismissed += (_, _) => SetMode(PillMode.Compact);
        CommandView.PanelRequested += (_, _) => OpenPanelFromKeyboard();
    }

    /// <summary>
    /// Does what the command asks. Sums and colours are copied and the bar stays open for the next one; pages open
    /// the panel; everything else runs and the island closes.
    /// </summary>
    private void RunCommand(IslandCommand command)
    {
        if (Commands is not { } commands)
        {
            return; // A preview: nothing runs.
        }

        switch (command.Kind)
        {
            case CommandKind.Calculate:
                Copy(Calculator.Format(command.Number));
                CommandView.Confirm(Strings.Format("Island_Cmd_CopiedFormat", Calculator.Format(command.Number)));
                return;
            case CommandKind.ShowColor:
                Copy(CommandPresenter.Hex(command.Color));
                CommandView.Confirm(Strings.Format("Island_Cmd_CopiedFormat", CommandPresenter.Hex(command.Color)));
                return;
            case CommandKind.OpenPage:
                _page = command.Text switch
                {
                    "music" => PanelPage.Media,
                    "shelf" => PanelPage.Shelf,
                    "glance" => PanelPage.Glance,
                    "notes" => PanelPage.Notes,
                    _ => PanelPage.Timer,
                };
                OpenPanelFromKeyboard();
                return;
        }

        // Close first, so lock and screen-off don't leave the bar open behind them.
        SetMode(PillMode.Compact);
        switch (command.Kind)
        {
            case CommandKind.StartTimer:
                commands.StartTimer(command.Duration);
                break;
            case CommandKind.StartFocus:
                commands.StartFocus();
                break;
            case CommandKind.StartStopwatch:
                commands.StartStopwatch();
                break;
            case CommandKind.Lap:
                commands.LapStopwatch();
                break;
            case CommandKind.StopTimer:
                commands.CancelTimer();
                break;
            case CommandKind.AddNote:
                commands.AddNote(command.Text ?? string.Empty);
                break;
            case CommandKind.PlayPause:
                commands.PlayPause();
                break;
            case CommandKind.NextTrack:
                commands.NextTrack();
                break;
            case CommandKind.PreviousTrack:
                commands.PreviousTrack();
                break;
            case CommandKind.ToggleMic:
                commands.ToggleMicMute();
                break;
            case CommandKind.ClearShelf:
                commands.ClearShelf();
                break;
            case CommandKind.Lock:
                SessionActions.Lock();
                break;
            case CommandKind.Screenshot:
                SessionActions.StartScreenshot();
                break;
            case CommandKind.ScreenOff:
                SessionActions.SleepDisplays();
                break;
        }
    }

    private static void Copy(string text)
    {
        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
    }
}
