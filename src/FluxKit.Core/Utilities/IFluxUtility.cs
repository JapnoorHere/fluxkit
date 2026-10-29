namespace FluxKit.Core.Utilities;

/// <summary>A self-contained FluxKit utility (module) hosted by the shell.</summary>
public interface IFluxUtility : IAsyncDisposable
{
    UtilityDescriptor Descriptor { get; }

    UtilityState State { get; }

    event EventHandler<UtilityState>? StateChanged;

    /// <summary>Starts background work. Must be idempotent and must not throw for expected failures; report them via <see cref="State"/>.</summary>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>Stops background work and releases native resources. Must be idempotent.</summary>
    Task StopAsync(CancellationToken cancellationToken);
}

/// <summary>Static metadata the shell uses to build navigation, cards and the tray menu.</summary>
/// <param name="Id">Stable kebab-case id; also the settings section key.</param>
/// <param name="Glyph">Segoe Fluent Icons code point, used where no <paramref name="IconAsset"/> is set.</param>
/// <param name="IconAsset">Optional full-colour icon file name under the app's Assets folder, e.g. "DragToSwitch.png".</param>
public sealed record UtilityDescriptor(string Id, string DisplayName, string Description, string Glyph, int Order, string? IconAsset = null);

public enum UtilityStatus
{
    Disabled,
    Starting,
    Running,
    Degraded,
    Faulted,
}

/// <summary>Current runtime state; <see cref="Detail"/> explains Degraded/Faulted in user-facing words.</summary>
public sealed record UtilityState(UtilityStatus Status, string? Detail = null)
{
    public static UtilityState Disabled { get; } = new(UtilityStatus.Disabled);

    public static UtilityState Starting { get; } = new(UtilityStatus.Starting);

    public static UtilityState Running { get; } = new(UtilityStatus.Running);

    public bool IsActive => Status is UtilityStatus.Running or UtilityStatus.Degraded;
}
