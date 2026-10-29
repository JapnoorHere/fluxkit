# Architecture decision records

Each record: **context → decision → consequences**. Status for all: *Accepted (2026-10-26)*.

## ADR-001 — C#, .NET 10, WinUI 3
Native Fluent UI and long-term support. Nullable on, warnings are errors, analyzers at `latest-recommended` for libraries.

## ADR-002 — Unpackaged, self-contained
Distributed as a zip, with no runtime installer and no admin rights. Startup uses an HKCU `Run` value. MSIX is deferred.

## ADR-003 — Drag detection with low-level hooks
`WH_MOUSE_LL` and `WH_KEYBOARD_LL` run in FluxKit's own process and need no injection. They observe input without changing the OLE drag in the source app. Detection is heuristic (allowed source, drag threshold, no move or size in progress, debounce). **Consequence:** some false positives are possible in "Any app" mode, and this is documented.

## ADR-004 — The selector never accepts a drop
Its drop target always reports `None`. Accepting a *move* could make Explorer delete the source file. FluxKit never claims delivery.

## ADR-005 — Backend ladder for virtual desktops
1. Shell COM, with interface layouts probed and validated against the registry.
2. Registry plus `Ctrl+Win+Arrow`.
3. Unavailable.

The undocumented API is isolated in `Platform/VirtualDesktop/Interop`, and each switch is verified through the registry. Calls go through raw vtable slots so layouts can be chosen at runtime.

## ADR-006 — Registry notifications for desktop changes
`RegNotifyChangeKeyValue` is version-proof and costs nothing while idle. No polling.

## ADR-007 — A pre-created, non-activating, topmost overlay pinned to all desktops
The overlay never takes focus or capture, and appears quickly. If pinning fails, it falls back to `MoveWindowToDesktop` on its own window. Feasibility showed it must re-assert topmost after each switch.

## ADR-008 — Hover decided from hook coordinates
XAML pointer events don't fire during an OLE drag. Cards are hit-tested in physical pixels against the geometry computed by `SelectorLayout`, which is pure and unit-tested.

## ADR-009 — Dedicated threads
Hook thread, STA shell thread and UI thread, connected by channels and dispatcher posts. Hook callbacks return in microseconds, and COM apartment rules are respected.

## ADR-010 — JSON settings with schema version, atomic writes and quarantine
Settings are robust to hand edits and crashes. Each field is bound independently, unknown fields are preserved, and a newer schema opens read-only.

## ADR-011 — Tool registry
Navigation, Home cards, tray toggles and status come from `ToolRegistry`. A new tool doesn't change the shell.

## ADR-012 — Hand-written P/Invoke (deviation from the plan's CsWin32)
The interop surface is about 40 functions. Hand-written `LibraryImport` declarations keep it small, reviewable and free of a source generator. **Lesson recorded:** `LibraryImport` doesn't add the `W` suffix, so string or message APIs must name their `...W` entry point explicitly (a bug found on first launch).

## ADR-013 — "Tool" in code, "utilities" in saved settings (2026-10-30)
**Context:** the app calls each feature a *tool*, but the code said *utility* (`IFluxUtility`, `FluxKit.Utilities.DragToSwitch`), so contributors had to translate between the two. **Decision:** the code now uses the same word as the UI: `IFluxTool`, `ToolRegistry`, `ToolDescriptor`, `ToolState`, `DragToSwitchTool`, `FluxKit.Core.Tools` and `FluxKit.Tools.<Name>` projects. The saved settings keys are **not** renamed: tool sections stay `"utilities/<id>"` under the `"utilities"` node, built only by `SettingsKeys`. **Consequences:** existing settings files keep working with no migration, and tests pin the saved keys. `Docs/PLAN.md` and the changelog keep the old word because they record history. ADR-011 was renamed with the rest.
