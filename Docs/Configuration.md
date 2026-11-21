# Configuration

All settings can be changed in the FluxKit window. This page documents the file behind it.

## Locations

| What | Where |
|---|---|
| Settings | `%LOCALAPPDATA%\FluxKit\settings.json` (plus `settings.json.bak` from the last save) |
| Logs | `%LOCALAPPDATA%\FluxKit\Logs\fluxkit-YYYYMMDD.log` (7 days, 10 MB per file) |
| Startup entry | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\FluxKit` = `"<path>\FluxKit.exe" --background` |

## File format

```json
{
  "schemaVersion": 1,
  "global": {
    "theme": "System",
    "launchAtStartup": false,
    "openSettingsOnLaunch": false,
    "logLevel": "Normal",
    "verboseUntilUtc": null,
    "firstRunCompleted": true,
    "mainWindow": { "x": null, "y": null, "width": 1040, "height": 700, "maximized": false }
  },
  "utilities": {
    "drag-to-switch": {
      "enabled": true,
      "trigger": "OnDragStart",
      "hoverDelayMs": 600,
      "sources": "ExplorerAndDesktop",
      "moveWindowsEnabled": true,
      "numberKeysEnabled": true,
      "showWithSingleDesktop": false,
      "overlayPosition": "BottomCenter",
      "overlaySize": "Standard",
      "previewStyle": "Wallpaper",
      "overlayTheme": "FollowApp",
      "animationsEnabled": true,
      "collapseAfterSwitch": true,
      "returnToOriginOnCancel": false
    }
  }
}
```

## Drag-to-Switch settings

| Key | Values | Default | Meaning |
|---|---|---|---|
| `enabled` | bool | `true` | Tool on or off. Off removes the input hooks. |
| `trigger` | `OnDragStart`, `EdgeSummon` | `OnDragStart` | Show the selector as soon as a drag starts, or only when the file reaches the screen edge. |
| `hoverDelayMs` | 200–1500 | 600 | Hold time over a card before switching. Values outside the range are clamped. |
| `sources` | `ExplorerAndDesktop`, `AnyApp` | `ExplorerAndDesktop` | Which apps a drag must start in. |
| `moveWindowsEnabled` | bool | `true` | Dragging an app window by its title bar shows the selector and takes the window to the chosen desktop. Needs the shell integration backend; ignored in limited (keyboard) mode. |
| `numberKeysEnabled` | bool | `true` | 1–9 switch desktops while the selector is open. |
| `showWithSingleDesktop` | bool | `false` | Show a "create a desktop" hint when only one exists. |
| `overlayPosition` | `BottomCenter`, `TopCenter`, `NearCursor` | `BottomCenter` | |
| `overlaySize` | `Compact`, `Standard` | `Standard` | Cards are 136×100 or 176×132 effective pixels. |
| `previewStyle` | `Wallpaper`, `None` | `Wallpaper` | |
| `overlayTheme` | `FollowApp`, `System`, `Light`, `Dark` | `FollowApp` | |
| `animationsEnabled` | bool | `true` | Also off whenever Windows animation effects are off. |
| `collapseAfterSwitch` | bool | `true` | Shrink to a pill after switching, instead of hiding. |
| `returnToOriginOnCancel` | bool | `false` | Esc after a switch takes you back to the starting desktop. |

## Flux Island settings

Section `utilities/flux-island`.

| Key | Values | Default | Meaning |
|---|---|---|---|
| `enabled` | bool | `true` | Tool on or off. Off closes the island and stops every watcher. |
| `showMedia` | bool | `true` | What is playing, from the Windows media controls (GSMTC). |
| `showPower` | bool | `true` | Plug-in and unplug moments, and a warning when the battery falls to 20 %. |
| `showNetwork` | bool | `true` | "Offline" after 3 s without internet, "Online" when it comes back. |
| `showPrivacy` | bool | `true` | Orange (mic) and green (camera) dots while an app uses them. |
| `showClipboard` | bool | `true` | A "Copied" moment. Only the kind (text, image, files) is checked; the content is never read. |
| `smartCopy` | bool | `false` | Reads copied text once to spot a link, email address, phone number or colour and offer an action. Never kept or logged. |
| `showVolumeChanges` | bool | `true` | Show the volume whenever it changes, from the keys or anywhere else. |
| `showAudioDevices` | bool | `true` | Name the device sound moved to. |
| `showMicMute` | bool | `true` | Microphone mute changes, and a reminder while an app listens to a muted mic. |
| `showDesktopSwitches` | bool | `true` | Name the virtual desktop you switch to. |
| `showDownloads` | bool | `true` | Downloads arriving and finished, from the Downloads folder. |
| `showScreenshots` | bool | `true` | New screenshots in Pictures\Screenshots, with a preview. |
| `showBrightness` | bool | `true` | Brightness changes, on PCs with a built-in screen. |
| `showBatterySaver` | bool | `true` | Battery saver turning on or off. |
| `showLockKeys` | bool | `true` | Caps Lock and Num Lock turning on or off. |
| `showDevices` | bool | `true` | Paired Bluetooth devices connecting and disconnecting. |
| `shelfEnabled` | bool | `true` | Drop files on the island to keep them (paths only, at most 12). |
| `keepShelf` | bool | `true` | Keep the shelf after a restart. Paths are saved in `%LOCALAPPDATA%\FluxKit\shelf.json`; turning this off deletes the file. |
| `scrollForVolume` | bool | `true` | The mouse wheel over the island changes the master volume in steps of 2 %. |
| `showGlance` | bool | `true` | The Glance page in the panel. Its numbers are read once a second, only while it's on screen. |
| `hotkey` | text | `Ctrl+Alt+I` | Opens the panel from the keyboard. Ctrl, Alt or Win (plus Shift if you like) with a letter, digit, F1–F24 or Space; an F-key can be used alone. Empty turns it off; anything unreadable goes back to the default. |
| `monitor` | `Main`, `FollowMouse` | `Main` | The screen the island sits on. `FollowMouse` moves it to the screen with the pointer when something new shows. |
| `keepNotes` | bool | `true` | Keep notes from the command bar in `%LOCALAPPDATA%\FluxKit\notes.json`. Turning it off deletes the file. |
| `showCalls` | bool | `true` | Show a call while a call app or browser uses the microphone, with its length, a mute button and a nudge when you talk while muted. |
| `clipboardHistory` | bool | `false` | Keep the last 25 pieces of copied text (memory only) for the Clipboard page. Turning it off forgets them, pins too. |
| `interruptions` | `Everything`, `Important`, `Nothing` | `Everything` | Which quick moments may take over the island. Important: time's up, low battery, offline, talking while muted and your own file jobs. |
| `sounds` | bool | `false` | Play Windows' reminder sound when a timer ends, and the default notification sound for low battery or talking while muted. |
| `look` | `Onyx`, `Glass`, `Mercury` | `Onyx` | True black, the labs smoked-glass look, or labs liquid metal with a highlight that follows the pointer. |
| `soundRipples` | bool | `true` | The liquid edge: the underside ripples like ferrofluid with the music, and when the pointer touches it (ADR-015). Off with reduced motion and high contrast. |
| `lightHalo` | bool | `false` | Labs: the island's colour spills onto the desktop under it like a lamp. A separate click-through window; hidden with the island. |
| `droplets` | list | `[]` | Activities torn off the island into floating droplets (`kind`: `Media`, `Timer` or `Stopwatch`; `x`, `y`: screen pixels). Managed by FluxKit; a droplet docks itself when its activity ends. |
| `liveActivities` | bool | `false` | Labs: scripts, terminals, builds and AI agents on this PC can show live progress on the island with `FluxKit.exe island …` (ADR-016). Display only. |
| `showNotifications` | bool | `false` | Show Windows notifications on the island (ADR-014). Turn it on from Settings, which registers FluxKit's identity and asks Windows' permission first. |
| `notificationPrivacy` | `Full`, `SenderOnly`, `Hidden` | `Full` | How much of a notification shows: sender and message, sender only, or the app name only. |
| `hideNotificationsWhilePresenting` | bool | `true` | Show only the app name while a full-screen app is on the main screen (needs `hideInFullScreen`'s full-screen watcher, which is on by default). |
| `notificationApps` | object | `{}` | Per app, keyed by its AppUserModelID: `name`, `show` (on the island), `hideBanner` (turn off Windows' banner while shown here) and `priority` (lets it through when `interruptions` is `Important`). Apps are added as their first notification arrives. |
| `tourSeen` | bool | `false` | Set once the first-run tour has played. Set it to `false` to see the tour again (or use Settings › Try it). |
| `seenHints` | flags | `None` | One-time gesture tips already shown: `Bubble`, `Dismiss`, `Volume`, `Pages`. |
| `hideInFullScreen` | bool | `true` | Hide while a full-screen app covers the main screen. |
| `showClockWhenIdle` | bool | `true` | The time on the idle pill. Off leaves a small empty pill. |
| `effects` | `Full`, `Light` | `Full` | Springs, glow and playing bars, or short fades only. Light is also used whenever Windows animations are off. |
| `focusMinutes` | 5–90 | 25 | Focus round length. Clamped. |
| `breakMinutes` | 1–30 | 5 | Break length. Clamped. |

## Robustness rules

- A missing file means defaults (first run).
- Invalid JSON: the file is renamed `settings.corrupt-<timestamp>.json`, defaults are used, and the Home page shows a notice.
- A field with a wrong type or unknown enum value falls back to its default **for that field only**.
- Unknown fields are preserved when FluxKit saves.
- A file from a newer FluxKit (higher `schemaVersion`) is opened **read-only**, so it isn't downgraded.
- Saves are debounced by 300 ms and written atomically (temp file, then replace).

## Environment variables

| Variable | Effect |
|---|---|
| `FLUXKIT_VD_BACKEND=keyboard` | Force the keyboard (Limited mode) backend. |
