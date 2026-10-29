namespace FluxKit.Core.Tools;

/// <summary>A self-contained FluxKit tool (module) hosted by the shell.</summary>
public interface IFluxTool : IAsyncDisposable
{
    ToolDescriptor Descriptor { get; }

    ToolState State { get; }

    event EventHandler<ToolState>? StateChanged;

    /// <summary>Starts background work. Must be idempotent and must not throw for expected failures; report them via <see cref="State"/>.</summary>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>Stops background work and releases native resources. Must be idempotent.</summary>
    Task StopAsync(CancellationToken cancellationToken);
}

/// <summary>Static metadata the shell uses to build navigation, cards and the tray menu.</summary>
/// <param name="Id">Stable kebab-case id; also the settings section key.</param>
/// <param name="Glyph">Segoe Fluent Icons code point, used where no <paramref name="IconAsset"/> is set.</param>
/// <param name="IconAsset">Optional full-colour icon file name under the app's Assets folder, e.g. "DragToSwitch.png".</param>
public sealed record ToolDescriptor(string Id, string DisplayName, string Description, string Glyph, int Order, string? IconAsset = null);

public enum ToolStatus
{
    Disabled,
    Starting,
    Running,
    Degraded,
    Faulted,
}

/// <summary>Current runtime state; <see cref="Detail"/> explains Degraded/Faulted in user-facing words.</summary>
public sealed record ToolState(ToolStatus Status, string? Detail = null)
{
    public static ToolState Disabled { get; } = new(ToolStatus.Disabled);

    public static ToolState Starting { get; } = new(ToolStatus.Starting);

    public static ToolState Running { get; } = new(ToolStatus.Running);

    public bool IsActive => Status is ToolStatus.Running or ToolStatus.Degraded;
}
