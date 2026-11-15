using System.Text.Json.Serialization;
using FluxKit.Tools.FluxIsland.Activities;

namespace FluxKit.Tools.FluxIsland.Sources;

/// <summary>Source-generated JSON for the island's small files, so saving and loading never use reflection.</summary>
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(List<IslandNote>))]
internal sealed partial class IslandJsonContext : JsonSerializerContext;
