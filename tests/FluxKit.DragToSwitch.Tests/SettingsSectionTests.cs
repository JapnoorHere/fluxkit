using FluxKit.Utilities.DragToSwitch;

namespace FluxKit.DragToSwitch.Tests;

public sealed class SettingsSectionTests
{
    [Fact]
    public void Drag_to_switch_settings_keep_their_saved_section_key() =>
        Assert.Equal("utilities/drag-to-switch", DragToSwitchSettings.Section);
}
