using FluxKit.Tools.FluxIsland;
using FluxKit.Tools.FluxIsland.Activities;

namespace FluxKit.FluxIsland.Tests;

public sealed class HotkeyGestureTests
{
    [Theory]
    [InlineData("Ctrl+Alt+I", HotkeyModifiers.Control | HotkeyModifiers.Alt, 'I')]
    [InlineData("ctrl + shift + 5", HotkeyModifiers.Control | HotkeyModifiers.Shift, '5')]
    [InlineData("Win+Space", HotkeyModifiers.Windows, 0x20)]
    [InlineData("F9", HotkeyModifiers.None, 0x78)]
    [InlineData("Alt+F24", HotkeyModifiers.Alt, 0x87)]
    public void Reads_valid_shortcuts(string text, HotkeyModifiers modifiers, int key)
    {
        Assert.True(HotkeyGesture.TryParse(text, out HotkeyGesture gesture));
        Assert.Equal(new HotkeyGesture(modifiers, key), gesture);
    }

    [Theory]
    [InlineData("")]
    [InlineData("I")]
    [InlineData("Shift+I")]
    [InlineData("Ctrl+Alt")]
    [InlineData("Ctrl+Ctrl+I")]
    [InlineData("Ctrl+I+J")]
    [InlineData("I+Ctrl")]
    [InlineData("Ctrl+Tab")]
    [InlineData("F25")]
    [InlineData("Ctrl+F0")]
    public void Refuses_shortcuts_that_would_get_in_the_way_or_that_it_does_not_know(string text) =>
        Assert.False(HotkeyGesture.TryParse(text, out _));

    [Fact]
    public void Writes_the_shortcut_back_in_the_standard_order()
    {
        Assert.True(HotkeyGesture.TryParse("shift+win+ctrl+alt+k", out HotkeyGesture gesture));
        Assert.Equal("Ctrl+Alt+Shift+Win+K", gesture.ToString());
        Assert.True(HotkeyGesture.TryParse("Ctrl+F12", out gesture));
        Assert.Equal("Ctrl+F12", gesture.ToString());
    }

    [Fact]
    public void A_broken_shortcut_in_settings_goes_back_to_the_default_and_empty_stays_off()
    {
        var broken = new FluxIslandSettings { Hotkey = "Ctrl+" };
        Assert.NotEmpty(broken.Normalize().ToList());
        Assert.Equal(HotkeyGesture.Default, broken.Hotkey);

        var off = new FluxIslandSettings { Hotkey = string.Empty };
        Assert.Empty(off.Normalize().ToList());
        Assert.Equal(string.Empty, off.Hotkey);
    }
}
