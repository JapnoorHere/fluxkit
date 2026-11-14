using FluxKit.Tools.FluxIsland.Abstractions;
using FluxKit.Tools.FluxIsland.Activities;

namespace FluxKit.FluxIsland.Tests.Fakes;

/// <summary>Records every scene the tool renders.</summary>
internal sealed class FakeIslandView : IIslandView
{
    public List<IslandScene> Scenes { get; } = [];

    public IslandScene Last => Scenes[^1];

    public bool Closed { get; private set; }

    public void Render(IslandScene scene) => Scenes.Add(scene);

    public int PanelToggles { get; private set; }

    public void TogglePanel() => PanelToggles++;

    public void Close() => Closed = true;
}
