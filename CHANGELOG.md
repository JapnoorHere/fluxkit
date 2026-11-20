# Changelog

All notable changes to FluxKit are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Changed
- New FluxKit design. A slim side rail replaces the old navigation, Home is a launchpad with a tile per tool, and settings sit in grouped cards under a brand-coloured page header.
- The Drag-to-Switch page shows a live preview of the picker that follows your settings (position, size, previews, theme, hold time and animations).
- All app text, including the picker and tray menu, now comes from one resource file, ready for translation.
- The code says "tool" everywhere, the same as the app. Saved settings keep their old keys, so nothing is lost (ADR-013).

### Added
- **Windows notifications on Flux Island** (opt-in, ADR-014). Messages, mail and reminders from your apps show on the island with the app's logo and colour, in place of Windows' corner banner:
  - A burst from one app becomes one moment ("3 new").
  - A Notifications page lists today's notifications grouped by app. Click one to open the app, quiet an app for an hour, dismiss one (it leaves Notification Center too), or clear all with Undo.
  - Privacy: show the sender and message, the sender only, or the app name only. Messages hide automatically during a full-screen presentation or video.
  - Per-app settings: Show on island, Hide Windows banner and Priority (lets it through when only important moments may interrupt).
  - Windows' banners are turned off only for apps the island shows, and put back exactly when you turn this off, close FluxKit, or after a crash.
  - Setup registers a small per-user identity package (no admin rights), restarts FluxKit once and asks Windows' permission. `scripts/make-identity.ps1` registers, packs and signs it.
- **Flux Island**, a new tool: a live pill at the top of the screen. It shows what is playing (with controls, a seek bar and an album glow), timers and a focus cycle, charger and battery moments, going offline and back online, mic and camera dots, "Copied" moments, a file shelf you drop files on and drag them out of, and scroll-to-change volume. It hides when an app is full screen. It has its own settings page with a live preview.
- Flux Island v2:
  - A second bubble beside the pill when two things happen at once (click to swap); right-click or middle-click waves a moment away.
  - Volume changes from anywhere, the sound device you switch to, and microphone mute, with a reminder while a call listens to a muted mic.
  - The name of the virtual desktop you switch to.
  - Downloads while they arrive and when they're done, and new screenshots with a preview.
  - Opt-in smart copy for links, email addresses, phone numbers and colours. Private copies are skipped.
  - Brightness changes, battery saver, and the time left or until full.
  - A shelf menu (open, show in folder, copy path, share), a share button, and keeping the shelf after restart.
  - A stopwatch with laps, +1 min on a running timer, and a switch between the two when both run.
  - Caps Lock and Num Lock moments, and Bluetooth devices connecting and disconnecting.
  - A Glance page with live CPU, memory, disk and battery, network speed, and quick actions (screenshot, lock, screen off, mic, settings).
  - A keyboard shortcut (Ctrl+Alt+I by default) that opens the panel; Tab, arrows, Enter and Esc work inside it.
  - A setting to put the island on the screen where the mouse is.
- Flux Island v3, a redesign:
  - A new look: true black with an edge light, a glow in each moment's own colour, rounder panel corners, and a labs Glass look.
  - New motion: content scales and fades as it changes, the second bubble buds off the pill like a drop of liquid, each moment arrives with its own flourish, and the pill leans toward the pointer.
  - Timers, the stopwatch and the battery show in a progress ring on the compact pill.
  - The panel has page dots and sideways swipes instead of a side rail. Music has bigger art and a scrubber that thickens on hover; timers get a dial; laps show the best and slowest.
  - Island Command: the shortcut opens a bar for timers, sums, colours, notes and quick actions, with a live preview, plus a Notes page.
  - Music bars follow the real sound. Call mode shows the call time and a mute button, and nudges you when you talk while muted.
  - Drop to act: keep, copy paths, zip, convert or halve dropped files.
  - A Recent page of moments you may have missed, and an opt-in clipboard history.
  - A first-run tour, one-time gesture tips, a Try it gallery in settings, Narrator announcements and high contrast support.
  - Choose what can interrupt, and optional sounds.
- Flux Island UI pass:
  - iOS dark system colours, a type ramp with fixed-width numbers, and every icon in a coloured badge.
  - Compact pill: icon or ring on the left, a big coloured value on the right. The call badge breathes, the mute slash draws, the charging bolt flashes, and the bubble shows a badge.
  - Music: the album's colours wash the panel, with bigger art, a scrubber that grows a knob and shows the time while you drag, solid controls, and long titles that scroll on hover.
  - Timers: tiles to start one, then big orange digits beside a dial, with round pause and stop buttons, like an iPhone.
  - Glance: ring tiles, a live network sparkline and round quick actions.
  - Lists: badges, actions on hover, rows that shrink away, Undo instead of a confirm, and helpful empty states. The command result is a tile with a check that draws itself; drop zones fill in their colour.
  - The compact art or ring travels into the open panel, pages rubber-band at the ends, failures shake, and the tour caption shows its steps.
- `--page <name>` opens FluxKit on a chosen page.
- `--preview-island <sample>[-open]` shows a sample island state, for screenshots and testing.

## [1.0.0-preview.1] — 2026-10-26

### Added
- FluxKit shell: settings window (Mica, navigation built from the utility registry), Home, General and About pages, first-run dialog, system tray icon and menu, single instance, launch at sign-in (per user).
- **Drag-to-Switch** utility:
  - Desktop selector overlay that appears during a file drag from File Explorer or the desktop.
  - Hover-to-switch with a visible dwell progress bar (200–1500 ms, default 600 ms).
  - Number keys 1–9, Esc to cancel, optional return to the original desktop on cancel.
  - Move app windows: drag a window by its title bar, hold it over a desktop, and the window comes along (setting "Move app windows between desktops", on by default; shell integration backend only).
  - Taskbar and tray drags no longer show the selector.
  - Text selection, scrollbar drags and rubber-band selection no longer show the selector. A file drag must be confirmed as a real OLE drag first.
  - Dragging selected text, links or web images no longer shows the selector. For drags outside File Explorer, a brief invisible check confirms that the drag carries files.
  - Collapses to a pill after switching.
  - Wallpaper previews; position, size and theme options.
- Virtual desktop backends: shell COM integration (verified on build 26300 with the 24H2 layout) with a `Ctrl+Win+Arrow` keyboard fallback. Every switch is verified against the registry.
- JSON settings with schema versioning, atomic writes, corruption recovery and read-only mode for newer schemas.
- Redacted rolling logs (no file names, paths or window titles).
- Tools: DropProbe (drop-target logger), DragHarness (automated real OLE drag E2E driver), VdProbe (COM layout probe).
- 69 automated unit tests.

### Changed
- FluxKit now presents itself as a collection of tools, with Drag-to-Switch as the first one. "Utilities" is now "Tools" in the app. The tray tooltip lists every tool, and the tray icon shows "off" only when every tool is off.
- New app icon (a grid of tools) and a separate colour icon for Drag-to-Switch, used in the menu, on Home and on its page.
- All app text is rewritten in plain English. The desktop selector is now called the "desktop picker".
- New README and CONTRIBUTING guide, including how to add a tool.

### Changed from the plan
- P/Invoke is hand-written instead of generated by CsWin32, to keep the interop surface small and reviewable.
- The tray icon uses `Shell_NotifyIcon` directly instead of H.NotifyIcon.
- Settings rows use a small custom `SettingRow` control instead of the CommunityToolkit SettingsCard.
- The "App icons" and "Wallpaper + app icons" preview styles were deferred. Previews are Wallpaper or None.
- Views (selector window, settings page) live in the App project. The utility library holds logic only and stays testable without XAML.
