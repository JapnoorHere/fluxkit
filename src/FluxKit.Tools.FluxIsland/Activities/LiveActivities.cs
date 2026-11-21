using System.Globalization;
using System.Text.Json;

namespace FluxKit.Tools.FluxIsland.Activities;

/// <summary>What a Live Activity update asks for.</summary>
public enum LiveState
{
    Running,
    Done,
    Failed,
    Clear,
}

/// <summary>One update from a script or app, already checked and cleaned (see <see cref="LiveProtocol"/>).</summary>
public sealed record LiveUpdate(string Id, LiveState State, string? Title, string? Detail, int? Progress, string? Glyph, TimeSpan? Ttl);

/// <summary>A Live Activity on the island.</summary>
public sealed record LiveActivity(string Id, string Title, string? Detail, int? Progress, string Glyph, DateTimeOffset Updated, DateTimeOffset Ends);

/// <summary>
/// The Live Activities protocol (plan B): one JSON object per line, for example
/// <c>{"id":"build","title":"Build","detail":"Compiling","progress":40,"state":"running","glyph":"build","ttl":600}</c>.
/// Anything malformed, too long or with unknown values is dropped; text is trimmed and stripped of control characters.
/// A Live Activity can only show text, a progress and one of a few named icons: it can never run anything.
/// </summary>
public static class LiveProtocol
{
    public const int MaxLineLength = 2048;

    public const int MaxTitle = 60;

    public const int MaxDetail = 120;

    public const int MaxId = 40;

    /// <summary>The icons a Live Activity may ask for; anything else shows the default spark.</summary>
    public static readonly IReadOnlySet<string> Glyphs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "spark", "build", "test", "deploy", "download", "upload", "ai", "sync", "terminal", "music" };

    public static bool TryParse(string? line, out LiveUpdate? update)
    {
        update = null;
        if (string.IsNullOrWhiteSpace(line) || line.Length > MaxLineLength)
        {
            return false;
        }

        try
        {
            using var json = JsonDocument.Parse(line);
            JsonElement root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object || Text(root, "id", MaxId) is not { } id)
            {
                return false;
            }

            LiveState? state = Text(root, "state", 16)?.ToUpperInvariant() switch
            {
                null or "RUNNING" or "SET" => LiveState.Running,
                "DONE" => LiveState.Done,
                "FAILED" or "FAIL" => LiveState.Failed,
                "CLEAR" => LiveState.Clear,
                _ => null,
            };
            if (state is null)
            {
                return false;
            }

            int? progress = root.TryGetProperty("progress", out JsonElement p) && p.ValueKind == JsonValueKind.Number && p.TryGetDouble(out double value)
                ? (int)Math.Clamp(Math.Round(value), 0, 100)
                : null;
            TimeSpan? ttl = root.TryGetProperty("ttl", out JsonElement t) && t.ValueKind == JsonValueKind.Number && t.TryGetDouble(out double seconds) && seconds > 0
                ? TimeSpan.FromSeconds(Math.Min(seconds, LiveActivityModel.MaxTtl.TotalSeconds))
                : null;
            string? glyph = Text(root, "glyph", 16) is { } named && Glyphs.Contains(named) ? named.ToLowerInvariant() : null;
            string? title = Text(root, "title", MaxTitle);
            string? detail = Text(root, "detail", MaxDetail);

            // A running update must say something: a title, a detail or a progress (later ones often send only progress).
            if (state == LiveState.Running && title is null && detail is null && progress is null)
            {
                return false;
            }

            update = new LiveUpdate(id, state.Value, title, detail, progress, glyph, ttl);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>A string property, without control characters, trimmed and cut to <paramref name="max"/>; null when empty.</summary>
    private static string? Text(JsonElement root, string name, int max)
    {
        if (!root.TryGetProperty(name, out JsonElement value))
        {
            return null;
        }

        string? raw = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null,
        };
        if (raw is null)
        {
            return null;
        }

        string clean = new string([.. raw.Where(c => !char.IsControl(c))]).Trim();
        if (clean.Length > max)
        {
            clean = clean[..(max - 1)].TrimEnd() + "…";
        }

        return clean.Length == 0 ? null : clean;
    }
}

/// <summary>
/// The Live Activities on the island: updated by id, at most <see cref="MaxItems"/>, each ending on its own after its
/// time to live (30 minutes by default, 8 hours at most, as on a phone). Pure, so it is tested without a pipe.
/// </summary>
public sealed class LiveActivityModel
{
    public const int MaxItems = 5;

    public static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(30);

    public static readonly TimeSpan MaxTtl = TimeSpan.FromHours(8);

    private readonly List<LiveActivity> _items = [];

    /// <summary>Running activities, most recently updated first.</summary>
    public IReadOnlyList<LiveActivity> Items => [.. _items.OrderByDescending(a => a.Updated)];

    public DateTimeOffset? NextEndAt => _items.Count == 0 ? null : _items.Min(a => a.Ends);

    /// <summary>
    /// Applies an update. Returns the activity that just finished (done or failed) so the island can celebrate or
    /// shake, or null.
    /// </summary>
    public LiveActivity? Apply(LiveUpdate update, DateTimeOffset now)
    {
        LiveActivity? existing = _items.FirstOrDefault(a => a.Id == update.Id);
        switch (update.State)
        {
            case LiveState.Clear:
                _items.RemoveAll(a => a.Id == update.Id);
                return null;
            case LiveState.Done or LiveState.Failed:
                _items.RemoveAll(a => a.Id == update.Id);
                string title = update.Title ?? existing?.Title ?? update.Id;
                return new LiveActivity(update.Id, title, update.Detail ?? existing?.Detail, 100, update.Glyph ?? existing?.Glyph ?? "spark", now, now);
        }

        var next = new LiveActivity(
            update.Id,
            update.Title ?? existing?.Title ?? update.Id,
            update.Detail ?? (update.Title is null ? existing?.Detail : null),
            update.Progress ?? (update.Title is null ? existing?.Progress : null),
            update.Glyph ?? existing?.Glyph ?? "spark",
            now,
            now + (update.Ttl ?? DefaultTtl));
        _items.RemoveAll(a => a.Id == update.Id);
        _items.Add(next);
        if (_items.Count > MaxItems)
        {
            _items.Remove(_items.MinBy(a => a.Updated)!);
        }

        return null;
    }

    /// <summary>Drops activities whose time is up (a script that died without saying "done").</summary>
    public bool Expire(DateTimeOffset now) => _items.RemoveAll(a => a.Ends <= now) > 0;

    /// <summary>"40 %" for a progress, or null.</summary>
    public static string? FormatProgress(int? progress) =>
        progress is { } p ? string.Create(CultureInfo.CurrentCulture, $"{p} %") : null;
}
