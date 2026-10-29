using Windows.UI;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>What the compact pill shows for one activity: an icon, a line or two of text, and a value on the right.</summary>
internal sealed record CompactContent(string Glyph, string Title, string? Subtitle, string? Trail, Color Accent);
