using FluxKit.Core.Settings;

namespace FluxKit.Core.Tests;

/// <summary>The keys are stored in users' settings files, so they must never change.</summary>
public sealed class SettingsKeysTests
{
    [Fact]
    public void Global_section_key_is_stable() => Assert.Equal("global", SettingsKeys.Global);

    [Fact]
    public void Tool_section_keys_keep_the_utilities_prefix() =>
        Assert.Equal("utilities/drag-to-switch", SettingsKeys.Tool("drag-to-switch"));
}
