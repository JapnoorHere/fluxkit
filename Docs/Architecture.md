# Architecture

## Projects and dependencies

```
FluxKit.App  ──▶  FluxKit.Utilities.DragToSwitch  ──▶  FluxKit.Platform  ──▶  FluxKit.Core
     └──────────────────────────────────────────────────────────────────────────▲
```

| Project | Target | Responsibility |
|---|---|---|
| `FluxKit.Core` | net10.0 (no Windows APIs) | Utility model (`IFluxUtility`, `UtilityRegistry`), settings service, logging setup and redaction |
| `FluxKit.Platform` | net10.0-windows | Win32/COM only: virtual desktop service, low-level input hooks, input synthesis, window helpers, startup registration, tray icon, STA worker |
| `FluxKit.Utilities.DragToSwitch` | net10.0-windows, no XAML | The utility: settings section, selector geometry, dwell tracking, the interaction coordinator (state machine) |
| `FluxKit.App` | WinUI 3 exe | Composition root (`AppHost`), settings window and pages, selector overlay window |
| `tools/*`, `spikes/*` | — | DropProbe, DragHarness, VdProbe (feasibility and E2E) |

The utility library talks to its view only through `ISelectorView`, and to the OS only through `IVirtualDesktopService`, `ISystemInputSource` and `IWindowProbe`. That keeps the whole interaction logic unit-testable with fakes and a `FakeTimeProvider`.

## Threads

| Thread | Owns | Notes |
|---|---|---|
| UI (DispatcherQueue) | Settings window, selector window, tray icon | The selector is created once at startup and hidden. Coordinator calls arrive through `ViewAdapter`, which posts to the dispatcher and never blocks. |
| `FluxKit.InputHook` | `WH_MOUSE_LL` + `WH_KEYBOARD_LL` and a message pump | Callbacks copy the event into a `Channel<InputEvent>` and return. Moves are coalesced to one per 8 ms. Installed only while Drag-to-Switch is on. |
| `FluxKit.Shell` (STA) | Shell COM objects | Every virtual-desktop call runs here with a timeout. Re-probes after `RPC_E_DISCONNECTED` (Explorer restart). |
| Registry watcher | `RegNotifyChangeKeyValue` on `Explorer\VirtualDesktops` | Raises `DesktopsChanged`, debounced 150 ms. Uses no CPU while idle. |
| Pump loop (task) | `DragInteractionCoordinator` | Reads the input channel. Ticks at ~60 Hz **only during a drag session**, for dwell timing. |

## Drag session state machine

```
Idle ─LDown on allowed source─▶ Armed ─moved ≥ drag threshold─▶ Suspected ─held 120 ms─▶ Active (selector shown)
Active ─over card ≠ current─▶ dwell ─HoverDelayMs─▶ Switching ─verified─▶ Collapsed (pill)
                                                         └─failed─▶ Active (card error, 2 s)
Collapsed ─cursor on pill─▶ Active
any ─LUp─▶ Released · ─Esc─▶ Cancelled (optional return to origin) · ─disable/timeout/<2 desktops─▶ Aborted
```

- **Hysteresis:** 8 px around the hovered card. **Grace:** 120 ms for brief exits. **Cooldown:** 400 ms after a switch, and the cursor must leave all cards first.
- Number keys 1–9 are swallowed only while the selector is expanded. Esc, Ctrl, Shift and Alt are never swallowed.
- `SessionSummary` records counts and timings only (no file names). The Troubleshooting section shows it as "Last drag".

## Virtual desktop backends

1. **Shell COM.** `ImmersiveShell` → `IVirtualDesktopManagerInternal`, tried with each known interface layout. A layout is accepted only if the count, order and current desktop all match the registry. Pinning the selector to all desktops uses `IVirtualDesktopPinnedApps`.
2. **Keyboard.** Desktops are read from the registry, and switching sends `Ctrl+Win+←/→` |delta| times. The UI shows "Limited mode".
3. **Unavailable.** The selector is never shown.

Every switch is verified by re-reading `CurrentVirtualDesktop` from the registry. Forcing a backend: `FLUXKIT_VD_BACKEND=keyboard`.

## Safety invariants

- The selector window is `WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST` and is shown without activation. It never takes focus or mouse capture.
- Its drop target always reports `None`, so a release over the selector cannot turn into a move that deletes the source file.
- FluxKit never reports delivery. A session ends as Released, Cancelled or Aborted.

## Adding a tool

FluxKit is a collection of tools. In code, each tool is a "utility" (`IFluxUtility`). In short:

1. Create `src/FluxKit.Utilities.<Name>` referencing `FluxKit.Platform`.
2. Implement `IFluxUtility` with a unique `Id` and a settings section `utilities/<id>` (a class implementing `ISettingsSection`). Optionally set `UtilityDescriptor.IconAsset` to a colour icon in `Assets`.
3. Register it in `AppHost.StartAsync`: `Registry.Register(new MyUtility(...))`.
4. Add its settings page to `MainWindow.UtilityPages`.

The navigation item, Home card, status dot, tray toggle and tray tooltip are all generated from the registry. The full step-by-step guide, with the rules every tool must follow, is in [CONTRIBUTING.md](../CONTRIBUTING.md#adding-a-tool).
