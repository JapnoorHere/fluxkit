using FluxKit.Tools.FluxIsland;
using FluxKit.Tools.FluxIsland.Abstractions;
using FluxKit.Tools.FluxIsland.Activities;
using FluxKit.Tools.FluxIsland.Sources;

namespace FluxKit.FluxIsland.Tests;

public sealed class ShelfAndSettingsTests
{
    [Fact]
    public void Shelf_puts_new_files_first_and_ignores_duplicates()
    {
        var shelf = new ShelfModel();

        shelf.Add([@"C:\a.txt", @"C:\b.txt"]);
        bool changed = shelf.Add([@"c:\B.TXT"]);

        Assert.True(changed);
        Assert.Equal([@"c:\B.TXT", @"C:\a.txt"], shelf.Items);
        Assert.False(shelf.Add([@"C:\b.txt"]));
    }

    [Theory]
    [InlineData(50, 1, 52)]
    [InlineData(50, -3, 44)]
    [InlineData(51, 1, 52)]
    [InlineData(51, -1, 50)]
    [InlineData(99, 5, 100)]
    [InlineData(1, -1, 0)]
    public void Volume_steps_by_two_and_lands_on_even_levels(int current, int notches, int expected)
    {
        int target = VolumeSource.StepTarget(current, notches);

        Assert.Equal(expected, target);
    }

    [Fact]
    public void Shelf_keeps_at_most_twelve_files()
    {
        var shelf = new ShelfModel();

        shelf.Add(Enumerable.Range(1, 20).Select(i => $@"C:\f{i}.txt"));

        Assert.Equal(ShelfModel.MaxItems, shelf.Count);
        Assert.Equal(@"C:\f1.txt", shelf.Items[0]);
    }

    [Fact]
    public void Shelf_remove_and_clear()
    {
        var shelf = new ShelfModel();
        shelf.Add([@"C:\a.txt", @"C:\b.txt"]);

        Assert.True(shelf.Remove(@"C:\A.txt"));
        Assert.Equal([@"C:\b.txt"], shelf.Items);
        Assert.True(shelf.Clear());
        Assert.False(shelf.Clear());
    }

    [Fact]
    public void Settings_section_key_is_stable()
    {
        Assert.Equal("utilities/flux-island", FluxIslandSettings.Section);
    }

    [Fact]
    public void Settings_clamp_timer_lengths_and_fix_unknown_effects()
    {
        var settings = new FluxIslandSettings { FocusMinutes = 500, BreakMinutes = 0, Effects = (IslandEffects)42 };

        List<string> fixes = [.. settings.Normalize()];

        Assert.Equal(3, fixes.Count);
        Assert.Equal(FluxIslandSettings.MaxFocusMinutes, settings.FocusMinutes);
        Assert.Equal(FluxIslandSettings.MinBreakMinutes, settings.BreakMinutes);
        Assert.Equal(IslandEffects.Full, settings.Effects);
    }

    [Fact]
    public void Default_settings_need_no_fixes()
    {
        Assert.Empty(new FluxIslandSettings().Normalize());
    }

    [Fact]
    public void Media_position_moves_on_while_playing_and_stops_at_the_end()
    {
        DateTimeOffset at = DateTimeOffset.UnixEpoch;
        var media = new MediaInfo("Song", "Artist", true, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(60), at, true, true, true);

        Assert.Equal(TimeSpan.FromSeconds(15), media.EstimatedPosition(at.AddSeconds(5)));
        Assert.Equal(TimeSpan.FromSeconds(60), media.EstimatedPosition(at.AddMinutes(5)));
        Assert.Equal(TimeSpan.FromSeconds(10), (media with { IsPlaying = false }).EstimatedPosition(at.AddSeconds(5)));
    }
}
