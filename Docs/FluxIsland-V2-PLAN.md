# Flux Island v2: the plan

Flux Island v1 shows music, timers, the shelf, volume and a handful of system moments. v2 turns it into the one place on a Windows laptop where everything that is *happening right now* shows up, and where the most common quick actions are one click away. Everything stays local, event-driven, admin-free and light.

## 1. What others do (research, October 2026)

| Product | What it does well | What we take from it |
|---|---|---|
| **iPhone Dynamic Island** (2022 → iPhone 17, iOS 26/27) | Live Activities: timers, music, calls, rides, sports, boarding passes, workouts. Two activities at once: one in the pill and one in a small bubble beside it. Long-press to expand. iOS 27 lets you swipe between up to three activities. | The **split pill** (a second bubble), **swipe between activities**, and activities that carry **progress**. |
| **MacBook Pro Dynamic Island** (announced 28 Oct 2026) | Replaces the notch. Calls, AirDrop requests, delivery tracking, music, Spotlight and Siri live in it. Tap on the touchscreen. | The island as a **launcher** (search and actions), not only a status display. |
| **Mac notch apps** (NotchNook, Alcove, Droppy, Notchy, Boring Notch) | File tray, clipboard history, replacement HUDs for volume and brightness, AirPods and device battery, download progress ring, meeting countdowns, Pomodoro, system stats, quick notes, screenshots, a quiet black idle state that springs open on hover. | **HUD replacement**, **device batteries**, **downloads**, **screenshots**, **clipboard smarts**, **stats**, **quick actions**. Users now expect all of these in one place. |
| **Android 16 Live Updates / Samsung Now Bar** | Progress-centric live notifications (rides, deliveries, navigation) in a status chip. | One consistent **progress** look for anything long-running. |
| **Windows island apps** (DynamicWin, Dynamic Edge) | Media, file tray, clipboard, timers, a stopwatch. | Baseline. We already match it; v2 goes further. |

## 2. What a Windows laptop can feed the island (feasibility)

Rules carried over from FluxKit: no admin rights, no code injection, no drivers, no network, never log names or content, and events only (polling is allowed only while a page that shows live numbers is open).

| Signal | API | Feasible | Notes |
|---|---|---|---|
| Volume and mute changed by **any** means (keys, other apps) | `IAudioEndpointVolume::RegisterControlChangeNotify` | Yes | A real HUD replacement for the volume keys. |
| **Microphone mute** for the whole PC | Capture endpoint `IAudioEndpointVolume::SetMute` | Yes | One click mutes the mic in every app, including calls. |
| **Output device switched** (headphones, speakers, Bluetooth) | `IMMNotificationClient::OnDefaultDeviceChanged` + the device's friendly name | Yes | "Now playing on WH-1000XM5". |
| **Virtual desktop switched** | FluxKit's own `IVirtualDesktopService.DesktopsChanged` | Yes | Synergy with Drag-to-Switch: name and position dots. |
| **Downloads** in progress and finished | `FileSystemWatcher` on the Downloads folder (`.crdownload`, `.part`, `.partial`, `.download`, then the rename) | Yes | Size grows, so we show a live size; real percent isn't available from the file system. |
| **Screenshots** | `FileSystemWatcher` on `Pictures\Screenshots` (Win+PrtScn, Snipping Tool autosave) | Yes | Shows a thumbnail you can drag out, open or put on the shelf. |
| **Copied content** (opt-in) | Clipboard text, read only when the setting is on | Yes | Link → Open; colour → swatch; email or phone → actions; a short preview. Off by default. |
| **Battery saver**, time to full / to empty | `PowerManager.EnergySaverStatusChanged`, `Battery.AggregateBattery.GetReport()` | Yes | Time estimates show in the timer page's battery corner. |
| **Brightness** changes | WMI `WmiMonitorBrightnessEvent` (root\wmi, no admin) | Yes, on laptop panels | External monitors don't report it. |
| **Caps Lock / Num Lock** | Raw input (`RegisterRawInputDevices`, `RIDEV_INPUTSINK`), only the lock keys are looked at | Yes | No hook, no injection. |
| **Bluetooth device** connected / disconnected | `DeviceWatcher` with `System.Devices.Aep.IsConnected` | Yes | Battery level only where Windows exposes it; otherwise just the name. |
| **System glance** (CPU, memory, disk, network speed) | Performance counters / `GetSystemTimes`, `GlobalMemoryStatusEx`, `GetIfTable2` | Yes | Only sampled while the Glance page is open (once a second). |
| Quick actions (screenshot, notes, lock, sleep, settings) | `ms-screenclip:`, `ms-settings:` URIs, `LockWorkStation` | Yes | |
| Keyboard access to the island | `RegisterHotKey` (Ctrl+Alt+I by default) | Yes | Opens the panel with keyboard focus; arrows switch pages; Esc closes. |
| **Other apps' notifications** | `UserNotificationListener` | **Not now** | Needs package identity and the user's consent. FluxKit is unpackaged. Revisit when FluxKit ships an MSIX. |
| **Calendar / next meeting** | `AppointmentStore` | **No** | Needs package identity and an appointments capability; Outlook has no local API we can use without injection. |
| **Phone calls** (Phone Link) | — | **No** | No public API. The mic dot already shows calls in desktop apps. |
| **AirDrop-like sharing** | Windows Share sheet (`IDataTransferManagerInterop`) | Partly | We can open the share sheet (Nearby Share lives there) for shelf files. |
| **Weather, deliveries, sports** | Web APIs | **No** | FluxKit makes no network connections, by promise. |
| **Do Not Disturb state** | — | **No** | No public API to read or change it. |
| **Screen recording indicator** | — | **No** | No public API. |

## 3. Experience design

### 3.1 The pill grows a second bubble
When two lasting things happen at once (say music and a timer), the main pill shows the more important one and a **small round bubble** sits beside it, separated by a soft gap, like iPhone. The bubble shows the other activity's glyph (or art) with a ring for progress. Clicking the bubble swaps them with a spring. Hovering it shows its own peek.

### 3.2 Every moment is actionable
Alerts are no longer only text. Each kind can carry one primary action, shown as a soft chip on the right in peek and as a button in the panel:

| Moment | Action |
|---|---|
| Download finished | Open · Show in folder · Keep on shelf |
| Screenshot taken | Open · Copy · Keep on shelf (and drag the thumbnail out) |
| Link copied | Open |
| Colour copied | (swatch preview) |
| Mic in use | Mute mic |
| Output device switched | — (shows the name) |
| Low battery | Battery saver |

Right-click (or middle-click) on the pill dismisses the current alert. Scrolling sideways (tilt wheel or shift-scroll) moves between activities.

### 3.3 HUD replacement
Volume (any source), mic mute, brightness and Caps Lock show as compact level/toggle moments, in the same springy style as the scroll-volume bar. Windows still shows its own flyout. We don't try to hide it, because that needs hooks we won't install.

### 3.4 New pages in the panel
The rail gains **Glance** (live system numbers and quick actions) after Music, Timer and Shelf. Pages are only listed when their feature is on.

- **Timer page:** adds a **stopwatch** (with laps), and **+1 min** while a countdown runs. Battery corner shows "Full in 45 min" / "3 h 10 min left".
- **Shelf page:** tiles get a small menu (open, show in folder, copy path, share). Optional **keep the shelf after restart** (paths saved in `%LOCALAPPDATA%\FluxKit\shelf.json`, never logged).
- **Glance page:** four soft rings (CPU, memory, disk, battery) and network ↑↓, plus a row of quick actions: Screenshot, Lock, Sleep display, Settings. Sampled once a second only while visible.

### 3.5 Keyboard and access
Ctrl+Alt+I (changeable, or off) opens the panel and focuses it. Tab and arrows move, Enter activates, Esc closes. Every element has an accessible name (already true in v1).

### 3.6 Feel
- Same black surface, same spring constants, same 40 ms stagger.
- Progress uses one look everywhere: a thin ring around the glyph in compact, a bar in the panel.
- New moments get small, specific flourishes: the screenshot thumbnail drops in with a slight tilt; a download's ring fills; Caps Lock's glyph flips; mic mute shows a slash that draws itself.

## 4. Architecture changes

- `ActivityKind` grows: `Download`, `Screenshot`, `DesktopSwitched`, `AudioDevice`, `MicMuted`, `Brightness`, `CapsLock`, `NumLock`, `BatterySaver`, `Device` (Bluetooth), `Stopwatch`.
- `IslandActivity` grows a small payload: `Text` (a name to show: file, device, desktop), `Path` (for file moments), `Progress` (0–1 or null), `Flag` (on/off). Names and paths are shown on screen only and never logged.
- New sources (each optional, each behind a setting, each `IIslandSource`): `AudioEventsSource` (volume callback, default device, capture mute), `DesktopSource` (adapter over FluxKit's desktop service), `DownloadsSource`, `ScreenshotSource`, `BrightnessSource`, `LockKeysSource`, `BluetoothSource`, `GlanceSampler` (pull, page-driven).
- New commands: `ToggleMicMute`, `OpenPath`, `RevealPath`, `CopyPath`, `ShareFiles`, `DismissAlert`, `SwapActivities`, `StartStopwatch`, `LapStopwatch`, `ResetStopwatch`, `AddMinute`, `QuickAction(kind)`.
- The arbiter keeps working as is; new kinds get priorities and durations. Download in progress is *lasting* (with progress); finished is an alert.
- Settings: one toggle per new feature, plus `hotkey`, `keepShelf`, `smartCopy` (off by default), `islandMonitor` (main / where the pointer is).

## 5. Phases (each ends with format, build, all tests, screenshots of new states, and one single-line commit)

1. **Payload and split pill.** New activity fields, the second bubble, swap on click, dismiss on right-click, sideways scroll between activities. Tests for the arbiter with the new kinds.
2. **Audio.** Volume/mute from any source (HUD), global mic mute (button in the mic-dot peek and in Glance), output device switched.
3. **Desktops.** Desktop switched moment with name and dots.
4. **Downloads and screenshots.** Watchers, live/finished moments, thumbnail, actions (open, show in folder, keep on shelf, drag out).
5. **Smart copy** (opt-in). Link/colour/email/phone detection with tests, preview and actions.
6. **Power and brightness.** Battery saver moment, time to full/empty, brightness HUD.
7. **Shelf+.** Tile menu, share sheet, keep-after-restart.
8. **Timer+.** Stopwatch with laps, +1 min.
9. **Glance page.** Live rings and quick actions.
10. **Keyboard.** Hotkey, focus, arrows, Esc. **Lock keys** HUD.
11. **Bluetooth devices.** Connected/disconnected, battery where available.
12. **Settings page and docs.** Every new toggle, preview samples for new moments, README, Configuration, Privacy, KnownLimitations, ManualTestPlan, CHANGELOG.

## 6. Testing

- Unit tests: arbiter priorities for new kinds, smart-copy detection, download name tracking (temp → final), stopwatch maths, shelf persistence round trip, hotkey parsing.
- Screenshots via `--preview-island <sample>` for every new state.
- Manual-only (recorded as not run until a person runs them): real volume keys, mic mute in a call, headphones switch, a browser download, Win+PrtScn, brightness keys, Caps Lock, a Bluetooth headset, the hotkey.
