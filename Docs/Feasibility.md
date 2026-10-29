# Phase 1 — Feasibility Results

Only results that were **actually executed** are marked as passed. Everything else says "Not run yet".

**Test machine:** Windows 11 Home Single Language 26H2, build 10.0.26300.9711, 4 virtual desktops, .NET SDK 10.0.303.
**Date:** 2026-10-26

## Results

| ID | Hypothesis | Method | Status | Evidence |
|---|---|---|---|---|
| H7 | Internal shell COM API resolves on build 26300 (enumerate, current, switch) | `spikes/VdProbe` | **PASS** | Layout `Win11-24H2` (`IVirtualDesktopManagerInternal {53F5CA0B-…}`) resolved. The 4 desktop GUIDs, their order and the current desktop matched the registry exactly. `switch 2` and `switch 1` were confirmed through the registry in 7 ms and 3 ms. `IVirtualDesktopPinnedApps` and `IApplicationViewCollection` also resolved (`canPin=True`). |
| H2 | A real OLE file drag survives a **programmatic** switch made by a *different process* | `tools/FluxKit.DragHarness --switch com-direct` (real `DoDragDrop` with `CF_HDROP`, mouse via `SendInput`, `VdProbe.exe` switches mid-drag) | **PASS** | DropProbe on Desktop 2 logged the exact temp-file path. `DoDragDrop` returned `Copy` and the source file was intact. In an earlier run, before the probe was made topmost, the same drag was **delivered to Chrome** (`chrome/Chrome_RenderWidgetHostHWND`) on Desktop 2, which also shows delivery to a real third-party app. |
| H6 | The keyboard fallback (`Ctrl+Win+→` injected mid-drag) keeps the drag alive | `DragHarness --switch manual-keys` | **PASS** | DropProbe on Desktop 2 received the exact path. Result `Copy`, source intact. |
| H1 | A drag from **File Explorer** survives a manual `Ctrl+Win+→` | Manual | Not run yet | Needs a human to drag from Explorer. Steps are in `ManualTestPlan.md` (MT-01). |
| H3 | A topmost, non-activating overlay mid-drag doesn't break the drag | FluxKit + harness `--switch fluxkit` | Not run yet | Runs once the selector window exists (M4/M5). |
| H4 | The overlay can see a file payload but never accepts the drop | Harness `--scenario release-on-overlay` | Not run yet | M5. |
| H5 | Telling a file drag apart from other drags | Detection rules + MT-12 | Not run yet | M5. |
| H8 | Number keys during a drag can be swallowed without side effects | MT-13 | Not run yet | M5. |

## Go / No-Go
**GO (full experience).** The two hardest unknowns are resolved: the drag survives a switch made by another process (H2), and the shell COM API works on this build (H7). The keyboard path (H6) is confirmed as a working fallback.

## Observations that shaped the design
- Windows doesn't raise a window that was moved to another desktop above that desktop's existing windows. The selector therefore has to be topmost and must re-assert its z-order after each switch.
- An unrelated app underneath the cursor (Chrome here) will happily accept the drop. FluxKit must never report "delivered", only that the drag was "released".
- The registry's `CurrentVirtualDesktop` updates within a few milliseconds of a switch, so it works as an independent check that a switch actually happened.

## How to reproduce
```powershell
dotnet build tools/FluxKit.DragHarness; dotnet build tools/FluxKit.DropProbe; dotnet build spikes/VdProbe
dotnet run --project spikes/VdProbe                       # H7 (read-only)
tools/FluxKit.DragHarness/bin/Debug/<tfm>/FluxKit.DragHarness.exe --switch com-direct --target 2   # H2
tools/FluxKit.DragHarness/bin/Debug/<tfm>/FluxKit.DragHarness.exe --switch manual-keys --target 2  # H6
```
The harness moves the mouse for about 5 seconds and returns to the original desktop afterwards. Results go to `harness-result.json`.
