using System.Globalization;
using FluxKit.Tools.FluxIsland.Activities;
using Windows.UI;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>How the command bar previews a command: an icon, what will happen, a detail line, and (for colours) a swatch.</summary>
internal sealed record CommandPreview(string Glyph, string Title, string? Detail, Color Accent, Color? Swatch = null);

/// <summary>Turns a parsed <see cref="IslandCommand"/> into the preview line shown under the command bar.</summary>
internal static class CommandPresenter
{
    public static CommandPreview Describe(IslandCommand command) => command.Kind switch
    {
        CommandKind.StartTimer => new("\xE916", Strings.Format("Island_Cmd_TimerFormat", IslandPresenter.FormatRemaining(command.Duration)), Strings.Get("Island_Cmd_TimerDetail"), IslandPalette.Orange),
        CommandKind.StartFocus => new("\xE916", Strings.Get("Island_Cmd_Focus"), null, IslandPalette.Orange),
        CommandKind.StartStopwatch => new("\xE121", Strings.Get("Island_Cmd_Stopwatch"), null, IslandPalette.Orange),
        CommandKind.Lap => new("\xE7C1", Strings.Get("Island_Lap"), null, IslandPalette.Orange),
        CommandKind.StopTimer => new("\xE711", Strings.Get("Island_Cmd_Stop"), null, IslandPalette.Orange),
        CommandKind.Calculate => new("\xE8EF", "= " + Calculator.Format(command.Number), Strings.Format("Island_Cmd_CalcDetailFormat", command.Text), IslandPalette.Cyan),
        CommandKind.ShowColor => new("\xE790", Hex(command.Color), Strings.Format("Island_Cmd_ColorDetailFormat", (command.Color >> 16) & 0xFF, (command.Color >> 8) & 0xFF, command.Color & 0xFF), IslandPalette.TextPrimary, ToColor(command.Color)),
        CommandKind.AddNote => new("\xE70B", command.Text ?? string.Empty, Strings.Get("Island_Cmd_NoteDetail"), IslandPalette.Yellow),
        CommandKind.PlayPause => new("\xE768", Strings.Get("Island_Cmd_PlayPause"), null, IslandPalette.Pink),
        CommandKind.NextTrack => new("\xE893", Strings.Get("Island_Cmd_Next"), null, IslandPalette.Pink),
        CommandKind.PreviousTrack => new("\xE892", Strings.Get("Island_Cmd_Previous"), null, IslandPalette.Pink),
        CommandKind.ToggleMic => new("\xE720", Strings.Get("Island_Cmd_Mic"), null, IslandPalette.Red),
        CommandKind.Lock => new("\xE72E", Strings.Get("Island_Cmd_Lock"), null, IslandPalette.Violet),
        CommandKind.Screenshot => new("\xE722", Strings.Get("Island_Cmd_Screenshot"), null, IslandPalette.Violet),
        CommandKind.ScreenOff => new("\xE708", Strings.Get("Island_Cmd_ScreenOff"), null, IslandPalette.Violet),
        CommandKind.ClearShelf => new("\xE74D", Strings.Get("Island_Cmd_ClearShelf"), null, IslandPalette.Violet),
        CommandKind.OpenPage => new("\xE8A7", Strings.Format("Island_Cmd_OpenFormat", command.Text), null, IslandPalette.Blue),
        _ => new(string.Empty, string.Empty, null, IslandPalette.TextPrimary),
    };

    /// <summary>"#3A7BFF".</summary>
    public static string Hex(int rgb) => "#" + rgb.ToString("X6", CultureInfo.InvariantCulture);

    public static Color ToColor(int rgb) => Color.FromArgb(0xFF, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
}
