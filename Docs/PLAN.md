# FluxKit — Engineering Plan & Product Specification (v1.0: Drag-to-Switch)

| Field | Value |
|---|---|
| Document | FluxKit Engineering Plan & Product Spec |
| Version | 1.0 (draft for approval) |
| Date | 2026-10-26 |
| Status | Approved 2026-10-26 |
| Scope | FluxKit shell + first utility **Drag-to-Switch** |
| Workspace | `D:\Crazy projects\fluxkit` (currently empty, no git repo) |
| Delivery note | Once approved, the first implementation step saves this document as `Docs/PLAN.md` in the repo so it lives with the code. |

---

## 0. Context

Windows users with several virtual desktops can't easily drag a file from an app on one desktop to an app on another. Today the only way is to hold the mouse button and press `Ctrl+Win+←/→`, which few people know about and which gets awkward past two desktops. **FluxKit** is a native Windows 11 toolkit in the style of PowerToys. Its first utility, **Drag-to-Switch**, shows a small desktop selector while a file drag is in progress. Holding the dragged file over a desktop card switches to that desktop and **keeps the original OLE drag alive**, so the user can drop the file into a real app there.

The goals are:
1. Prove, on this machine, that the real drag survives a programmatic desktop switch and that a real target receives the file.
2. Ship a polished shell that can host more utilities later.
3. Ship Drag-to-Switch with honest fallbacks and documentation.

### Non-goals (v1)
- Any utility other than Drag-to-Switch. No placeholder or "coming soon" pages.
- Replacing or copying Windows Task View.
- Code injection, drivers, services, or admin rights.
- Telemetry or network access of any kind.
- MSIX Store submission. A zip build is the v1 distributable, and MSIX is designed for but deferred.

---

## 1. Environment Findings (verified 2026-10-26, read-only inspection)

| Item | Finding | Implication |
|---|---|---|
| OS | Windows 11 Home Single Language, build **10.0.26300**, DisplayVersion **26H2** | Newer than 24H2 (26100). The virtual-desktop COM IIDs **must be probed at runtime**. Phase 1 confirms which layout works. |
| .NET SDKs | 9.0.316, **10.0.303** | Target **net10.0** (LTS). |
| WinUI templates | `winui`, `winui-navview`, `winui-lib`, `winui-unittest` installed | Scaffold from the templates, then restructure. |
| Windows App SDK | NuGet cache: `Microsoft.WindowsAppSDK` **2.3.1**. Runtimes 1.1–1.8 and **2.x** installed | Use WinAppSDK **2.3.1**, unpackaged and **self-contained** so it doesn't depend on the installed runtime. |
| Visual Studio | VS Community 2022 | Optional. Everything builds with the `dotnet` CLI. |
| Cached packages | `H.NotifyIcon.WinUI` 2.4.1, xUnit 2.9.3, runner 3.1.4 | Use these for the tray icon and tests. |
| Git | 2.55 present; workspace is **not** a repo | `git init` as the first step. |

Packages to add (all MIT or Apache, all small):
- `Microsoft.WindowsAppSDK` 2.3.1
- `Microsoft.Windows.SDK.BuildTools`
- `Microsoft.Windows.CsWin32` for generated P/Invoke
- `CommunityToolkit.Mvvm`
- `CommunityToolkit.WinUI.Controls.SettingsControls` for SettingsCard/SettingsExpander, the same look as PowerToys
- `H.NotifyIcon.WinUI`
- `Microsoft.Extensions.DependencyInjection`
- `Serilog` + `Serilog.Sinks.File`
- Tests only: `xunit`, `Microsoft.Extensions.TimeProvider.Testing` (FakeTimeProvider), `NSubstitute`

---

## 2. Key Technical Decisions (ADR summary)

Each decision gets a short ADR file in `Docs/adr/`.

| # | Decision | Rationale |
|---|---|---|
| ADR-001 | C# / .NET 10 / WinUI 3 (WinAppSDK 2.3.1), nullable on, warnings treated as errors | Required stack. Native Fluent UI. |
| ADR-002 | **Unpackaged, self-contained** app (`WindowsPackageType=None`, `WindowsAppSDKSelfContained=true`) | xcopy/zip distribution with no runtime installer. A `Run` key is enough for startup. MSIX can be added later. |
| ADR-003 | **Drag detection = global low-level mouse/keyboard hooks (`WH_MOUSE_LL`, `WH_KEYBOARD_LL`) + heuristics + OLE drop-target confirmation on the overlay** | LL hooks need no injection and run in our process. They observe input without changing it, so the OLE session in the source app is untouched. The overlay's drop target reads the real `IDataObject` and confirms the payload is files (`CF_HDROP`/StorageItems). |
| ADR-004 | **Overlay drop target always reports `DROPEFFECT_NONE`** | Safety-critical. If a release over the overlay ever reported MOVE, Explorer could delete the source file. FluxKit never accepts the drop and never claims delivery. |
| ADR-005 | **Virtual-desktop backend ladder** behind `IVirtualDesktopService`: (1) Shell COM internal API with per-build IID tables, probed and validated at runtime; (2) registry read-only enumeration + `SendInput` `Ctrl+Win+←/→` switching; (3) Unavailable | The internal COM API is undocumented and its IIDs change between builds. The registry (`HKCU\...\Explorer\VirtualDesktops`) has been stable for enumeration and names. Keyboard switching is the shortcut Windows itself documents. |
| ADR-006 | Desktop change notifications via **`RegNotifyChangeKeyValue`** on the VirtualDesktops key (no polling). The COM notification service is optional extra. | Version-proof, event-driven, zero idle CPU. |
| ADR-007 | Overlay is a WinUI 3 window with `WS_EX_NOACTIVATE \| WS_EX_TOOLWINDOW \| WS_EX_TOPMOST`, shown with `SW_SHOWNOACTIVATE`, **pinned to all desktops** (COM `IVirtualDesktopPinnedApps`; tool-window style as fallback). Pre-created at startup and hidden. | It never steals focus or capture, survives desktop switches, and shows in under 50 ms. |
| ADR-008 | Hover/dwell is decided by **hook cursor coordinates hit-testing card rectangles** (in physical pixels), not by XAML pointer events | XAML pointer events don't fire during an OLE drag, and drag-over events depend on the drop-target path. Hook coordinates are authoritative and testable. |
| ADR-009 | Dedicated threads: `FluxKit.InputHook` (hook + message pump), `FluxKit.Shell` (STA, owns all VD COM objects), UI thread (DispatcherQueue). Communication through `System.Threading.Channels`. | Hook callbacks must return within `LowLevelHooksTimeout`. COM apartment rules are respected. The UI never blocks. |
| ADR-010 | Settings in JSON at `%LOCALAPPDATA%\FluxKit\settings.json` with `schemaVersion`, migrations, atomic writes, and corrupt-file quarantine | Spec requirement, robust to bad input. |
| ADR-011 | Utility modules implement `IFluxUtility` and register through `UtilityRegistry`. The shell builds navigation from the registry. | New utilities need no shell changes. |
| ADR-012 | P/Invoke via **CsWin32**. The internal VD COM interfaces are hand-declared `[ComImport]` interfaces, isolated in `FluxKit.Platform/VirtualDesktop/Interop/Builds/*`. | Version-sensitive code stays in one quarantined place. |

---

## 3. Phase 1 — Feasibility (gate before the full build)

### 3.1 Hypotheses to prove
| ID | Hypothesis | Proven when |
|---|---|---|
| H1 | An OLE file drag from Explorer survives a desktop switch triggered by the **manual** shortcut `Ctrl+Win+→` | The file arrives in DropProbe on the other desktop (baseline Windows behavior). |
| H2 | It survives a **programmatic** `SwitchDesktop` called from a separate, unrelated process (FluxKit) during the drag | DropProbe logs the exact path. |
| H3 | A topmost, non-activating overlay window shown mid-drag does not break the drag, including when the cursor passes over it | Drag continues and the drop succeeds after crossing the overlay. |
| H4 | The overlay can confirm a **file** payload through its drop target without accepting it | DragEnter shows StorageItems/CF_HDROP. Release over the overlay causes no file operation. |
| H5 | LL hooks can tell "OLE drag started" apart from window moves, text selection and rubber-band selection with acceptable accuracy | Logged signatures meet the criteria in 3.4. |
| H6 | The keyboard fallback (`SendInput` `Ctrl+Win+Arrow`) during a drag switches desktops and the drop still succeeds | DropProbe receives the file. A held Ctrl only changes the effect temporarily. |
| H7 | The internal COM IIDs for build 26300 can be resolved (enumerate, current, switch, pin) | The probe passes validation against the registry. |
| H8 | Number keys pressed during a drag can be swallowed by the LL keyboard hook without side effects in the source app | Explorer type-ahead is not triggered and the drag is unaffected. |

### 3.2 Spike artifacts (kept in repo under `spikes/` and `tools/`)
- **`tools/FluxKit.DropProbe`** (WinForms, net10.0-windows). A drop target window that accepts `FileDrop`, shows the received paths in a list, and appends JSONL to `%LOCALAPPDATA%\FluxKit\DropProbe\drops.jsonl` (`{ts, pid, desktopId, effect, paths[]}`). CLI flags: `--title`, `--move-to-desktop <guid|index>` (uses the public `IVirtualDesktopManager.MoveWindowToDesktop` on its own window), `--log <path>`. Reports `DROPEFFECT_COPY` only, so test sources are never deleted.
- **`tools/FluxKit.DragHarness`** (WinForms). An automated E2E driver:
  1. Creates a temp file `fluxkit-e2e-<guid>.txt`.
  2. Shows a drag-source window that calls `DoDragDrop` with a `CF_HDROP` DataObject.
  3. Launches DropProbe on desktop N.
  4. Drives the mouse with `SendInput`: press on the source, move past the threshold, move onto the overlay card for desktop N, wait for the dwell, move to the probe's client area, release.
  5. Asserts that the probe's JSONL contains the exact path within 5 s.
  Modes: `--switch=manual-keys|fluxkit|com-direct`. Exit code 0 means pass.
- **`spikes/DragSwitchSpike`** (console + minimal Win32/WinUI window). Runs H2–H8 in isolation. It logs, during every left-button drag, the `GetGUIThreadInfo` foreground flags (`GUI_INMOVESIZE`), the `hwndCapture` class name and process, the cursor handle (`GetCursorInfo`), and the process under the cursor at mouse-down. This builds the detection signature table for H5.
- **`spikes/VdProbe`**. Tries every known IID layout (see 6.2), prints the build, the chosen layout, desktop GUIDs and names, and compares them with the registry.

### 3.3 Execution
1. Build and run DropProbe and the harness. Run H1 manually. Give the user exact steps if a step needs a human hand on the mouse, or the harness covers it.
2. H7 on 26300. If no IID layout validates, record the failure and go to the keyboard fallback.
3. H2, H3, H4, H6 via the harness (`--switch=com-direct`, then `--switch=manual-keys`), then **by hand with real File Explorer** as the source and DropProbe plus a second real target as destinations. The second target is Notepad (opens the dropped .txt) or Chrome/Edge (gmail.com attachment or `file://` open).
4. H5: perform 20 each of: Explorer file drag, desktop icon drag, window title-bar move, text selection in Notepad, rubber-band select in Explorer, scrollbar drag, browser link/image drag. Tabulate the signatures.
5. H8: test in Explorer, Desktop and Chrome.
6. Write `Docs/Feasibility.md`. Include a results table (hypothesis, method, ran/not-ran, result, evidence path), the signature table, the chosen detection rules, and limitations. **Mark nothing as passed unless it was executed.**

### 3.4 Go / No-Go criteria
- **GO (full experience):** H2, H3 and H4 pass with real Explorer. H7 resolves a layout. H5 gives a false-positive overlay rate of ≤ 1 in 20 for the listed non-file drags in the default mode.
- **GO (degraded):** H7 fails but H6 passes → ship the registry + keyboard backend and show "Limited mode" in the UI.
- **GO with trigger change:** H5 accuracy is poor → the default trigger becomes **"Edge summon"** (the overlay appears only when a suspected drag reaches the screen-edge zone), and the overlay drop target confirms the file payload before any switch happens.
- **NO-GO:** H1/H2/H6 all fail (the drag never survives a switch). Then document the obstacle precisely, ship FluxKit with a **"Desktop Shelf"** fallback, and label it honestly: dropping files onto a desktop card parks the *paths* (never copies or moves files). After the user switches, they drag them out of a small shelf window, which starts a **new** drag. This is clearly described as not preserving the original drag. It is built only if needed.

### 3.5 Detection rules (initial; finalized from H5 data)
Candidate drag = all of the following:
- Left button down in a window whose root process is in the **source allow-list**. Default allow-list: `explorer.exe` (Explorer windows and the Desktop `Progman`/`WorkerW`). The "Any app" mode removes this check.
- The cursor moved ≥ `SM_CXDRAG`/`SM_CYDRAG` (scaled to DPI) while the button stays held.
- The foreground thread is **not** in move/size (`GUI_INMOVESIZE` clear) and is not in a menu mode.
- The capture window matches the OLE drag signature found in Phase 1. The working assumption is that `hwndCapture` changes to an OLE-owned window when `DoDragDrop` starts; the class names are recorded empirically, not guessed.
- Held ≥ 120 ms after crossing the threshold (debounce). Then the overlay shows.

Confirmation: if the cursor enters the overlay and the drop target sees StorageItems → `Confirmed`. If the drag ends with no confirmation in "Any app" mode, the overlay closes silently.

---

## 4. Solution Architecture

### 4.1 Repository layout
```
fluxkit/
├─ FluxKit.sln
├─ Directory.Build.props          # net10.0, Nullable, LangVersion latest, TreatWarningsAsErrors, deterministic, version
├─ Directory.Packages.props       # central package management (pinned versions)
├─ .editorconfig / .gitignore / .gitattributes / global.json (SDK 10.0.303, rollForward latestFeature)
├─ README.md  LICENSE (MIT)  CHANGELOG.md  CONTRIBUTING.md  SECURITY.md  THIRD-PARTY-NOTICES.md
├─ src/
│  ├─ FluxKit.App/                    # WinUI 3 exe – composition root
│  │  ├─ App.xaml(.cs)                # startup, single-instance, DI, lifecycle
│  │  ├─ Program.cs                   # custom Main: AppInstance redirection, DISABLE_XAML_GENERATED_MAIN
│  │  ├─ Lifecycle/  AppLifecycle.cs, SingleInstance.cs, CommandLine.cs (--background, --settings, --page)
│  │  ├─ Theming/    ThemeService.cs, BackdropHelper.cs
│  │  ├─ Shell/      MainWindow.xaml, ShellViewModel.cs, NavigationService.cs, TrayIconService.cs,
│  │  │              Pages/HomePage, GeneralPage, AboutPage (+ViewModels)
│  │  ├─ Resources/  Styles/*.xaml (tokens, card styles), Strings/en-US/Resources.resw
│  │  └─ Assets/     FluxKit.ico, Square44/150 logos, tray icons (light/dark/disabled)
│  ├─ FluxKit.Core/                   # net10.0 (no Windows dependency) – fully unit-testable
│  │  ├─ Utilities/  IFluxUtility.cs, UtilityDescriptor.cs, UtilityRegistry.cs, UtilityState.cs
│  │  ├─ Settings/   ISettingsService.cs, SettingsService.cs, SettingsDocument.cs, GlobalSettings.cs,
│  │  │              SettingsMigrator.cs, SettingsValidator.cs, IFileSystem.cs
│  │  ├─ Logging/    LoggingSetup.cs, LogLevelSwitch.cs, Redaction.cs
│  │  └─ Common/     Result.cs, Error.cs, Debouncer.cs (TimeProvider-based)
│  ├─ FluxKit.Platform/               # net10.0-windows10.0.22621 – Win32/COM only, no XAML
│  │  ├─ NativeMethods.txt            # CsWin32 inputs
│  │  ├─ Input/      LowLevelInputHook.cs (thread+pump), InputEvent.cs, ISystemInputSource.cs
│  │  ├─ VirtualDesktop/ IVirtualDesktopService.cs, VirtualDesktopInfo.cs, VdCapabilities.cs,
│  │  │              ShellComBackend.cs, RegistryKeyboardBackend.cs, UnavailableBackend.cs,
│  │  │              VirtualDesktopServiceFactory.cs, RegistryDesktopReader.cs, DesktopChangeWatcher.cs,
│  │  │              Interop/Builds/{Win11_22H2.cs, Win11_24H2.cs, ...}, Interop/ImmersiveShell.cs
│  │  ├─ Windows/    WindowStyles.cs, MonitorInfo.cs, DpiHelper.cs, WindowEnumerator.cs, ProcessInfo.cs
│  │  ├─ Shell/      StaWorker.cs (dedicated STA thread + work queue), ExplorerRestartWatcher.cs
│  │  ├─ System/     OsInfo.cs (build, edition), StartupRegistration.cs (HKCU Run), SystemSettings.cs
│  │  └─ Keyboard/   KeyboardSwitcher.cs (SendInput Ctrl+Win+Arrow)
│  └─ FluxKit.Utilities.DragToSwitch/ # WinUI class library – the utility module
│     ├─ DragToSwitchUtility.cs       # IFluxUtility implementation
│     ├─ Settings/   DragToSwitchSettings.cs (+ defaults, validation)
│     ├─ Detection/  DragDetectionService.cs, DragHeuristics.cs, SourceFilter.cs
│     ├─ Interaction/ DragInteractionCoordinator.cs, DragSessionStateMachine.cs, DragSession.cs,
│     │              HoverDwellTracker.cs, CardHitTester.cs, SwitchGuard.cs, Outcome.cs
│     ├─ Selector/   DesktopSelectorWindow.xaml(.cs), SelectorViewModel.cs, DesktopCardViewModel.cs,
│     │              DesktopCard.xaml (UserControl), SelectorLayout.cs (pure geometry), PreviewProvider.cs
│     └─ SettingsUI/ DragToSwitchPage.xaml, DragToSwitchViewModel.cs
├─ tests/
│  ├─ FluxKit.Core.Tests/
│  ├─ FluxKit.DragToSwitch.Tests/
│  └─ FluxKit.Platform.IntegrationTests/   # [Trait("Category","Integration")], skipped in CI
├─ tools/  FluxKit.DropProbe/, FluxKit.DragHarness/
├─ spikes/ DragSwitchSpike/, VdProbe/
├─ scripts/ build.ps1, test.ps1, publish.ps1, e2e.ps1
├─ Docs/ PLAN.md, Architecture.md, Setup.md, Feasibility.md, KnownLimitations.md, Configuration.md,
│        Troubleshooting.md, ManualTestPlan.md, Privacy.md, Performance.md, adr/ADR-00x-*.md, images/
└─ .github/workflows/ci.yml          # build + unit tests on windows-latest; publish artifacts on tag
```

Dependency direction: `App → Utilities.DragToSwitch → Platform → Core` and `App → Core`. Core references nothing Windows-specific. Utilities never reference App.

### 4.2 Process and threading model
```
┌──────────────────────── FluxKit.exe (medium IL, single instance) ─────────────────────────┐
│ UI thread (DispatcherQueue)   : MainWindow (created on demand), DesktopSelectorWindow (pre-created, hidden),
│                                 tray icon, ViewModels
│ FluxKit.InputHook thread      : WH_MOUSE_LL + WH_KEYBOARD_LL, GetMessage pump; callbacks only copy
│                                 the event into Channel<InputEvent> and return (< 1 ms). Only installed
│                                 while Drag-to-Switch is enabled.
│ FluxKit.Shell thread (STA)    : owns ImmersiveShell COM objects; serial work queue for enumerate/switch/pin;
│                                 re-inits on Explorer restart ("TaskbarCreated") or RPC_E_DISCONNECTED.
│ Coordinator loop (async Task) : reads input channel, runs state machine, uses TimeProvider timers,
│                                 marshals UI updates to DispatcherQueue.
│ Registry watcher (thread-pool wait) : RegNotifyChangeKeyValue → DesktopsChanged event (debounced 150 ms).
└────────────────────────────────────────────────────────────────────────────────────────────┘
```

### 4.3 Startup sequence
1. `Program.Main`: `AppInstance.FindOrRegisterForKey("FluxKit.Main")`. If another instance exists, redirect the activation (args such as `--settings`) and exit.
2. Initialize logging (bootstrap logger). Load settings; on corruption, quarantine and continue with defaults.
3. Build the DI container and register utilities through `UtilityRegistry`.
4. Create the tray icon. `VirtualDesktopServiceFactory.CreateAsync()` probes the backend on the STA thread with a 3 s timeout.
5. For each enabled utility: `StartAsync`. Drag-to-Switch pre-creates the hidden selector window and installs the hooks.
6. Show the MainWindow **only** if it's the first run or the app wasn't launched with `--background` (startup entries use `--background`).
7. Target: ready in < 1.5 s cold.

### 4.4 Shutdown sequence
Tray → Exit, or the OS session ends:
1. Coordinator cancels any active session (hides the overlay, takes no switch actions).
2. Utilities `StopAsync` in reverse order, 2 s timeout each. Hooks are unhooked on the hook thread, then the pump is quit.
3. The STA worker releases COM objects (`Marshal.FinalReleaseComObject`) and the thread exits.
4. Settings are flushed. The tray icon is disposed (no ghost icons). Logs are flushed (`Log.CloseAndFlush`).

---

## 5. Core Contracts

### 5.1 Utility model
```csharp
public interface IFluxUtility : IAsyncDisposable
{
    UtilityDescriptor Descriptor { get; }          // Id "drag-to-switch", DisplayName, Description, Glyph, Order, SettingsPageType
    UtilityState State { get; }                    // Disabled | Starting | Running | Degraded(reason) | Faulted(error)
    event EventHandler<UtilityState>? StateChanged;
    Task StartAsync(CancellationToken ct);
    Task StopAsync(CancellationToken ct);
}
public sealed class UtilityRegistry   // ordered, id-unique; Register(), All, Get(id), SetEnabledAsync(id,bool) persists + starts/stops
```
The shell's navigation menu, Home cards, and tray utility toggles are all generated from `UtilityRegistry.All`. Adding a utility means: a new project + one `services.AddFluxUtility<T>()` line.

### 5.2 Settings service
- `ISettingsService`:
  - `T Get<T>(string section)` where `T : class, new()`
  - `Task UpdateAsync<T>(string section, Func<T,T> mutate)`
  - `event SettingsChanged(section)`
- File format: one document with `schemaVersion`, a `global` section, and a `utilities.{id}` section per utility. Each utility owns and validates its own section.
- Writes are debounced at 300 ms. They go to `settings.json.tmp`, then `File.Replace` swaps it in and keeps a `.bak`.
- Load rules, in order:
  - File missing → defaults (first run).
  - Invalid JSON → move it to `settings.corrupt-{yyyyMMddHHmmss}.json`, use defaults, set the `RecoveredFromCorruption` flag (the UI shows an InfoBar).
  - Unknown fields → preserved on round-trip (`JsonExtensionData`).
  - Out-of-range values → clamped, with a warning logged.
  - Older `schemaVersion` → run the migrations in sequence.
  - Newer `schemaVersion` → read-only mode, plus a warning in the UI ("Settings were created by a newer FluxKit").

### 5.3 Logging
- Serilog with a rolling daily file: `%LOCALAPPDATA%\FluxKit\Logs\fluxkit-YYYYMMDD.log`. Keeps 7 files, 10 MB cap per file.
- Levels map to the user setting: **Errors only** (Warning+), **Normal** (Information+, default), **Verbose** (Debug+, auto-reverts to Normal after 24 h). A `LoggingLevelSwitch` applies changes live.
- What gets logged:
  - Startup: version, OS build, VD backend and layout, settings path.
  - Utility state changes.
  - Drag sessions: id, trigger reason, confirmed y/n, desktops shown, switch attempts (from → to, backend, latency ms, result/HRESULT), and the end reason.
  - Desktop list changes, hook installs/removals, COM re-init, and unhandled exceptions (`AppDomain`, `TaskScheduler.UnobservedTaskException`, and the WinUI `UnhandledException`).
- **Never logged:** file names, paths, contents, window titles. Only counts and process names (e.g. `source=explorer.exe files=3`). `Redaction.cs` enforces this.

---

## 6. Platform Layer Specs

### 6.1 `IVirtualDesktopService`
```csharp
public interface IVirtualDesktopService : IDisposable
{
    VdCapabilities Capabilities { get; }   // flags: Enumerate, Names, Wallpapers, SwitchDirect, SwitchByKeyboard, PinWindow, Notifications
    string BackendName { get; }            // "Shell COM (layout Win11-24H2)" | "Registry + keyboard" | "Unavailable"
    IReadOnlyList<VirtualDesktopInfo> Desktops { get; }   // snapshot: Id(Guid), Index, Name ("Desktop N" if unnamed), WallpaperPath?, IsCurrent
    Guid CurrentDesktopId { get; }
    event EventHandler<DesktopsChangedEventArgs>? DesktopsChanged;      // added/removed/renamed/reordered/current
    Task<SwitchResult> SwitchToAsync(Guid desktopId, CancellationToken ct);  // Success | NotFound | AlreadyCurrent | Failed(hr, message) | TimedOut
    Task<bool> PinWindowToAllDesktopsAsync(nint hwnd);
    Task RefreshAsync();
}
```
- Every call runs on the STA worker with a 1.5 s timeout. A failure never throws to callers; it returns a typed result.
- After a switch, the result is verified: re-read the current desktop. It must equal the target within 500 ms or the result is `Failed`.

### 6.2 Backend ladder (factory)
1. **ShellComBackend**:
   - Get the `IServiceProvider` from CLSID `ImmersiveShell` `{C2F03A33-21F5-47FA-B4BB-156362A2F239}`.
   - `QueryService` for `IVirtualDesktopManagerInternal` (service SID `{C5E0CDCA-7B6E-41B2-9FC4-D93975CC467B}`) using each **IID layout** in `Interop/Builds/` in order, preferring the one whose build range matches.
   - Validation: `GetCount` equals the registry count, the desktop GUID order equals the registry `VirtualDesktopIDs`, and the current desktop GUID equals the registry `CurrentVirtualDesktop`. Only a validated layout is accepted.
   - Pinning: `IVirtualDesktopPinnedApps` via `GetApplicationViewForHwnd` (`IApplicationViewCollection`).
2. **RegistryKeyboardBackend**:
   - Enumeration from `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops` (`VirtualDesktopIDs` = concatenated 16-byte GUIDs, `CurrentVirtualDesktop`, `Desktops\{GUID}\Name`, `Wallpaper`). If `CurrentVirtualDesktop` is missing there, read the per-session key under `...\SessionInfo\<id>\VirtualDesktops`.
   - Switching: compute `delta = targetIndex − currentIndex`, then `SendInput` `Ctrl+Win+(←|→)` |delta| times, 80 ms apart, releasing modifiers immediately each time, and verify via the registry.
   - Logs a warning that Ctrl is pressed briefly, which can flicker the drop-effect cursor. This is documented.
3. **UnavailableBackend**: the utility state becomes `Degraded("Virtual desktops could not be read on this Windows version")` and the overlay is never shown.

Known IID layouts are taken from the maintained MIT references (MScholtes/VirtualDesktop, Ciantic/VirtualDesktopAccessor), credited in `THIRD-PARTY-NOTICES.md`. They are **verified on this machine in Phase 1** (H7) and never trusted blindly. Expected starting point for 24H2+:
- `IVirtualDesktopManagerInternal` `{53F5CA0B-158F-4124-900C-057158060B27}`
- `IVirtualDesktop` `{3F07F4BE-B107-441A-AF0F-39D82529072C}`

If 26300 needs a new layout, it is derived and verified via VdProbe.

### 6.3 Other platform pieces
- **LowLevelInputHook**: installs/uninstalls both hooks on its thread. `InputEvent` = `{Kind (LDown, LUp, Move, KeyDown, KeyUp), Point (physical px), VK, Timestamp, Injected flag}`. Mouse moves are coalesced to ≤ 1 per 8 ms. Hook health: if Windows silently removes the hook (timeout), a 5 s watchdog re-installs it **only while idle** (not during a session). `LLMHF_INJECTED` events from FluxKit's own `SendInput` are ignored.
- **DesktopChangeWatcher**: `RegNotifyChangeKeyValue(REG_NOTIFY_CHANGE_LAST_SET | NAME, subtree)` + `RegisterWaitForSingleObject`, debounced 150 ms, raises `DesktopsChanged`.
- **ExplorerRestartWatcher**: listens for the `TaskbarCreated` registered message → re-probes the backend and re-pins the overlay.
- **StartupRegistration**: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\FluxKit = "<exe path>" --background`. `IsEnabled()` validates that the path still matches the current exe. A mismatch shows the "Startup entry points to an old location — Fix" action. Respects `StartupApproved\Run` (disabled in Task Manager) and reports it in the UI.
- **OsInfo**: build/UBR. If below 22000 (Windows 10) the app still runs, but the VD layout set is limited and "Windows 11 is recommended" is shown.
- **WindowEnumerator**: lists top-level visible, non-tool windows with `IVirtualDesktopManager.GetWindowDesktopId` (public API) to build per-desktop app icon strips.

---

## 7. Drag-to-Switch — Interaction Specification

### 7.1 Session state machine (`DragSessionStateMachine`, pure, deterministic, unit-tested)
```
Idle ──LDown(on allowed source)──▶ Armed
Armed ──Move ≥ threshold & !inMoveSize──▶ Suspected ──120 ms held──▶ Active (overlay shows)
Armed/Suspected ──LUp──▶ Idle (nothing shown)
Active ──cursor in card(X≠current)──▶ Dwelling(X) ──dwell elapsed & guard ok──▶ Switching(X)
Dwelling(X) ──cursor leaves X (beyond hysteresis + 120 ms grace)──▶ Active
Switching(X) ──SwitchResult.Success──▶ Switched(X) (overlay collapses to pill)
Switching(X) ──Failed/TimedOut──▶ Active + error toast in overlay ("Couldn't switch — try Ctrl+Win+→")
Switched ──cursor enters pill──▶ Active (expanded again; can switch again)
Any active ──LUp──▶ Ended(Released)   → Idle   (outcome unknown to FluxKit; target app handles drop)
Any active ──Esc──▶ Ended(Cancelled) → optional ReturnToOrigin → Idle
Any active ──utility disabled / app exit / session timeout 120 s / desktops became < 2──▶ Ended(Aborted) → Idle
```
- **One session at a time**, identified by `SessionId`. Events tagged with a stale id are ignored. The overlay window is a singleton.
- Re-entrancy: a new `LDown` while a session is active (this shouldn't happen) aborts the old session first.
- Overlay is never shown when there are fewer than 2 desktops. The setting `Show when only one desktop exists` (default off) shows a hint card "+ Create a desktop with Ctrl+Win+D" — it is informational only; FluxKit never creates desktops.

### 7.2 Hover/dwell rules (`HoverDwellTracker`, `CardHitTester`, `SwitchGuard`)
- Hit-testing uses card rects in physical pixels, published by the overlay after each layout pass (`SelectorLayout` is pure geometry and testable).
- **Hysteresis:** once a card is hovered, its hit rect is inflated by 8 px (DPI-scaled) so the cursor can wobble at the edge.
- **Grace:** leaving for < 120 ms doesn't reset the dwell.
- **Dwell delay:** setting `HoverDelayMs`, default **600**, range 200–1500, step 50. Dwell progress (0..1) is shown with a composition animation driven by the start time, not per-frame messages.
- **Current-desktop card:** never dwells. It shows "You're here".
- **Guard:**
  - After a switch, no further switch until the cursor has left all cards **and** 400 ms have passed.
  - At most 1 in-flight switch.
  - A target removed mid-dwell → dwell cancelled.
- **Desktop list changes during a session:** the cards are re-bound by GUID. If the hovered card is removed, the dwell is cancelled. A new card appears with an animation.

### 7.3 Keyboard (during an active session only; setting `NumberKeysEnabled`, default on — enabled only if H8 passes)
- `1`–`9`: switch immediately to desktop N, subject to the guard. The key is **swallowed** (the hook returns non-zero) only while the overlay is expanded.
- `Esc`: never swallowed. OLE uses it to cancel the drag, and FluxKit just observes it → `Ended(Cancelled)`.
- `Ctrl`/`Shift`/`Alt` are never swallowed, because they set the copy/move/link effect.
- Arrow keys are not used (they could conflict with the source app). This is documented.

### 7.4 After the switch
- The overlay collapses to a **pill** (keeping its edge position) that shows the destination desktop name + "Drop into any app". The pill fades to 70 % opacity after 1.5 s so it's out of the way. Hovering the pill re-expands it. Setting `CollapseAfterSwitch` (default on; off = hide completely).
- Optional `ReturnToOriginOnCancel` (default off): on `Esc` after a switch, switch back to the origin desktop.
- FluxKit **never** reports a delivery result. After a release, the session log says `Released` and nothing more. No UI says "delivered".

### 7.5 Edge cases (each has a defined behavior and test)
| Case | Behavior |
|---|---|
| Release over the overlay | Drop target reports `NONE`. Cursor shows no-drop. A header hint "Release outside the selector to drop" appears while the cursor is inside the overlay. |
| Source app elevated, FluxKit not | Hooks still observe the input, so switching works. UIPI rules for the target are Windows'. Documented. |
| Target app elevated | Windows blocks drops from medium IL. Documented in KnownLimitations. |
| Multi-monitor | Overlay appears on the monitor containing the cursor at session start, positioned within that monitor's work area. Per-Monitor V2 DPI. Moving to another monitor doesn't move the overlay (it's predictable). |
| Desktops 10+ | Cards scroll horizontally. Edge cards act as auto-scroll zones (dwell 400 ms → scroll by one card). Number keys cover 1–9. |
| Full-screen app / game | Overlay suppressed when `SHQueryUserNotificationState` returns `QUNS_RUNNING_D3D_FULL_SCREEN` or `QUNS_PRESENTATION_MODE`. |
| Explorer restarts mid-session | Session aborted, backend re-probed. |
| Lock / session switch (`WTS_SESSION_LOCK`) | Session aborted, hooks kept. |
| Tablet / touch / pen drags | Not supported in v1 (LL mouse hook sees promoted pointer input inconsistently). Documented. |
| Right-button drag | Not supported in v1. Documented. |
| Remote Desktop session | Works if VDs exist. Not tested in v1, documented as untested. |

---

## 8. UI / UX Specification

### 8.1 Design foundations
- **Typography:** Segoe UI Variable through the WinUI type ramp only: Caption 12, Body 14, BodyStrong 14 semibold, Subtitle 20, Title 28. No custom fonts.
- **Icons:** Segoe Fluent Icons (`FontIcon`). The app logo is a custom SVG→ICO mark: two offset rounded squares (desktops) with a curved arrow ("flux"), monochrome tray variants for light, dark and disabled.
- **Color:** system resources only (`AccentFillColorDefaultBrush`, `CardBackgroundFillColorDefaultBrush`, `CardStrokeColorDefaultBrush`, `TextFillColorSecondaryBrush`, …), so light, dark and **high contrast** work automatically. No hard-coded hex values except the logo.
- **Shape:** `OverlayCornerRadius` 8 for windows/flyouts/overlay container, `ControlCornerRadius` 4 for controls, cards 8. Spacing on a 4 px grid (4/8/12/16/24/32).
- **Materials:** settings window **Mica** (falls back to solid if Mica is unsupported or transparency is off). Overlay **Desktop Acrylic** (thin). Transparency respects `UISettings.AdvancedEffectsEnabled`; when off, solid `SolidBackgroundFillColorBase`.
- **Motion:** respects `UISettings.AnimationsEnabled` and the FluxKit `AnimationsEnabled` setting. Disabled → instant opacity only. Easing: Fluent decelerate `cubic-bezier(0,0,0,1)` for entrances and accelerate `(1,0,1,1)` for exits. Durations: entrance 167 ms, exit 120 ms, hover 83 ms, state changes 250 ms.
- **Theme:** app setting System/Light/Dark applies `RequestedTheme` to all windows and updates the title bar button colors and the tray icon variant live.

### 8.2 Desktop Selector overlay (expanded)
```
                         ┌──────────────────────────────────────────────────────────────────────┐
  Header (32 px)         │ ◈ FluxKit   Hold over a desktop to switch            1–9 · Esc cancel │
                         ├──────────────────────────────────────────────────────────────────────┤
                         │ ┌──────────────┐  ┌──────────────┐  ┌──────────────┐                 │
  Cards (≈140 px)        │ │ ▓▓ wallpaper │  │ ▓▓ wallpaper │  │ ▓▓ wallpaper │                 │
                         │ │  [◯][◯][◯]   │  │  [◯][◯]      │  │  [◯][◯][◯]   │                 │
                         │ │──────────────│  │══════════════│  │──────────────│                 │
                         │ │ 1  Comms     │  │ 2  Project A │  │ 3  Project B │                 │
                         │ │              │  │   ● Current  │  │  ▬▬▬▬▬▬░░░░  │ ← dwell progress│
                         │ └──────────────┘  └──────────────┘  └──────────────┘                 │
                         └──────────────────────────────────────────────────────────────────────┘
                                         16 px gap from taskbar/work-area edge
```
- **Window:** borderless, no title bar, non-resizable, `WS_EX_NOACTIVATE|TOOLWINDOW|TOPMOST`, excluded from Alt+Tab and the taskbar, pinned to all desktops. DWM rounded corners (`DWMWA_WINDOW_CORNER_PREFERENCE=ROUND`), system shadow, 1 px `CardStrokeColorDefault` border. Padding 12. Max width = 90 % of the monitor work area.
- **Header:**
  - Left: 16 px logo + "FluxKit" (Caption, secondary).
  - Center/left: instruction text (Body). It changes with state: "Hold over a desktop to switch" → "Switching to *Project B*…" → (error) "Couldn't switch. Try Ctrl+Win+→". When the cursor is over the overlay itself: "Release outside the selector to drop".
  - Right: hint "1–9 · Esc to cancel" (Caption, tertiary). Hidden when number keys are disabled.
- **Desktop card** (Standard size 176×132; Compact size 136×100):
  - Preview area 16:9 (160×90). Content depends on the `PreviewStyle` setting:
    - **Wallpaper** (default): the desktop's wallpaper from the registry, decoded at 160 px width on a background thread, LRU cached by path+mtime; falls back to the current wallpaper.
    - **App icons:** up to 4 app icons (24 px) of top-level windows on that desktop over a subtle `SubtleFillColorSecondary` fill, with "+N" if more.
    - **Wallpaper + icons:** icons overlaid bottom-left on a translucent strip.
    - **None:** no preview area; cards are 64 px tall.
  - Label row: number badge ("1", Caption, in a 18 px rounded rectangle) + desktop name (BodyStrong, single line, ellipsis, full name in tooltip and automation name).
  - Card states:
    | State | Visual |
    |---|---|
    | Rest | `CardBackgroundFillColorDefault`, 1 px `CardStrokeColorDefault` |
    | Current | 2 px accent bar under preview + "● Current" caption; not dwell-able (subtle hover only) |
    | Hover/Dwelling | Scale 1.00→1.04 (83 ms), border 2 px `AccentFillColorDefault`, **dwell progress bar** (3 px, accent) fills left→right over exactly `HoverDelayMs` |
    | Switching | Progress bar becomes indeterminate shimmer, label "Switching…" |
    | Switched | Checkmark glyph (accent), label "Now on *Name*", then overlay collapses |
    | Error | Border `SystemFillColorCritical`, error glyph, tooltip with reason; returns to rest after 2 s |
    | High contrast | Uses `SystemColorHighlightColor` for border/progress; previews hidden behind solid fill for legibility |
- **Pill (collapsed)** 220×40: logo + "On *Project B* — drop into any app" + chevron-up glyph. Same material. Hover re-expands (250 ms size animation from the pill's anchor).
- **Positions** (setting `OverlayPosition`): `BottomCenter` (default), `TopCenter`, `NearCursor` (centered horizontally on the cursor, 48 px above it, clamped to the work area, flips below if there's no room). The anchor is decided once per session.
- **Animations:**
  - Entrance: opacity 0→1 + translateY 12→0 px (bottom) or −12→0 (top), 167 ms decelerate. Cards stagger 20 ms each (max 8).
  - Exit: opacity 1→0 + translateY 0→8, 120 ms accelerate, then `SW_HIDE`.
  - Collapse to pill: container size animation 250 ms + crossfade content.
  - All animations are Composition `ImplicitAnimations`/`KeyFrameAnimation` on the visual layer; none on the UI-thread layout.
- **Accessibility:**
  - Cards: `AutomationProperties.Name` = "Desktop 2, Project A, current desktop" / "Desktop 3, Project B. Hold to switch".
  - On switch: `RaiseNotificationEvent("Switched to desktop Project B")` for Narrator.
  - Text contrast ≥ 4.5:1 is guaranteed by the theme brushes. Respects text scaling (cards grow in height, names stay readable).
- **Performance:** window pre-created hidden. Show = set data + `SetWindowPos(SWP_NOACTIVATE|SWP_SHOWWINDOW)` + animation, target **< 50 ms** from the trigger to the first frame. Previews are loaded asynchronously, with a placeholder shimmer if not ready.

### 8.3 Settings window (MainWindow)
- Size 1040×700 by default, min 760×520. Position and size remembered. Mica. Custom title bar (`ExtendsContentIntoTitleBar`, 48 px tall) with the 16 px logo + "FluxKit" and a "Preview" badge only for pre-release builds. Close (X) **hides to tray** (the first time, a one-time teaching tip says "FluxKit is still running in the system tray"). The window is fully disposed after close to free memory and recreated on demand.
- **NavigationView** (Left mode, auto-collapses to LeftCompact < 1000 px, LeftMinimal < 640 px):
  ```
  ┌──────────────────────────────────────────────────────────────────────────────┐
  │ ◈ FluxKit                                                     ─   ☐   ✕      │
  ├────────────────────┬─────────────────────────────────────────────────────────┤
  │ ⌂  Home            │  Home                                                   │
  │ ── Utilities ───── │                                                         │
  │ ⇄  Drag-to-Switch ●│  [content]                                              │
  │                    │                                                         │
  │ ⚙  General         │                                                         │
  │ ⓘ  About           │                                                         │
  └────────────────────┴─────────────────────────────────────────────────────────┘
  ```
  The menu items under "Utilities" come from `UtilityRegistry`. A small status dot follows each name: green = running, amber = degraded, none = off. The dot also has an automation name. General and About are footer items.

#### 8.3.1 Home page
```
 Home
 Welcome to FluxKit
 Small, focused tools that make Windows feel faster.       (Body, secondary)

 ┌─ InfoBar (only if something needs attention) ─────────────────────────────────┐
 │ ⚠ Drag-to-Switch is running in limited mode. Desktop switching uses keyboard   │
 │   shortcuts on this Windows build.                      [Learn more] [Dismiss] │
 └────────────────────────────────────────────────────────────────────────────────┘

 Utilities
 ┌──────────────────────────────────────────┐
 │ ⇄  Drag-to-Switch                [ On ●] │
 │ Drag files across virtual desktops       │
 │ without keyboard shortcuts.              │
 │ Status: Ready · 3 desktops detected      │
 │                          [Open settings] │
 └──────────────────────────────────────────┘
 (Grid of UtilityCard controls, 320 px wide, wraps; only real utilities)

 Quick start
 1. Start dragging a file in File Explorer.
 2. Hold it over a desktop in the selector that appears.
 3. Release it in any app on that desktop.
```

#### 8.3.2 Drag-to-Switch page
Built with `SettingsCard` / `SettingsExpander` (PowerToys look). Header area:
```
 Drag-to-Switch
 ┌──────────────────────────────────────────────────────────────────────────────┐
 │ [illustration 320×120: file → selector → desktop]                            │
 │ Drag a file, hold it over a desktop card, and drop it into an app there.    │
 │ The original drag is kept. FluxKit never copies or moves your files.         │
 │                                                     [Learn more ↗ (Docs)]    │
 └──────────────────────────────────────────────────────────────────────────────┘
 ┌ ⇄ Enable Drag-to-Switch ─────────────────────────────────────────── [ On ] ┐
 └────────────────────────────────────────────────────────────────────────────┘
 Status InfoBar: ✔ Ready — Shell integration (Windows 11 24H2 layout), 3 desktops
              / ⚠ Limited mode — keyboard switching (reason)   / ✖ Unavailable — reason + [View log]
```
The sections below are disabled (greyed) when the utility is off.

**Activation**
| Control | Type | Values / default | Notes |
|---|---|---|---|
| Show the selector | ComboBox | **When a file drag starts** / When the dragged file reaches the screen edge | The default may change after H5, per 3.4. |
| Hover delay | Slider + value label "600 ms" | 200–1500, step 50, **600** | Description: "How long to hold over a desktop before switching." A live mini dwell-bar animation next to it previews the timing. |
| Drag sources | ComboBox | **File Explorer and desktop** / Any app | Any app → "The selector may appear for non-file drags." |
| Number keys switch desktops while dragging | ToggleSwitch | **On** | Hidden/disabled with explanation if H8 failed. |
| Show when only one desktop exists | ToggleSwitch | Off | Shows a hint card. |

**Selector appearance**
| Control | Type | Values / default |
|---|---|---|
| Position | ComboBox with mini-diagram icons | **Bottom center** / Top center / Near cursor |
| Size | RadioButtons (horizontal) | Compact / **Standard** |
| Desktop previews | ComboBox | **Wallpaper** / App icons / Wallpaper + app icons / None |
| Theme | ComboBox | **Use FluxKit theme** / System / Light / Dark |
| Animations | ToggleSwitch | **On** (follows Windows "Animation effects"; description notes when Windows has them off) |
| Preview the selector | Button "Show preview" | Shows the real overlay for 4 s at the configured position with sample state (mouse-driven dwell demo; no switching occurs). |

**After switching**
| Control | Type | Default |
|---|---|---|
| Collapse selector to a small pill | ToggleSwitch | **On** |
| Return to the original desktop if the drag is cancelled (Esc) | ToggleSwitch | Off |

**Troubleshooting** (SettingsExpander, collapsed by default)
- Backend: "Shell integration (layout Win11-24H2)", read-only text + [Re-detect] button.
- Desktops detected: "3 — Comms, Project A, Project B" + [Refresh].
- Last session: "Today 14:02 — switched Desktop 3 → Desktop 1 in 41 ms, released" (no file names).
- [Open log folder] [Copy diagnostic info] buttons.

#### 8.3.3 General page
| Group | Control | Default | Notes |
|---|---|---|---|
| Startup | Launch FluxKit when I sign in (ToggleSwitch) | **Off** (asked once in first-run) | Description shows exactly what's done: "Adds FluxKit to your Windows startup apps (HKCU\…\Run). You can also manage this in Settings › Apps › Startup." Shows a warning if disabled by Task Manager. |
| Startup | Open settings window on launch | Off | Applies to manual launches only; background launch never opens it. |
| Appearance | App theme | **System** | Light / Dark / System; live. |
| System tray | Show tray icon | On (locked on in v1) | Explanation: required to reach FluxKit while in background. |
| Diagnostics | Logging level (ComboBox) | **Normal** | Errors only / Normal / Verbose (auto-reverts after 24 h). |
| Diagnostics | Open log folder / Open settings file | buttons | |
| Reset | Reset all settings | button → ContentDialog | "Reset all FluxKit settings? Utilities will restart with default settings." [Reset] [Cancel] |

#### 8.3.4 About page
- Logo 64 px, "FluxKit", version `1.0.0 (build 1.0.0+sha)`, channel, "© 2026 FluxKit contributors". Then:
- Rows:
  - Windows version (e.g. "Windows 11 26H2, build 26300.xxxx")
  - Virtual desktop backend
  - Settings location
  - Log location
- Buttons:
  - [Copy diagnostic info] (puts redacted diagnostic text on the clipboard)
  - [Open documentation] (local `Docs` shipped in the zip)
  - [Third-party notices]
- No update checker (no network in v1).

#### 8.3.5 First-run experience
A one-time ContentDialog on the first launch:
- Title: "Welcome to FluxKit"
- Body: three-step Drag-to-Switch illustration
- Option: "Launch FluxKit when I sign in" checkbox, unchecked
- Buttons: [Get started] [Open settings]

Recorded in settings as `global.firstRunCompleted`.

### 8.4 System tray
- Icon: the logo in mono variants by taskbar theme. The disabled variant has a slash when Drag-to-Switch is off.
- Tooltip: "FluxKit — Drag-to-Switch on" / "… off" / "… limited mode".
- Left click → opens/focuses the settings window. Double-click is the same.
- Right-click context menu (native style via H.NotifyIcon `ContextMenuMode.PopupMenu`):
  ```
  Open FluxKit
  ─────────────
  ✓ Drag-to-Switch            (toggle)
  ─────────────
  Settings…                   (opens Drag-to-Switch page)
  Open log folder
  ─────────────
  Exit FluxKit
  ```
  The utility toggles are generated from the registry.

### 8.5 Error and notice surfaces (no modal popups during drags, ever)
- In the overlay: inline header message and card error state.
- In settings: InfoBars at the top of the relevant page (severity Informational/Success/Warning/Error, with actions).
- Background failures (e.g. the backend becomes unavailable while the window is closed): the tray icon tooltip + a badge, plus one Windows app notification (`AppNotificationManager`, unpackaged-supported) **at most once per app run**, with "Open FluxKit".

### 8.6 Localization
All user strings go in `Strings/en-US/Resources.resw` via `x:Uid` and the `ResourceLoader`. No concatenated sentences; format strings are used. RTL layouts work through `FlowDirection` inheritance. Only en-US ships in v1.

---

## 9. Settings Schema (`settings.json`)
```jsonc
{
  "schemaVersion": 1,
  "global": {
    "theme": "System",                 // System | Light | Dark
    "launchAtStartup": false,
    "openSettingsOnLaunch": false,
    "logLevel": "Normal",              // ErrorsOnly | Normal | Verbose
    "verboseUntilUtc": null,           // ISO-8601 UTC, e.g. "2026-10-27T09:00:00Z"
    "firstRunCompleted": false,
    "mainWindow": { "x": null, "y": null, "width": 1040, "height": 700, "maximized": false }
  },
  "utilities": {
    "drag-to-switch": {
      "enabled": true,
      "trigger": "OnDragStart",        // OnDragStart | EdgeSummon
      "hoverDelayMs": 600,             // 200..1500
      "sources": "ExplorerAndDesktop", // ExplorerAndDesktop | AnyApp
      "numberKeysEnabled": true,
      "showWithSingleDesktop": false,
      "overlayPosition": "BottomCenter", // BottomCenter | TopCenter | NearCursor
      "overlaySize": "Standard",         // Compact | Standard
      "previewStyle": "Wallpaper",       // Wallpaper | AppIcons | WallpaperAndIcons | None
      "overlayTheme": "FollowApp",       // FollowApp | System | Light | Dark
      "animationsEnabled": true,
      "collapseAfterSwitch": true,
      "returnToOriginOnCancel": false
    }
  }
}
```
(Comments here are for documentation only. The real file is plain JSON.)

Validation: enums are parsed case-insensitively, and unknown values fall back to the default. Numbers are clamped. Invalid types use the default for that field only, not the whole section. All of this is covered by tests.

---

## 10. Reliability, Performance, Security

### 10.1 Budgets (measured and recorded in `Docs/Performance.md`)
| Metric | Target |
|---|---|
| Idle CPU (Drag-to-Switch on, no drag) | ~0 % (event-driven; only the 5 s hook watchdog runs while idle) |
| Idle working set (settings window closed) | ≤ 90 MB private (WinUI baseline); measure and document |
| Hook callback time | p99 < 0.5 ms |
| Trigger → overlay first frame | < 50 ms (pre-created window) |
| Dwell elapsed → desktop switched (COM backend) | < 150 ms |
| Cold start to ready | < 1.5 s |

### 10.2 Robustness rules
- All `IDisposable`/COM/hook handles are owned by one component, released deterministically, and covered by tests where possible (fake hooks).
- Each utility's work is wrapped in a fault boundary. An exception sets the utility to `Faulted` with a "Restart utility" action, and the app stays alive.
- No `async void` except XAML event handlers, which are wrapped in try/catch + log.
- Timeouts on every cross-process call (COM 1.5 s, switch verify 500 ms).
- The global unhandled exception handler logs, writes `crash-*.txt` (redacted), and shows an error dialog on the next launch.

### 10.3 Security and privacy
- Runs as the user at medium IL. No admin rights, no manifest elevation, no network, no injection.
- LL hooks only observe events and never alter them, with **one** exception: digits 1–9 are swallowed while the selector is expanded and number keys are on. The hook is uninstalled when the utility is disabled.
- No file contents, names or window titles are logged. `Docs/Privacy.md` states exactly what's stored (settings + redacted logs, locally).
- Release binaries are code-signed if a certificate is available. Otherwise the README explains SmartScreen.

---

## 11. Testing Strategy

### 11.1 Automated unit tests (xUnit; `dotnet test` with `FakeTimeProvider`)
**FluxKit.Core.Tests**
- Settings:
  - Defaults when the file is missing.
  - Round-trip save/load.
  - Unknown fields preserved.
  - Invalid JSON is quarantined and defaults are used.
  - Out-of-range values clamped.
  - Invalid enums fall back to defaults.
  - Wrong types handled per field.
  - v0→v1 migration.
  - Newer schema → read-only.
  - The atomic write leaves a `.bak`.
  - Debounced writes coalesce.
  - Concurrent `UpdateAsync` calls are serialized.
- UtilityRegistry:
  - Register/order.
  - Duplicate ids rejected.
  - Enable/disable persists and calls Start/Stop exactly once.
  - Start failure → Faulted.
  - Disposal order is reversed.
- Redaction: paths and titles are stripped from messages.
- Debouncer behavior.

**FluxKit.DragToSwitch.Tests**
- State machine:
  - Every transition in 7.1.
  - LUp before threshold → no overlay.
  - Move-size → no session.
  - Disallowed source → no session.
  - Esc → Cancelled.
  - Disable mid-session → Aborted.
  - Timeout → Aborted.
  - Duplicate/stale events ignored.
  - Only one session.
- HoverDwellTracker:
  - The switch fires exactly at the delay.
  - Wobble inside the hysteresis doesn't reset.
  - A < 120 ms exit keeps the dwell.
  - A ≥ 120 ms exit resets.
  - The current-desktop card never fires.
  - The delay change is applied at the next dwell.
- SwitchGuard: cooldown 400 ms, must-leave-cards rule, single in-flight.
- CardHitTester / SelectorLayout:
  - Geometry for 2, 3, 9 and 12 desktops.
  - DPI 100/150/200 %.
  - Each position.
  - Clamping to the work area.
  - Flip for NearCursor.
- Coordinator (fakes for `ISystemInputSource`, `IVirtualDesktopService`, `ISelectorView`):
  - Switch success → collapse.
  - Switch failure → error state and back to Active, no false "success".
  - Desktops change mid-dwell (hovered card removed) → cancel.
  - < 2 desktops → no overlay.
  - Number key → switch + swallow flag only while expanded.
  - ReturnToOrigin on Esc.
  - Repeated sessions leave no leaked subscriptions (event handler counts are asserted).
- Settings section validation for Drag-to-Switch.

**FluxKit.Platform.IntegrationTests** (run locally; `[Trait("Category","Integration")]`)
- Registry desktop reader parses real data.
- COM backend probe on this machine.
- `StartupRegistration` writes and removes the HKCU Run value under a test name.
- Hook install/uninstall leaves no leaks (1000 cycles).

### 11.2 Automated E2E (local, `scripts/e2e.ps1`)
Uses `FluxKit.DragHarness` with real OLE `DoDragDrop`, real FluxKit, real desktop switching and real DropProbe. Scenarios:
- E2E-1: 2 desktops
- E2E-2: 3 desktops, target index 1 from 3
- E2E-3: cancel via Esc → probe receives nothing
- E2E-4: release over the overlay → nothing received, source file still exists
- E2E-5: keyboard backend forced (`FLUXKIT_VD_BACKEND=keyboard`)

The script creates temporary desktops only if the user agrees (prompt), restores the original desktop afterwards, and writes `artifacts/e2e-results.json`.

### 11.3 Manual integration test plan (`Docs/ManualTestPlan.md`)
Each case has steps, expected result, and columns **Executed (Y/N) / Result / Date / Notes**. They are filled in honestly; un-run cases stay "Not run".
| ID | Scenario |
|---|---|
| MT-01 | Explorer file → DropProbe across 2 desktops |
| MT-02 | Explorer file → app on desktop 1 of 3 (from desktop 3), then 3→2 |
| MT-03 | Drop into File Explorer (copy) and into Chrome/Edge (e.g. Gmail attachment) and Notepad |
| MT-04 | Cancel with Esc before switch; after switch (with/without Return-to-origin) |
| MT-05 | Switch desktops, then release on empty taskbar area / no target (no file op) |
| MT-06 | Disable via tray during normal use; mid-drag |
| MT-07 | Create, remove, rename, reorder desktops while FluxKit runs; and mid-drag |
| MT-08 | Dark, light, high-contrast; transparency off; animations off |
| MT-09 | Start from Run/startup entry; exit from tray; no ghost tray icon |
| MT-10 | Restart FluxKit; all settings restored; corrupt settings file recovery |
| MT-11 | Multi-monitor + mixed DPI (if hardware is available, otherwise marked N/A) |
| MT-12 | Non-file drags (text selection, window move, rubber band) → no overlay in default mode |
| MT-13 | Number keys 1–9 during a drag; Explorer type-ahead unaffected |
| MT-14 | Explorer restart (`taskkill /f /im explorer.exe` then start) → recovery |
| MT-15 | Desktop icon drag source (Desktop → app on other desktop) |

---

## 12. Build, CI, Versioning, Packaging
- `global.json` pins SDK 10.0.303. Central package versions. `TreatWarningsAsErrors`. Analyzers: `AnalysisLevel=latest-recommended`.
- Platforms: **x64** and **ARM64** (WinUI needs an RID; there's no AnyCPU exe).
- `scripts/build.ps1` [-Configuration Release] → `dotnet build`.
- `scripts/test.ps1` → unit tests + `--filter Category!=Integration`, plus a coverage report (coverlet). Target ≥ 80 % line coverage for Core and DragToSwitch logic.
- `scripts/publish.ps1` → `dotnet publish src/FluxKit.App -c Release -r win-x64 --self-contained -p:WindowsAppSDKSelfContained=true -p:PublishReadyToRun=true` → zips `FluxKit-<ver>-win-x64.zip` with README, Docs, THIRD-PARTY-NOTICES. Same for arm64.
- Versioning: SemVer in `Directory.Build.props`, `InformationalVersion` includes the git sha. `CHANGELOG.md` follows Keep a Changelog.
- `.github/workflows/ci.yml`: windows-latest, restore/build/unit tests on push/PR. On a `v*` tag, publish zips as artifacts. (No remote is configured. The workflow is ready but not run.)
- Git: `git init`, `main` branch. Conventional commits per milestone. No pushes unless asked.

---

## 13. Documentation Deliverables
- `README.md`: what it is, a GIF/screenshot, requirements (Win11, x64/ARM64), install (unzip, run `FluxKit.exe`), build from source, usage, settings summary, limitations summary, privacy summary.
- `Docs/Setup.md`: SDK/VS requirements, build/run/test/publish commands, debugging tips (attach to `FluxKit.exe`; the `--background` flag).
- `Docs/Architecture.md`: module diagram, threading model, the state machine, how to add a utility (step-by-step with a code sample).
- `Docs/Feasibility.md`: Phase 1 results (only executed results).
- `Docs/KnownLimitations.md`: undocumented COM API and version sensitivity, keyboard fallback side effects, elevated targets, touch/pen/right-drag unsupported, full-screen suppression, no previews of live desktop contents (Windows exposes no API for that), detection heuristics, untested environments.
- `Docs/Configuration.md`: every setting, file locations, env overrides (`FLUXKIT_VD_BACKEND`, `FLUXKIT_LOG_LEVEL`).
- `Docs/Troubleshooting.md`: selector doesn't appear, switch fails, limited mode, reading logs, resetting settings, removing the startup entry.
- `Docs/Privacy.md`, `Docs/ManualTestPlan.md`, `Docs/Performance.md`, `Docs/adr/*`, `THIRD-PARTY-NOTICES.md`.

---

## 14. Milestones and Execution Order
| Milestone | Work | Exit criteria |
|---|---|---|
| **M0 Bootstrap** | Save this plan as `Docs/PLAN.md`; git init; solution skeleton, Directory.*.props, editorconfig, empty projects build | `dotnet build` green |
| **M1 Feasibility** | DropProbe, DragHarness, VdProbe, DragSwitchSpike; run H1–H8; write Feasibility.md; Go/No-Go decision | Recorded evidence for each hypothesis; decision documented |
| **M2 Core + Platform** | Settings, logging, registry, StaWorker, VD backends + factory, hooks, watchers, startup registration; unit + integration tests | Tests green; VdProbe-equivalent works through the service |
| **M3 Drag-to-Switch logic** | State machine, dwell, guard, hit-testing, coordinator with fakes; detection rules from M1 | Full unit suite green |
| **M4 UI** | Shell window, navigation, Home/General/About, tray, theming, first run; selector overlay + pill + animations + previews; settings page bound to the VM | App launches; manual smoke test; screenshots in Docs |
| **M5 Validation** | E2E harness scenarios; manual test plan executed where possible; perf measurements; fix issues | E2E-1..5 results recorded; MT table filled honestly |
| **M6 Docs & Package** | README + Docs; publish zips x64/arm64; verify the zip runs from a clean folder | Zip launches, Drag-to-Switch verified from the packaged build |

Agent-execution note: everything except physically operating the mouse with real Explorer can be done autonomously. The harness covers real OLE drags via `SendInput`. Where a real-Explorer human run is needed, exact steps are handed to the user and their observed results are recorded. Nothing is claimed that wasn't observed.

---

## 15. Risk Register
| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Build 26300 changed the internal VD IIDs | Medium | High | Runtime probing + validation; keyboard fallback; VdProbe to derive new layouts |
| Drag doesn't survive a programmatic switch | Low–Med | Critical | Phase 1 gate; keyboard-path variant; honest Shelf fallback |
| Drag detection false positives | Medium | Medium | Explorer-only default, debounce, OLE capture signature, Edge-summon mode, drop-target confirmation |
| LL hook silently removed by Windows (timeout) | Low | Medium | Minimal callback + dedicated thread; watchdog re-install while idle |
| Overlay steals focus/capture | Low | High | NOACTIVATE styles, `SW_SHOWNOACTIVATE`, verified in H3 |
| Accidental file move via overlay drop | Low | Critical | Drop target always `NONE` (ADR-004) + E2E-4 test |
| WinUI memory footprint | High | Low | Dispose the settings window when closed; measure; document |
| Explorer restart / COM disconnect | Medium | Medium | `TaskbarCreated` + `RPC_E_DISCONNECTED` re-init |
| Antivirus flags global hooks | Low | Medium | Signed binaries; documented; no injection |

---

## 16. Assumptions
- English (en-US) only for v1. MIT license. Product/company name "FluxKit contributors".
- Minimum OS: Windows 11 22H2 (22621). Windows 10 is "may work, unsupported".
- Use the user's existing virtual desktops. FluxKit never creates, deletes or renames desktops, except temporary ones in the E2E script and only with consent.
- Chrome/Edge and Notepad are present as second drop targets. If not, any installed app that accepts file drops is used and named in the test record.

---

## 17. Verification / Definition of Done
1. `scripts/build.ps1` and `scripts/test.ps1` pass with 0 warnings. The unit test count and coverage are reported from real output.
2. `FluxKit.exe` launches: the tray icon appears, the settings window renders in light/dark, and settings persist across a restart (MT-09, MT-10).
3. **E2E-1/2 pass:** a real OLE file drag, a switch via the FluxKit overlay, and DropProbe on another desktop logs the exact file path.
4. **A real File Explorer** drag across desktops into DropProbe and one other app (MT-01..03) is executed, with the results recorded. If anything couldn't be run, it is listed as "Not run", with the reason.
5. E2E-3/4 confirm that cancel and release-over-overlay cause no delivery and no source file changes.
6. Feasibility.md and KnownLimitations.md describe the actual backend used on build 26300 and any fallback in effect.
7. The published zip runs from a clean folder and the core workflow is verified there.
