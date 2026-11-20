# Manual and end-to-end test plan

**Rule:** a case is marked passed only if it was actually executed and observed. Everything else stays **Not run**, with a reason.

Environment for recorded runs: Windows 11 Home Single Language 26H2, build 26300.9711, x64, 4 virtual desktops, single monitor.

## Automated end-to-end (`scripts/e2e.ps1`, real OLE drag via DragHarness)

| ID | Scenario | Executed | Result | Date | Notes |
|---|---|---|---|---|---|
| E2E-0a | Drag survives switch by another process (`--switch com-direct`) | Y | PASS | 2026-10-26 | Feasibility H2: exact path received on Desktop 2, source intact |
| E2E-0b | Drag survives injected `Ctrl+Win+→` (`--switch manual-keys`) | Y | PASS | 2026-10-26 | Feasibility H6 |
| E2E-1 | Through the FluxKit selector, desktop 1 → 2 (`--switch fluxkit --scenario drop`) | N | Not run | — | Workstation was locked when the selector build was ready. Input injection doesn't reach a locked session. |
| E2E-2 | Through FluxKit, 3+ desktops, non-adjacent target (`--target 3`) | N | Not run | — | Same as above |
| E2E-3 | Esc cancel: nothing delivered (`--scenario cancel`) | N | Not run | — | Same as above |
| E2E-4 | Release over the selector: nothing delivered, source intact (`--scenario release-on-overlay`) | N | Not run | — | Same as above |
| E2E-6 | Window move: drag the harness window by its title bar through the selector (`--scenario window`) | Y | Inconclusive | 2026-10-26 | Run collided with a user drag at the same moment (log: session aborted by "unexpected button press"); not counted. Re-run needed. |
| E2E-5 | Keyboard backend (`FLUXKIT_VD_BACKEND=keyboard`, then E2E-1) | N | Not run | — | Same as above |

## Manual cases

| ID | Scenario | Steps | Expected | Executed | Result | Date | Notes |
|---|---|---|---|---|---|---|---|
| MT-01 | Explorer file → DropProbe across 2 desktops | Run `FluxKit.DropProbe --desktop 2`. In Explorer on desktop 1, drag a file down to the selector, hold over card 2, release on DropProbe | DropProbe lists the exact path. Selector collapses to a pill. | N | Not run | — | Needs a human-driven Explorer drag |
| MT-02 | Desktop 3 → app on desktop 1, then 3 → 2 | As MT-01 with 3 desktops | Correct desktop each time | N | Not run | — | |
| MT-03 | Drop into Explorer, Chrome/Edge, Notepad | Drag a .txt file to each target on another desktop | Explorer copies, browser opens or attaches, Notepad opens the file | N | Not run | — | Feasibility recorded one harness drag delivered to Chrome |
| MT-04 | Esc before and after switching (Return-to-origin on and off) | Press Esc during each phase | Drag cancelled. With the option on, you return to the origin desktop. | N | Not run | — | |
| MT-05 | Release on empty taskbar or no target | Switch, release on the taskbar | No file operation. Selector hides. | N | Not run | — | |
| MT-06 | Disable via tray, idle and mid-drag | Tray › uncheck Drag-to-Switch | Selector hides immediately. Hooks removed (log). | N | Not run | — | |
| MT-07 | Add, remove, rename, reorder desktops while running and mid-drag | Use Task View | Cards update by id. A removed hovered card cancels the dwell. | N | Not run | — | |
| MT-08 | Dark, light, high contrast; transparency off; animations off | Change Windows settings | Readable, no hard-coded colors, no motion when off | N | Not run | — | |
| MT-09 | Start via the startup entry, exit from the tray | Enable startup, sign out and in, then Exit | Starts in the tray only. No ghost tray icon after exit. | N | Not run | — | |
| MT-10 | Settings persist; corrupt-file recovery | Change settings, restart. Then write `{` into settings.json and start. | Values restored. Notice shown, `.corrupt-*` file kept. | N | Not run | — | Covered by unit tests; app-level run pending |
| MT-11 | Multi-monitor, mixed DPI | | Selector on the cursor's monitor at the correct size | N | N/A | — | Single-monitor machine |
| MT-12 | Non-file drags (text select, window move, rubber band) | Perform each 20 times | No selector in the default mode | N | Not run | — | Feasibility H5 |
| MT-13 | Number keys during a drag | Press 2 during an Explorer drag | Switches. Explorer type-ahead isn't triggered. | N | Not run | — | Feasibility H8 |
| MT-14 | Explorer restart | `taskkill /f /im explorer.exe`, then `start explorer` | FluxKit re-probes. The next drag works. | N | Not run | — | |
| MT-15 | Desktop icon as drag source | Drag a desktop icon to an app on another desktop | Same as MT-01 | N | Not run | — | |

### Flux Island

| ID | Scenario | Steps | Expected | Executed | Result | Date | Notes |
|---|---|---|---|---|---|---|---|
| FI-01 | Music | Play a song in Spotify, the Media Player or a browser | Pill shows art, title and bars. Click opens controls; play/pause, skip and seek work. | N | Not run | — | Rendered with `--preview-island music` |
| FI-02 | Hover and click | Hover the pill, then click, then move away | Peek after a moment, panel on click, closes ~0.6 s after leaving | N | Not run | — | |
| FI-03 | Click-through | Click just beside and below the pill | The app below gets the click | N | Not run | — | |
| FI-04 | Charger | Plug in, then unplug, a laptop | Ring sweeps to the level, then "On battery" | N | Not run | — | |
| FI-05 | Offline | Turn Wi-Fi off for 5 s, then on | "Offline", then "Online" | N | Not run | — | |
| FI-06 | Mic and camera | Join a call or open the Camera app | Orange or green dot while in use, gone after | N | Not run | — | |
| FI-07 | Copy | Copy text, an image and a file | "Copied" with Text, Image or Files | N | Not run | — | |
| FI-08 | Timer | Open the panel › Timer › 1 min; pause, resume, let it finish | Rolling digits, progress, bell with burst and bounce at the end | N | Not run | — | Rendered with `--preview-island timer-open` and `done` |
| FI-09 | Shelf drop | Drag two files from Explorer onto the pill | Shelf opens with a glowing drop zone; tiles appear after the drop | N | Not run | — | Rendered with `--preview-island drop` and `shelf-open` |
| FI-10 | Shelf drag out | Drag a tile into an Explorer folder and into an email | A copy (or link) lands there; the original stays | N | Not run | — | |
| FI-11 | Volume | Scroll over the pill | Level bar moves in 2 % steps; Windows volume follows | N | Not run | — | Interop checked read-only |
| FI-12 | Full screen | Start a full-screen game or F11 a browser video | Island hides; comes back after | N | Not run | — | |
| FI-13 | Reduced motion | Windows › Accessibility › Animation effects off | Island uses short fades only | N | Not run | — | |
| FI-14 | Desktops and Explorer restart | Switch desktops; restart Explorer | Island stays on every desktop | N | Not run | — | |
| FI-15 | Stopwatch | Panel › Timer › stopwatch; lap twice, pause, reset | Digits count up; "Lap 3" with the two laps listed; reset returns to quick timers | N | Not run | — | Rendered with `--preview-island stopwatch-open` |
| FI-16 | Timer and stopwatch together | Start the stopwatch, then a 5 min timer; use the switch button | Timer shows first; the switch flips to the stopwatch and back; the stopwatch sits in the bubble | N | Not run | — | Rendered with `--preview-island both-open` |
| FI-17 | +1 min | Start a 1 min timer; press +1 min | Time left and the bar grow by a minute | N | Not run | — | |
| FI-18 | Shortcut | Press Ctrl+Alt+I in another app; Tab, arrows, Enter, Esc | Panel opens with a focused button; keys move and press; Esc closes and focus goes back to the app | N | Not run | — | |
| FI-19 | Shortcut setting | Settings › shortcut: clear it, type "Shift+I", type "Ctrl+Alt+K" | Off; refused with a hint; then Ctrl+Alt+K opens the panel | N | Not run | — | Parsing unit-tested |
| FI-20 | Caps Lock and Num Lock | Press Caps Lock twice, then Num Lock | "Caps Lock on", "Caps Lock off", Num Lock state | N | Not run | — | Rendered with `--preview-island caps` and `numlock` |
| FI-21 | Bluetooth | Connect, then disconnect, paired earbuds | Device name with "Connected", then "Disconnected"; one moment each | N | Not run | — | Rendered with `--preview-island device` and `disconnected` |
| FI-22 | Glance | Open the panel › Glance; start a big download | Rings move once a second; ↓ speed rises; nothing sampled after closing (check CPU) | N | Not run | — | Live numbers checked with `--preview-island glance` |
| FI-23 | Glance actions | Press screenshot, lock, screen off, mic, settings | Snipping bar; PC locks; screens go dark; mic mutes; settings open on Flux Island | N | Not run | — | |
| FI-24 | Follow the mouse | Two screens; Screen › Where the mouse is; copy something with the pointer on each screen | The island moves to the pointer's screen | N | Not run | — | |
| FI-25 | Command bar | Ctrl+Alt+I, then type 5m, = 12*8, #3a7bff, note test, lock (Esc to leave) | Live preview for each; Enter runs it (sums and colours copy and stay open); lock locks | N | Not run | — | Parser unit-tested; rendered with `--preview-island command` |
| FI-26 | Call mode | Join a Teams or browser call; mute; talk for a second | Call time and Mute button; "You're muted" at most every 30 s | N | Not run | — | Rendered with `--preview-island call`; nudge depends on the driver |
| FI-27 | Live music bars | Play music with quiet and loud parts | Bars move with the sound; stop when paused or hidden | N | Not run | — | |
| FI-28 | Drop zones | Drag two images from Explorer onto the island; drop on Zip, then on Convert and Half size | Zones light up under the pointer; new files appear next to the originals and on the shelf | N | Not run | — | Zip and naming unit-tested; rendered with `--preview-island drop` |
| FI-29 | Recent and clipboard | Take a screenshot, copy a link; turn on clipboard history and copy text | Recent lists them with actions; Clipboard lists text with pin and remove | N | Not run | — | Rendered with `rewind` and `clips` samples |
| FI-30 | Tour and tips | First run (or Settings › Try it › Take the tour); later use the bubble, an alert, the wheel, the pages | Tour plays once; each tip shows once and never again | N | Not run | — | Tour checked with `--island-tour` |
| FI-31 | Interruptions and sounds | Set Important only, copy text, let a timer end; turn on sounds | Copy shows nothing; timer done shows and plays the reminder sound | N | Not run | — | Rules unit-tested |
| FI-32 | Glass and high contrast | Settings › Labs › Glass; then Windows high contrast on | Smoked glass with a bright rim; in high contrast a white outline and no glow | N | Not run | — | Rendered with `--preview-island glass` |
| FI-33 | Narrator | Narrator on; trigger a timer end and a copy | Narrator reads each moment as it arrives | N | Not run | — | |
| FI-34 | UI continuity | Play music, hover then click the pill; close it | The art grows from the pill into the panel and back | N | Not run | — | Rendered with `music-open` |
| FI-35 | Scrubber | Hover the music scrubber, then drag it | Track thickens with a knob; a time bubble follows; release seeks | N | Not run | — | |
| FI-36 | List details | Hover a note, remove it, clear the list, press Undo | Actions fade in; the row shrinks away; Undo brings the list back | N | Not run | — | Undo unit-tested |
| FI-37 | Page ends | On the first or last page, swipe further | The page leans and springs back | N | Not run | — | |
| FI-38 | Notification setup | Settings › Flux Island › Notifications: turn on | A dialog explains; after "Set up and restart" FluxKit restarts, Windows allows access, the switch stays on | N | Not run | — | Spike 2026-11-16: identity active, access Allowed |
| FI-39 | Notification on the island | Send a toast (WhatsApp, Teams, or the PowerShell test toast in ADR-014) | The island shows the app logo and sender; hover shows the message and Open; no Windows banner appears | Y | PASS (dev) | 2026-11-16 | PowerShell test toast shown; ShowBanner set to 0 |
| FI-40 | Banner restore | Turn notifications off, or close FluxKit, or kill it and start again | Each app's ShowBanner is back exactly as before; `banners.json` is gone | Y | PASS (kill and restart) | 2026-11-16 | |
| FI-41 | Privacy | Set "App name only"; send a toast; also start a full-screen slideshow | Only the app name shows, on the pill and on the Notifications page | N | Not run | — | |
| FI-42 | Notifications page | Open the island on Notifications; open, quiet, dismiss and clear with Undo | Rows grouped by app; Dismiss also removes it from Notification Center | N | Not run | — | |
| FI-43 | Priority | Set "What can interrupt" to Important; mark one app Priority | Only that app's notifications take over the island; others go to the page | N | Not run | — | |

## Smoke checks executed during development

| Check | Executed | Result | Date | Notes |
|---|---|---|---|---|
| App launches; COM backend resolves; hooks install; "FluxKit ready" logged | Y | PASS | 2026-10-26 | Log: `Virtual desktop backend: ShellCom (layout Win11-24H2), 4 desktops`. The first launch found a hook-pump P/Invoke bug (`GetMessage` entry point), which was fixed. |
| Main window opens (process reports window title "FluxKit") | Y | PASS | 2026-10-26 | Visual inspection not done (session locked) |
| 69 unit tests | Y | PASS | 2026-10-26 | `dotnet test` |
| Move another process's window to another desktop and back (`VdProbe move-window`) | Y | PASS | 2026-10-26 | DropProbe window moved 3 → 1 → 3; public `IVirtualDesktopManager.GetWindowDesktopId` confirmed both moves. No drag involved. |
| User-driven title-bar drags of Windows Terminal through the selector | Y | PASS (log evidence) | 2026-10-26 | Log: two window sessions, switches 4→2 and 3→4→3→2→3, released on desktop 3, no move failures; screenshot showed the window still being dragged on the new desktop. Final window placement not independently verified. |
| OLE drag signature: mouse capture during `DoDragDrop` | Y | PASS | 2026-10-28 | Scratch probe ran a real WinForms `DoDragDrop` and sampled `GetGUIThreadInfo` 15 times: capture was always `CLIPBRDWNDCLASS` in the drag's process. Text-selection and scrollbar drags not yet re-checked by hand after the fix (MT-12). |
| Selector, Home and Drag-to-Switch pages visually checked | Y | PASS | 2026-10-26 | Found and fixed: card bottoms clipped, Drag-to-Switch page crashed on open. General/About pages, tray menu and pill not yet checked. |
| Published zip (`FluxKit-1.0.0-preview.1-win-x64.zip`) runs from a clean folder | Y | PASS | 2026-10-26 | Unzipped to an empty folder. COM backend resolved, hooks installed, "ready" logged ~280 ms after start, 103 MB private memory. The first attempt failed with `XamlParseException` because `FluxKit.pri` wasn't published; fixed with `EnableMsixTooling=true`. Drag workflow from the packaged build not yet exercised (session locked). |
