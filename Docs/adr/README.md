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

## ADR-014 — A sparse identity package for reading notifications (2026-11-16)
**Context:** Flux Island should show Windows notifications in place of Windows' own banners. Windows exposes other apps' notifications only through `UserNotificationListener`, which needs a package identity, the `userNotificationListener` capability, and the user's permission. FluxKit is unpackaged (ADR-002). No API lets an app intercept or replace another app's banner.
**Decision:**
- FluxKit keeps its zip install but adds a *sparse* identity package (`packaging/Identity/AppxManifest.xml`, `AllowExternalContent`), which points at the folder `FluxKit.exe` already runs from. `app.manifest` carries the matching `<msix>` element.
- In development the manifest is registered unsigned in Developer Mode (`Add-AppxPackage -Register … -ExternalLocation <app folder>`). Release builds pack it with MakeAppx and sign it with a real certificate.
- Windows' banner for an app is turned off only with the user's consent, through its own per-app switch (`HKCU\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\<AUMID>\ShowBanner = 0`). The previous value is recorded first and restored exactly.

**Spike results (2026-11-16, this PC):**
- With the identity registered, `Package.Current` resolved (`FluxKit.Identity_1.0.0.0_neutral__99d4agh9szqr8`).
- `RequestAccessAsync` returned **Allowed**.
- `GetNotificationsAsync(Toast)` returned 31 current toasts with their app names, AUMIDs and `ToastGeneric` text elements.
- `NotificationChanged` **fired live** (Added) for a test toast and for a real one, so no polling is needed.
- The per-app settings keys exist under the path above. `ShowBanner` is absent by default, meaning banners are shown. Setting it to 0 and back worked; whether the banner visibly disappeared couldn't be confirmed on this PC, which shows no banners in the corner, so it is a manual test.

**Consequences:**
- The listener gives text and app info but not the toast's own buttons. The island can open the app, dismiss the notification or open Notification Center, but can't press "Reply" or "Accept".
- A signing certificate is needed before release.
- The identity can be removed with `Remove-AppxPackage FluxKit.Identity_…`.
- Turning off banners leaves notifications in Notification Center.

**As built (2026-11-16):**
- **Platform:** `PackageIdentity` (state, register, unregister), `NotificationWatcher` (listener events, logo cache) and `NotificationBanners` (the registry switch).
- **Tool:** `NotificationRules` (privacy, bursts, mute and priority), `NotificationInbox` (50, memory only) and `BannerControl` (record then restore, saved in `banners.json`).
- **App:** the logo and its colour on the pill, Open while peeking, a grouped Notifications page in place of a separate card, and a settings group that runs setup and permission one step at a time.
- **Restarts:** after a restart, permission is asked on the UI thread and the island then starts reading (`ResumeNotifications`).
- **Verified live:** a real toast showed on the island, its banner was turned off, and after a forced kill the next start put the banner back.

## ADR-015 — "Liquid": the island behaves like a drop of liquid (2026-11-16)
**Context:** every island on the market, on any platform, is a black pill that grows and shrinks. Flux Island wants a look of its own that is still calm, light and readable.
**Decision:**
- The island is treated as liquid:
  - Moments are *drunk in* on arrival: a drop is pulled out of the underside on a neck, then drawn back.
  - Moments waved away *shed* a drop that falls and fades.
  - The second bubble *buds* off sideways.
  - Later phases add tear-off droplets and a ferrofluid edge that follows sound.
- **How it's drawn:** drops are circles and necks are round-ended bars, in the island's own surface brush (`Design/LiquidShape.cs`). They are drawn in the same shape visual as the body, so they read as one piece.
- **Cost:** everything is compositor keyframes, with no Win2D, no blur and no per-frame UI work.
- **Window region:** while a drop hangs, the region reaches `LiquidShape.Reach` px lower for about 0.8 s, then shrinks back so clicks under the island pass through again.
- **Mercury:** a new labs surface, liquid metal. It is a vertical gunmetal gradient, a brighter rim, and a soft specular spot that eases toward the pointer (one property animation per pointer move).
**Consequences:**
- Reduced motion and "light" effects skip the liquid entirely.
- In very bright screenshots the drop is small (14 px). It is meant to be felt, not to steal attention.

## ADR-016 — Live Activities for any script or app (2026-11-16)
**Context:** builds, deployments, test runs and AI agents (such as Claude Code) take minutes, and people switch away while they wait. No island lets other software put its own live progress on it.
**Decision:**
- **Transport:** a named pipe, `\.\pipe\FluxKit.Island.<user SID>`, created with `PipeOptions.CurrentUserOnly`, so only the signed-in user's processes can connect.
  - The server (`Platform/Watchers/LiveActivityServer.cs`) waits for connections without polling.
  - It reads at most 16 KB per connection and gives each connection 3 s.
- **Protocol:** one JSON object per line: `id`, `title`, `detail`, `progress` (0–100), `state` (`running`, `done`, `failed`, `clear`), `glyph` (a few named icons) and `ttl` (seconds).
  - `LiveProtocol` drops malformed or oversize lines, strips control characters, and cuts titles to 60 and details to 120 characters.
  - `LiveActivityModel` keeps at most 5 activities; each ends after its ttl (default 30 min, at most 8 h).
- **Display only:** an update can never make FluxKit open, run or fetch anything.
- **Sending:** `FluxKit.exe island set|done|fail|clear --id … [--title …] [--detail …] [--progress N] [--glyph G] [--ttl S]` sends one update and exits without a window. In PowerShell, pipe it to `Out-Null` so calls stay in order.
- **Off by default** (Settings › Flux Island › Labs). Logs record ids and states only.
**Consequences:** any process running as the user can show text on the island while this is on. That is the same trust level as the user's own scripts, so it's labs and opt-in.
