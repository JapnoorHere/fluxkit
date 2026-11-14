using Windows.UI;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>
/// What the compact pill shows for one activity, in Apple's leading/trailing layout: an icon on the leading edge
/// (in a ring when <see cref="Progress"/> is set), a line or two of text, and a value on the trailing edge.
/// </summary>
/// <param name="Progress">0–1 for moments with a level (a timer, a lap, the battery), drawn as a ring round the icon.</param>
internal sealed record CompactContent(string Glyph, string Title, string? Subtitle, string? Trail, Color Accent, double? Progress = null);
