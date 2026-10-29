using FluxKit.Platform.Windows;
using Serilog;

namespace FluxKit.Utilities.DragToSwitch.Interaction;

/// <summary>What <see cref="DragClassifier.Classify"/> decided about a suspected drag.</summary>
internal enum DragClass
{
    Ignore,
    Pending,
    Start,
}

/// <summary>A classification result: whether to start a session, and what is being dragged.</summary>
internal readonly record struct DragDecision(DragClass Class, DragKind Kind, nint MovingWindow);

/// <summary>
/// Decides what a drag is: a window move (title-bar drag), a file drag, or neither (resize, menu, text selection,
/// scrollbar, or a source that isn't allowed). Owned by <see cref="DragInteractionCoordinator"/> and only called
/// under its lock.
/// </summary>
internal sealed class DragClassifier(IWindowProbe probe, ILogger log)
{
    /// <summary>
    /// Root window classes of shell surfaces whose drags never carry files: dragging a taskbar button reorders it
    /// and dragging a tray icon moves it. They belong to explorer.exe, so they must be excluded explicitly; the app
    /// behind a taskbar button can't be moved along either (no API maps a button to one window).
    /// </summary>
    private static readonly HashSet<string> NonFileShellSurfaces = new(StringComparer.Ordinal)
    {
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd",
        "NotifyIconOverflowWindow",
        "TopLevelWindowForOverflowXamlIsland",
    };

    private nint _sourceWindow;
    private bool _fileSourceAllowed;
    private DateTimeOffset? _payloadCheckStart;
    private bool _carriesFiles;

    /// <summary>The process the current drag started in (a name only, safe to log).</summary>
    public string SourceProcess { get; private set; } = string.Empty;

    /// <summary>
    /// Starts a new drag at the window under the button press. Returns false when the press is on a shell surface
    /// that never drags files; otherwise <paramref name="fileSourceAllowed"/> says whether a file drag from here counts.
    /// </summary>
    public bool BeginDrag(WindowInfo? source, DragSources sources, out bool fileSourceAllowed)
    {
        StopPayloadCheck();
        _carriesFiles = false;
        fileSourceAllowed = false;
        if (source is not null && NonFileShellSurfaces.Contains(source.ClassName))
        {
            return false;
        }

        SourceProcess = source?.ProcessName ?? string.Empty;
        _sourceWindow = source?.RootWindow ?? 0;
        _fileSourceAllowed = fileSourceAllowed = sources == DragSources.AnyApp || IsExplorer;
        return true;
    }

    /// <summary>Keeps a running payload check under the cursor.</summary>
    public void OnMove(int x, int y)
    {
        if (_payloadCheckStart is not null)
        {
            probe.MovePayloadCheck(x, y);
        }
    }

    /// <summary>
    /// A file drag counts only once the app has really started an OLE drag; until then (up to
    /// <see cref="DragInteractionCoordinator.FileDragConfirmTimeout"/>) it is pending. Drags that don't start in
    /// File Explorer can carry text or links too, so for those a payload check then confirms that files are being dragged.
    /// </summary>
    public DragDecision Classify(DateTimeOffset now, DateTimeOffset suspectedAt, int x, int y, bool windowMovesAvailable)
    {
        nint moving = probe.GetWindowBeingMoved();
        if (moving != 0)
        {
            StopPayloadCheck();
            return new DragDecision(windowMovesAvailable ? DragClass.Start : DragClass.Ignore, DragKind.Window, moving);
        }

        return new DragDecision(ClassifyFileDrag(now, suspectedAt, x, y), DragKind.File, 0);
    }

    public void StopPayloadCheck()
    {
        if (_payloadCheckStart is not null)
        {
            _payloadCheckStart = null;
            probe.EndPayloadCheck();
        }
    }

    private bool IsExplorer => string.Equals(SourceProcess, "explorer", StringComparison.OrdinalIgnoreCase);

    private DragClass ClassifyFileDrag(DateTimeOffset now, DateTimeOffset suspectedAt, int x, int y)
    {
        if (!_fileSourceAllowed || probe.IsForegroundInMoveSizeOrMenu())
        {
            return DragClass.Ignore;
        }

        if (_payloadCheckStart is null && !probe.IsFileDragInProgress(_sourceWindow))
        {
            return now - suspectedAt < DragInteractionCoordinator.FileDragConfirmTimeout ? DragClass.Pending : DragClass.Ignore;
        }

        // File Explorer and the desktop only start OLE drags for files and folders.
        if (_carriesFiles || IsExplorer)
        {
            return DragClass.Start;
        }

        if (_payloadCheckStart is null)
        {
            _payloadCheckStart = now;
            probe.BeginPayloadCheck(x, y);
            return DragClass.Pending;
        }

        switch (probe.PayloadCheckResult)
        {
            case DragPayload.Files:
                StopPayloadCheck();
                _carriesFiles = true; // remembered while waiting for the screen edge (EdgeSummon)
                return DragClass.Start;
            case DragPayload.Other:
                StopPayloadCheck();
                log.Information("Drag from {Source} ignored: it doesn't carry files", SourceProcess);
                return DragClass.Ignore;
            default:
                if (now - _payloadCheckStart.Value < DragInteractionCoordinator.PayloadCheckTimeout)
                {
                    return DragClass.Pending;
                }

                StopPayloadCheck();
                log.Information("Drag from {Source} ignored: couldn't tell what it carries", SourceProcess);
                return DragClass.Ignore;
        }
    }
}
