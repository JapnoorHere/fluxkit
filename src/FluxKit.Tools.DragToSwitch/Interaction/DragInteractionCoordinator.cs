using FluxKit.Platform.Input;
using FluxKit.Platform.VirtualDesktop;
using FluxKit.Platform.Windows;
using Serilog;

namespace FluxKit.Tools.DragToSwitch.Interaction;

/// <summary>
/// The Drag-to-Switch state machine (Docs/PLAN.md §7). All entry points are serialized by a lock and are
/// deterministic given the injected <see cref="TimeProvider"/>, so the whole lifecycle is unit-testable.
/// The coordinator observes input only; it never touches the OLE drag itself.
/// </summary>
public sealed class DragInteractionCoordinator : IDisposable
{
    public static readonly TimeSpan SuspectDebounce = TimeSpan.FromMilliseconds(120);
    public static readonly TimeSpan HoverGrace = TimeSpan.FromMilliseconds(120);
    public static readonly TimeSpan SwitchCooldown = TimeSpan.FromMilliseconds(400);
    public static readonly TimeSpan SessionTimeout = TimeSpan.FromSeconds(120);

    /// <summary>How long after the drag threshold an app may take to start its OLE drag before the drag is ignored.</summary>
    public static readonly TimeSpan FileDragConfirmTimeout = TimeSpan.FromMilliseconds(1000);

    /// <summary>How long the payload check may take to see the drag before the drag is ignored.</summary>
    public static readonly TimeSpan PayloadCheckTimeout = TimeSpan.FromMilliseconds(700);

    private const int VkEscape = 0x1B;
    private const int VkDigit1 = 0x31;
    private const int VkDigit9 = 0x39;
    private const int VkNumpad1 = 0x61;
    private const int VkNumpad9 = 0x69;

    private readonly object _sync = new();
    private readonly IVirtualDesktopService _desktops;
    private readonly ISelectorView _view;
    private readonly IWindowProbe _probe;
    private readonly TimeProvider _time;
    private readonly ILogger _log;
    private readonly HoverDwellTracker _dwell = new(HoverGrace);
    private readonly SwitchGuard _guard = new(SwitchCooldown);
    private readonly DragClassifier _classifier;

    private DragToSwitchSettings _settings;
    private int _downX, _downY, _lastX, _lastY;
    private DateTimeOffset _suspectedAt, _sessionStart;
    private DragKind _kind;
    private nint _movingWindow;
    private bool _windowMoved;
    private Guid _originDesktop;
    private IReadOnlyList<CardModel> _cards = [];
    private SelectorGeometry? _geometry;
    private bool _overSelector;
    private int _sessionId;
    private SessionSummary? _pendingSummary;

    public DragInteractionCoordinator(IVirtualDesktopService desktops, ISelectorView view, IWindowProbe probe, DragToSwitchSettings settings, TimeProvider time, ILogger log)
    {
        _desktops = desktops;
        _view = view;
        _probe = probe;
        _settings = settings.Clone();
        _time = time;
        _log = log.ForContext<DragInteractionCoordinator>();
        _classifier = new DragClassifier(probe, _log);
        _desktops.DesktopsChanged += OnDesktopsChanged;
    }

    public SessionPhase Phase { get; private set; } = SessionPhase.Idle;

    public SessionSummary? LastSession { get; private set; }

    public event EventHandler<SessionSummary>? SessionEnded;

    /// <summary>True while number keys should be swallowed (read on the hook thread).</summary>
    public bool ShouldSwallowKey(InputEvent e) =>
        Volatile.Read(ref _swallowDigits) && e.Kind == InputKind.KeyDown && IsDigit(e.VirtualKey);

    private bool _swallowDigits;

    public void UpdateSettings(DragToSwitchSettings settings)
    {
        lock (_sync)
        {
            _settings = settings.Clone();
        }
    }

    public void Process(InputEvent e)
    {
        lock (_sync)
        {
            DateTimeOffset now = _time.GetUtcNow();
            switch (e.Kind)
            {
                case InputKind.LeftDown:
                    OnLeftDown(e.X, e.Y);
                    break;
                case InputKind.Move:
                    _lastX = e.X;
                    _lastY = e.Y;
                    OnMove(now);
                    break;
                case InputKind.LeftUp:
                    OnLeftUp();
                    break;
                case InputKind.KeyDown when e.VirtualKey == VkEscape:
                    OnEscape();
                    break;
                case InputKind.KeyDown when IsDigit(e.VirtualKey):
                    OnDigit(e.VirtualKey, now);
                    break;
            }

            Evaluate(now);
        }
    }

    /// <summary>Periodic evaluation (debounce, dwell completion, timeout). Called ~60 Hz only while a session is live.</summary>
    public void Tick()
    {
        lock (_sync)
        {
            Evaluate(_time.GetUtcNow());
        }
    }

    public bool NeedsTicks => Phase is SessionPhase.Suspected or SessionPhase.Active or SessionPhase.Switching or SessionPhase.Collapsed;

    /// <summary>Ends any live session immediately (tool disabled or app exiting).</summary>
    public void Abort(string reason)
    {
        lock (_sync)
        {
            if (Phase is SessionPhase.Active or SessionPhase.Switching or SessionPhase.Collapsed)
            {
                End(SessionEnd.Aborted, reason);
            }

            Phase = SessionPhase.Idle;
        }
    }

    public void Dispose() => _desktops.DesktopsChanged -= OnDesktopsChanged;

    private static bool IsDigit(int vk) => vk is >= VkDigit1 and <= VkDigit9 or >= VkNumpad1 and <= VkNumpad9;

    private void OnLeftDown(int x, int y)
    {
        if (Phase is SessionPhase.Active or SessionPhase.Switching or SessionPhase.Collapsed)
        {
            End(SessionEnd.Aborted, "unexpected button press");
        }

        _classifier.StopPayloadCheck();
        Phase = SessionPhase.Idle;
        if (!_settings.Enabled || _probe.IsFullScreenAppRunning())
        {
            return;
        }

        if (!_classifier.BeginDrag(_probe.GetWindowAt(x, y), _settings.Sources, out bool fileSourceAllowed))
        {
            return;
        }

        if (!fileSourceAllowed && !WindowMovesAvailable)
        {
            return;
        }

        _downX = _lastX = x;
        _downY = _lastY = y;
        Phase = SessionPhase.Armed;
    }

    private void OnMove(DateTimeOffset now)
    {
        if (Phase == SessionPhase.Armed)
        {
            int dx = Math.Abs(_lastX - _downX), dy = Math.Abs(_lastY - _downY);
            if (Math.Max(dx, dy) >= _probe.DragThreshold)
            {
                // What is being dragged is decided after the debounce (Classify), when the app has had time
                // to enter its move loop for a title-bar drag.
                Phase = SessionPhase.Suspected;
                _suspectedAt = now;
            }
        }
        else if (Phase == SessionPhase.Suspected)
        {
            _classifier.OnMove(_lastX, _lastY);
        }
    }

    private bool WindowMovesAvailable => _settings.MoveWindowsEnabled && _desktops.CanMoveWindows;

    private void OnLeftUp()
    {
        _classifier.StopPayloadCheck();
        if (Phase is SessionPhase.Active or SessionPhase.Switching or SessionPhase.Collapsed)
        {
            End(SessionEnd.Released, null);
        }

        Phase = SessionPhase.Idle;
    }

    private void OnEscape()
    {
        _classifier.StopPayloadCheck();
        if (Phase is SessionPhase.Active or SessionPhase.Switching or SessionPhase.Collapsed)
        {
            bool switched = _desktops.CurrentDesktopId != _originDesktop;
            Guid origin = _originDesktop;
            bool returnWindow = _windowMoved;
            nint window = _movingWindow;
            End(SessionEnd.Cancelled, null);

            // Esc cancels a window move, so the window goes back to the desktop it came from;
            // "Return to the original desktop" decides whether the user goes back with it.
            if (returnWindow && _settings.ReturnToOriginOnCancel)
            {
                _ = _desktops.MoveWindowAndSwitchAsync(window, origin);
            }
            else if (returnWindow)
            {
                _ = _desktops.MoveWindowAsync(window, origin);
            }
            else if (switched && _settings.ReturnToOriginOnCancel)
            {
                _ = _desktops.SwitchToAsync(origin);
            }
        }

        if (Phase != SessionPhase.Idle)
        {
            Phase = SessionPhase.WaitingForRelease;
        }
    }

    private void OnDigit(int vk, DateTimeOffset now)
    {
        if (Phase != SessionPhase.Active || !_settings.NumberKeysEnabled)
        {
            return;
        }

        int number = vk >= VkNumpad1 ? vk - VkNumpad1 + 1 : vk - VkDigit1 + 1;
        int index = number - 1;
        if (index < _cards.Count && !_cards[index].IsCurrent && _guard.CanSwitchByKey(now))
        {
            BeginSwitch(index);
        }
    }

    private void Evaluate(DateTimeOffset now)
    {
        switch (Phase)
        {
            case SessionPhase.Suspected when now - _suspectedAt >= SuspectDebounce:
                DragDecision decision = _classifier.Classify(now, _suspectedAt, _lastX, _lastY, WindowMovesAvailable);
                _kind = decision.Kind;
                _movingWindow = decision.MovingWindow;
                DragClass drag = decision.Class;
                if (drag == DragClass.Ignore)
                {
                    _classifier.StopPayloadCheck();
                    Phase = SessionPhase.WaitingForRelease;
                    return;
                }

                if (drag == DragClass.Pending)
                {
                    return; // the app hasn't started its OLE drag yet; check again on the next tick
                }

                TryStartSession(now);
                break;
            case SessionPhase.Active:
            case SessionPhase.Collapsed:
                if (now - _sessionStart > SessionTimeout)
                {
                    End(SessionEnd.Aborted, "session timeout");
                    Phase = SessionPhase.WaitingForRelease;
                    return;
                }

                UpdateHover(now);
                break;
        }
    }

    private void TryStartSession(DateTimeOffset now)
    {
        (PixelRect work, double scale) = _probe.GetMonitorAt(_lastX, _lastY);
        if (_settings.Trigger == SelectorTrigger.EdgeSummon && !SelectorLayout.IsInSummonZone(_settings.OverlayPosition, work, scale, _lastX, _lastY))
        {
            return; // stay Suspected until the cursor reaches the edge
        }

        IReadOnlyList<VirtualDesktopInfo> desktops = _desktops.Desktops;
        if (_desktops.Backend == VirtualDesktopBackend.Unavailable || (desktops.Count < 2 && !_settings.ShowWithSingleDesktop))
        {
            Phase = SessionPhase.WaitingForRelease;
            return;
        }

        _sessionId++;
        _sessionStart = now;
        _originDesktop = _desktops.CurrentDesktopId;
        _cards = BuildCards(desktops);
        _geometry = SelectorLayout.Compute(_cards.Count, _settings.OverlaySize, _settings.PreviewStyle, _settings.OverlayPosition, work, scale, _lastX, _lastY);
        _dwell.Reset();
        _guard.Reset();
        _overSelector = false;
        _pendingSummary = null;
        _windowMoved = false;
        Phase = SessionPhase.Active;
        Volatile.Write(ref _swallowDigits, _settings.NumberKeysEnabled);
        _log.Information("Drag session {Session} started: kind={Kind} source={Source} desktops={Count} trigger={Trigger}", _sessionId, _kind, _classifier.SourceProcess, _cards.Count, _settings.Trigger);
        _view.Show(_cards, _geometry, _settings, _kind);
    }

    private void UpdateHover(DateTimeOffset now)
    {
        if (_geometry is null)
        {
            return;
        }

        if (Phase == SessionPhase.Collapsed)
        {
            if (_geometry.Pill.Contains(_lastX, _lastY))
            {
                Phase = SessionPhase.Active;
                Volatile.Write(ref _swallowDigits, _settings.NumberKeysEnabled);
                _view.Expand();
            }

            return;
        }

        bool over = _geometry.Overlay.Contains(_lastX, _lastY);
        if (over != _overSelector)
        {
            _overSelector = over;
            _view.SetCursorOverSelector(over);
        }

        int hysteresis = (int)Math.Round(8 * _geometry.Scale);
        int? hit = CardHitTester.HitTest(_geometry.Cards, _lastX, _lastY, _dwell.Target, hysteresis);
        if (hit is null)
        {
            _guard.CursorLeftCards();
        }

        int? previous = _dwell.Target;
        var delay = TimeSpan.FromMilliseconds(_settings.HoverDelayMs);
        bool fired = _dwell.Update(hit is int h && !_cards[h].IsCurrent && _guard.CanSwitch(now) ? hit : null, now, delay);
        if (_dwell.Target != previous)
        {
            if (previous is int p && p < _cards.Count)
            {
                _view.SetCardState(p, CardVisualState.Rest, delay);
            }

            if (_dwell.Target is int t)
            {
                _view.SetCardState(t, CardVisualState.Dwelling, delay);
            }
        }

        if (fired && _dwell.Target is int target)
        {
            BeginSwitch(target);
        }
    }

    private void BeginSwitch(int index)
    {
        CardModel card = _cards[index];
        int session = _sessionId;
        _guard.Begin();
        Phase = SessionPhase.Switching;
        Volatile.Write(ref _swallowDigits, false);
        _view.SetCardState(index, CardVisualState.Switching, TimeSpan.Zero);
        int from = _cards.FirstOrDefault(c => c.IsCurrent)?.Number ?? 0;

        Task<SwitchResult> switching = _kind == DragKind.Window
            ? _desktops.MoveWindowAndSwitchAsync(_movingWindow, card.DesktopId)
            : _desktops.SwitchToAsync(card.DesktopId);
        switching.ContinueWith(
            t => OnSwitchCompleted(session, index, from, t.IsCompletedSuccessfully ? t.Result : new SwitchResult(SwitchOutcome.Failed, TimeSpan.Zero, "Unexpected error.")),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private void OnSwitchCompleted(int session, int index, int from, SwitchResult result)
    {
        lock (_sync)
        {
            DateTimeOffset now = _time.GetUtcNow();
            if (session != _sessionId || Phase != SessionPhase.Switching)
            {
                _guard.Complete(now, result.Succeeded);
                return; // the drag ended while the switch was in flight
            }

            _guard.Complete(now, result.Succeeded);
            _dwell.Reset();
            CardModel card = _cards[index];
            _pendingSummary = new SessionSummary(_sessionStart, _classifier.SourceProcess, from, card.Number, (int)result.Elapsed.TotalMilliseconds, SessionEnd.Released, null);
            if (result.Succeeded)
            {
                _windowMoved |= _kind == DragKind.Window;
                _cards = _cards.Select(c => c with { IsCurrent = c.DesktopId == card.DesktopId }).ToList();
                _view.UpdateCards(_cards, _geometry!);
                _view.SetCardState(index, CardVisualState.Switched, TimeSpan.Zero);
                if (_settings.CollapseAfterSwitch)
                {
                    Phase = SessionPhase.Collapsed;
                    Volatile.Write(ref _swallowDigits, false);
                    _view.Collapse(card);
                }
                else
                {
                    Phase = SessionPhase.WaitingForRelease;
                    Volatile.Write(ref _swallowDigits, false);
                    _view.Hide();
                }
            }
            else
            {
                _log.Warning("Switch to desktop {Number} failed: {Outcome} {Message}", card.Number, result.Outcome, result.Message);
                Phase = SessionPhase.Active;
                Volatile.Write(ref _swallowDigits, _settings.NumberKeysEnabled);
                _view.SetCardState(index, CardVisualState.Error, TimeSpan.Zero, result.Message ?? "Couldn't switch. Try Ctrl+Win+Arrow.");
            }
        }
    }

    private void OnDesktopsChanged(object? sender, EventArgs e)
    {
        lock (_sync)
        {
            if (Phase is not (SessionPhase.Active or SessionPhase.Collapsed or SessionPhase.Switching) || _geometry is null)
            {
                return;
            }

            IReadOnlyList<VirtualDesktopInfo> desktops = _desktops.Desktops;
            if (desktops.Count < 2 && !_settings.ShowWithSingleDesktop)
            {
                End(SessionEnd.Aborted, "fewer than two desktops");
                Phase = SessionPhase.WaitingForRelease;
                return;
            }

            IReadOnlyList<CardModel> fresh = BuildCards(desktops);
            if (fresh.Select(c => (c.DesktopId, c.Name, c.IsCurrent)).SequenceEqual(_cards.Select(c => (c.DesktopId, c.Name, c.IsCurrent))))
            {
                return;
            }

            Guid? hovered = _dwell.Target is int t && t < _cards.Count ? _cards[t].DesktopId : null;
            _cards = fresh;
            (PixelRect work, double scale) = _probe.GetMonitorAt(_geometry.Overlay.Left + 1, _geometry.Overlay.Top + 1);
            _geometry = SelectorLayout.Compute(_cards.Count, _settings.OverlaySize, _settings.PreviewStyle, _settings.OverlayPosition, work, scale, _lastX, _lastY);
            if (hovered is not null)
            {
                _dwell.Reset(); // the hovered card moved or disappeared; never switch to a stale target
            }

            _view.UpdateCards(_cards, _geometry);
        }
    }

    private void End(SessionEnd end, string? detail)
    {
        Volatile.Write(ref _swallowDigits, false);
        _view.Hide();
        SessionSummary summary = (_pendingSummary ?? new SessionSummary(_sessionStart, _classifier.SourceProcess, null, null, null, end, detail)) with { End = end, Detail = detail };
        _pendingSummary = null;
        _sessionId++;
        _dwell.Reset();
        LastSession = summary;
        _log.Information("Drag session ended: {End}{Detail}; kind={Kind}; switched {From}->{To} in {Ms} ms{Note}",
            end, detail is null ? string.Empty : $" ({detail})", _kind, summary.FromDesktop, summary.ToDesktop, summary.SwitchMs,
            _kind == DragKind.File ? " (delivery is handled by the target app)" : string.Empty);
        SessionEnded?.Invoke(this, summary);
    }

    private static List<CardModel> BuildCards(IReadOnlyList<VirtualDesktopInfo> desktops) =>
        desktops.Select(d => new CardModel(d.Id, d.Index + 1, d.Name, d.WallpaperPath, d.IsCurrent)).ToList();
}
