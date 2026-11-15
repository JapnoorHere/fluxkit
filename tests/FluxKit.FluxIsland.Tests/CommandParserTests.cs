using FluxKit.Tools.FluxIsland.Activities;

namespace FluxKit.FluxIsland.Tests;

public sealed class CommandParserTests
{
    [Theory]
    [InlineData("5m", 300)]
    [InlineData("5 min", 300)]
    [InlineData("90s", 90)]
    [InlineData("25", 1500)]
    [InlineData("1h20", 4800)]
    [InlineData("1h 20m", 4800)]
    [InlineData("1h", 3600)]
    [InlineData("2:30", 150)]
    [InlineData("timer 10", 600)]
    [InlineData("t 45s", 45)]
    [InlineData("  5M  ", 300)]
    public void Timers_are_read_in_many_shapes(string text, int seconds)
    {
        IslandCommand? command = CommandParser.Parse(text);

        Assert.Equal(CommandKind.StartTimer, command?.Kind);
        Assert.Equal(TimeSpan.FromSeconds(seconds), command?.Duration);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("0m")]
    [InlineData("25h")]
    [InlineData("1:75")]
    public void Zero_too_long_or_impossible_timers_are_refused(string text) =>
        Assert.NotEqual(CommandKind.StartTimer, CommandParser.Parse(text)?.Kind);

    [Theory]
    [InlineData("= 12*8+3", 99)]
    [InlineData("12*8", 96)]
    [InlineData("=2^10", 1024)]
    [InlineData("(1+2)*3", 9)]
    [InlineData("10 ÷ 4", 2.5)]
    [InlineData("1,200 / 4", 300)]
    public void Sums_are_calculated(string text, double expected)
    {
        IslandCommand? command = CommandParser.Parse(text);

        Assert.Equal(CommandKind.Calculate, command?.Kind);
        Assert.Equal(expected, command!.Number, 6);
    }

    [Theory]
    [InlineData("#3a7bff", 0x3A7BFF)]
    [InlineData("#38f", 0x3388FF)]
    [InlineData("3a7bff", 0x3A7BFF)]
    [InlineData("rgb(58, 123, 255)", 0x3A7BFF)]
    public void Colours_are_read(string text, int rgb)
    {
        IslandCommand? command = CommandParser.Parse(text);

        Assert.Equal(CommandKind.ShowColor, command?.Kind);
        Assert.Equal(rgb, command?.Color);
    }

    [Theory]
    [InlineData("facade")]
    [InlineData("rgb(300, 0, 0)")]
    public void Words_and_bad_colours_are_not_colours(string text) =>
        Assert.NotEqual(CommandKind.ShowColor, CommandParser.Parse(text)?.Kind);

    [Fact]
    public void A_bare_number_is_a_timer_not_a_colour() =>
        Assert.Equal(CommandKind.StartTimer, CommandParser.Parse("255")?.Kind);

    [Fact]
    public void Notes_keep_their_text_and_are_cut_when_too_long()
    {
        Assert.Equal("buy milk", CommandParser.Parse("note buy milk")?.Text);
        Assert.Equal("call mum", CommandParser.Parse("n call mum")?.Text);
        Assert.Equal(CommandParser.MaxNoteLength, CommandParser.Parse("note " + new string('a', 500))?.Text?.Length);
        Assert.Null(CommandParser.Parse("note   "));
    }

    [Theory]
    [InlineData("stopwatch", CommandKind.StartStopwatch)]
    [InlineData("SW", CommandKind.StartStopwatch)]
    [InlineData("focus", CommandKind.StartFocus)]
    [InlineData("lap", CommandKind.Lap)]
    [InlineData("cancel", CommandKind.StopTimer)]
    [InlineData("pause", CommandKind.PlayPause)]
    [InlineData("next", CommandKind.NextTrack)]
    [InlineData("mute", CommandKind.ToggleMic)]
    [InlineData("lock", CommandKind.Lock)]
    [InlineData("screenshot", CommandKind.Screenshot)]
    [InlineData("screen  off", CommandKind.ScreenOff)]
    [InlineData("clear shelf", CommandKind.ClearShelf)]
    public void Verbs_are_recognised(string text, CommandKind kind) =>
        Assert.Equal(kind, CommandParser.Parse(text)?.Kind);

    [Fact]
    public void Page_names_open_that_page()
    {
        IslandCommand? command = CommandParser.Parse("Glance");

        Assert.Equal(CommandKind.OpenPage, command?.Kind);
        Assert.Equal("glance", command?.Text);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("hello there")]
    [InlineData(null)]
    public void Anything_else_is_not_understood(string? text) => Assert.Null(CommandParser.Parse(text));
}
