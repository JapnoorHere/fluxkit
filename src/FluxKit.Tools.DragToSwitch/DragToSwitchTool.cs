using FluxKit.Core.Settings;
using FluxKit.Core.Tools;
using FluxKit.Platform.Input;
using FluxKit.Platform.VirtualDesktop;
using FluxKit.Tools.DragToSwitch.Interaction;
using Serilog;

namespace FluxKit.Tools.DragToSwitch;

/// <summary>
/// The Drag-to-Switch tool: owns the input hook, the interaction coordinator and its pump loop.
/// The selector view is supplied by the host (the App) so this assembly stays free of XAML.
/// </summary>
public sealed class DragToSwitchTool : IFluxTool
{
    public const string Id = "drag-to-switch";

    private readonly ISettingsService _settings;
    private readonly IVirtualDesktopService _desktops;
    private readonly Func<ISelectorView> _viewFactory;
    private readonly Func<ISystemInputSource> _inputFactory;
    private readonly IWindowProbe _probe;
    private readonly TimeProvider _time;
    private readonly ILogger _log;
    private ISystemInputSource? _input;
    private DragInteractionCoordinator? _coordinator;
    private CancellationTokenSource? _loopCts;
    private Task? _loop;

    public DragToSwitchTool(ISettingsService settings, IVirtualDesktopService desktops, Func<ISelectorView> viewFactory, Func<ISystemInputSource> inputFactory, IWindowProbe probe, TimeProvider time, ILogger log)
    {
        _settings = settings;
        _desktops = desktops;
        _viewFactory = viewFactory;
        _inputFactory = inputFactory;
        _probe = probe;
        _time = time;
        _log = log.ForContext<DragToSwitchTool>();
        _settings.SettingsChanged += OnSettingsChanged;
    }

    public ToolDescriptor Descriptor { get; } = new(
        Id,
        "Drag-to-Switch",
        "Move files and windows to another virtual desktop while you drag them.",
        "",
        10,
        "DragToSwitch.png");

    public ToolState State { get; private set; } = ToolState.Disabled;

    public SessionSummary? LastSession => _coordinator?.LastSession;

    public event EventHandler<ToolState>? StateChanged;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_coordinator is not null)
        {
            return Task.CompletedTask;
        }

        SetState(ToolState.Starting);
        try
        {
            if (_desktops.Backend == VirtualDesktopBackend.Unavailable)
            {
                SetState(new ToolState(ToolStatus.Degraded, _desktops.BackendDescription));
                return Task.CompletedTask;
            }

            _coordinator = new DragInteractionCoordinator(_desktops, _viewFactory(), _probe, _settings.Read<DragToSwitchSettings>(DragToSwitchSettings.Section), _time, _log);
            _input = _inputFactory();
            DragInteractionCoordinator coordinator = _coordinator;
            _input.KeyFilter = coordinator.ShouldSwallowKey;
            _input.Install();
            _loopCts = new CancellationTokenSource();
            _loop = Task.Run(() => PumpAsync(coordinator, _input, _loopCts.Token), CancellationToken.None);
            SetState(_desktops.Backend == VirtualDesktopBackend.Keyboard
                ? new ToolState(ToolStatus.Degraded, _desktops.BackendDescription)
                : ToolState.Running);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Drag-to-Switch failed to start");
            SetState(new ToolState(ToolStatus.Faulted, "Drag-to-Switch couldn't start. Open the log folder for details."));
            _ = StopAsync(CancellationToken.None);
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _coordinator?.Abort("tool stopped");
        if (_loopCts is not null)
        {
            await _loopCts.CancelAsync().ConfigureAwait(false);
        }

        if (_loop is not null)
        {
            try
            {
                await _loop.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
            {
                // Pump loop is cancelled cooperatively; nothing else to do.
            }
        }

        if (_input is not null)
        {
            _input.KeyFilter = null;
            _input.Dispose();
        }

        _coordinator?.Dispose();
        _loopCts?.Dispose();
        _input = null;
        _coordinator = null;
        _loopCts = null;
        _loop = null;
        SetState(ToolState.Disabled);
    }

    public async ValueTask DisposeAsync()
    {
        _settings.SettingsChanged -= OnSettingsChanged;
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        (_probe as IDisposable)?.Dispose();
    }

    /// <summary>Reads input events and drives ticks at ~60 Hz only while a session needs them.</summary>
    private async Task PumpAsync(DragInteractionCoordinator coordinator, ISystemInputSource input, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (coordinator.NeedsTicks)
                {
                    using var tick = new CancellationTokenSource(TimeSpan.FromMilliseconds(16), _time);
                    using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, tick.Token);
                    try
                    {
                        while (await input.Events.WaitToReadAsync(linked.Token).ConfigureAwait(false))
                        {
                            while (input.Events.TryRead(out InputEvent e))
                            {
                                coordinator.Process(e);
                            }
                        }
                    }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                    {
                        coordinator.Tick();
                    }
                }
                else
                {
                    InputEvent e = await input.Events.ReadAsync(ct).ConfigureAwait(false);
                    coordinator.Process(e);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Drag-to-Switch input loop failed");
            SetState(new ToolState(ToolStatus.Faulted, "Drag-to-Switch stopped unexpectedly. Turn it off and on again to restart it."));
        }
    }

    private void OnSettingsChanged(object? sender, string section)
    {
        if (section is DragToSwitchSettings.Section or "*")
        {
            _coordinator?.UpdateSettings(_settings.Read<DragToSwitchSettings>(DragToSwitchSettings.Section));
        }
    }

    private void SetState(ToolState state)
    {
        State = state;
        StateChanged?.Invoke(this, state);
    }
}
