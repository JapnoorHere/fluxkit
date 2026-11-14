using FluxKit.Tools.FluxIsland.Activities;
using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace FluxKit.App.Modules.FluxIsland.Design;

/// <summary>
/// One page of the open island (music, timers, shelf, glance…). The pill treats every page the same way: it asks the
/// page to show the scene, sizes itself to <see cref="PreferredSize"/> and plays the entrance over <see cref="Rows"/>.
/// </summary>
internal interface IIslandPage
{
    /// <summary>The page's rows, top to bottom, so they can arrive one after the other.</summary>
    UIElement[] Rows { get; }

    /// <summary>The size the panel should be for what the page shows now, not counting the page dots.</summary>
    Size PreferredSize { get; }

    /// <summary>
    /// A short name for the page's current state (for example "empty" or "running"). When it changes, the rows play
    /// their entrance again.
    /// </summary>
    string State { get; }

    /// <summary>Shows <paramref name="scene"/> and returns what a screen reader should announce for the page.</summary>
    string Update(IslandScene scene, DateTimeOffset now, bool springs);
}
