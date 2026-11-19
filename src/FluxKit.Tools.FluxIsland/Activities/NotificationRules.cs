using FluxKit.Tools.FluxIsland.Abstractions;

namespace FluxKit.Tools.FluxIsland.Activities;

/// <summary>How much of a notification the island shows.</summary>
public enum NotificationPrivacy
{
    /// <summary>The sender and the message, like a phone.</summary>
    Full,

    /// <summary>Only who it is from ("Teams · Priya"); open the island to read it.</summary>
    SenderOnly,

    /// <summary>Only which app ("WhatsApp · New message").</summary>
    Hidden,
}

/// <summary>What the user chose for one app's notifications.</summary>
public sealed class NotificationAppSettings
{
    /// <summary>The app's name as Windows reports it, for the settings list.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Show this app's notifications on the island.</summary>
    public bool Show { get; set; } = true;

    /// <summary>Turn off Windows' own banner for this app while the island shows its notifications.</summary>
    public bool HideBanner { get; set; } = true;

    /// <summary>Lets this app through when "What can interrupt" is set to important only.</summary>
    public bool Priority { get; set; }
}

/// <summary>Everything a decision about one notification depends on, apart from the notification itself.</summary>
public readonly record struct NotificationContext(DateTimeOffset Now, NotificationPrivacy Privacy, bool Presenting, bool HideWhilePresenting, NotificationAppSettings App);

/// <summary>
/// Decides how a notification shows (pure logic, no Windows calls): whether its app is on and not muted, how much text
/// may show (privacy, and hiding while presenting), and grouping a burst from one app into a single moment
/// ("Teams · 3 new").
/// </summary>
public sealed class NotificationRules
{
    /// <summary>Notifications from the same app this close together are shown as one growing moment.</summary>
    public static readonly TimeSpan BurstWindow = TimeSpan.FromSeconds(4);

    private readonly Dictionary<string, (DateTimeOffset At, int Count)> _bursts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _muted = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Keeps an app quiet on the island until <paramref name="until"/> (its notifications still reach the inbox).</summary>
    public void Mute(string appId, DateTimeOffset until) => _muted[appId] = until;

    public bool IsMuted(string appId, DateTimeOffset now) => _muted.TryGetValue(appId, out DateTimeOffset until) && now < until;

    /// <summary>The privacy that applies right now: hidden while presenting, if the user asked for that.</summary>
    public static NotificationPrivacy Effective(in NotificationContext context) =>
        context.Presenting && context.HideWhilePresenting ? NotificationPrivacy.Hidden : context.Privacy;

    /// <summary>
    /// The moment to show for <paramref name="toast"/>, or null when it shouldn't take over the island (its app is off
    /// or muted, or it is empty).
    /// </summary>
    public IslandActivity? ToMoment(ToastInfo toast, in NotificationContext context)
    {
        if (!context.App.Show || IsMuted(toast.AppId, context.Now) || (toast.Title.Length == 0 && toast.Body.Length == 0))
        {
            return null;
        }

        int count = 1;
        if (_bursts.TryGetValue(toast.AppId, out (DateTimeOffset At, int Count) last) && context.Now - last.At <= BurstWindow)
        {
            count = last.Count + 1;
        }

        _bursts[toast.AppId] = (context.Now, count);
        (string text, string? detail) = Redact(toast.AppName, toast.Title, toast.Body, Effective(context));
        return new IslandActivity(ActivityKind.Notification)
        {
            Text = text,
            Detail = detail,
            Source = toast.AppName,
            Path = toast.AppId,
            Image = toast.Logo,
            Value = count,
            Total = unchecked((int)toast.Id),
            Flag = context.App.Priority,
        };
    }

    /// <summary>
    /// What may be shown of a notification: the sender and message, the sender only, or just the app's name. An
    /// empty detail means "say only that something arrived".
    /// </summary>
    public static (string Text, string? Detail) Redact(string appName, string title, string body, NotificationPrivacy privacy) => privacy switch
    {
        NotificationPrivacy.Hidden => (appName, null),
        NotificationPrivacy.SenderOnly => (title.Length > 0 ? title : appName, null),
        _ => title.Length > 0 ? (title, body.Length > 0 ? body : null) : (appName, body),
    };
}
