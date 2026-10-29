namespace FluxKit.Utilities.DragToSwitch.Interaction;

/// <summary>Summary of the last session for the Troubleshooting panel; contains no file names.</summary>
public sealed record SessionSummary(DateTimeOffset StartedAt, string SourceProcess, int? FromDesktop, int? ToDesktop, int? SwitchMs, SessionEnd End, string? Detail);
