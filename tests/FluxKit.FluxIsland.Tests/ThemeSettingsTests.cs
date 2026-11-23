using FluxKit.Tools.FluxIsland;

namespace FluxKit.FluxIsland.Tests;

public sealed class ThemeSettingsTests
{
    [Theory]
    [InlineData(IslandLook.Rose)]
    [InlineData(IslandLook.Lavender)]
    [InlineData(IslandLook.Peach)]
    [InlineData(IslandLook.Sakura)]
    public void Retired_colour_tints_load_as_onyx(IslandLook retired)
    {
        var settings = new FluxIslandSettings { Look = retired };

        Assert.NotEmpty(settings.Normalize());
        Assert.Equal(IslandLook.Onyx, settings.Look);
    }

    [Theory]
    [InlineData(IslandLook.Onyx)]
    [InlineData(IslandLook.PureBlack)]
    [InlineData(IslandLook.Graphite)]
    [InlineData(IslandLook.Glass)]
    [InlineData(IslandLook.Mercury)]
    public void Materials_are_kept(IslandLook material)
    {
        var settings = new FluxIslandSettings { Look = material, UseWindowsAccent = true };

        _ = settings.Normalize().ToList();
        Assert.Equal(material, settings.Look);
        Assert.True(settings.UseWindowsAccent);
    }
}
