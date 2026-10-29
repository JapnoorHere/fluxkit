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
| `enabled` | bool | `true` | Utility on or off. Off removes the input hooks. |
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
