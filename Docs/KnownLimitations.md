# Known limitations

## Platform

- **Undocumented shell interface.** Direct desktop switching and pinning use the internal Windows virtual-desktop COM interface. Its layout changes between Windows builds. FluxKit checks each known layout against the registry before using it. On this machine (Windows 11 26H2, build 26300) the 24H2 layout validated. On an unknown future build, FluxKit drops to **Limited mode**.
- **Limited mode (keyboard backend).** Switching sends `Ctrl+Win+←/→`. It works during a drag (verified, Feasibility H6), but Ctrl is held for a moment, so the drop-effect cursor may flicker. Jumping several desktops takes about 80 ms per step.
- **Selector not pinned.** If pinning fails, the selector is moved to the current desktop after each switch with the documented `IVirtualDesktopManager.MoveWindowToDesktop`. This works, but can cause a brief flicker.

## Interaction

- Drag detection uses a left-button press in an allowed source (Explorer and the desktop by default), movement past the system drag threshold, and no window move or resize in progress. The selector then appears only once the app has really started an OLE drag. During an OLE drag, Windows gives mouse capture to an OLE window of class `CLIPBRDWNDCLASS` (verified on build 26300), so text selection, scrollbar drags and rubber-band selection never show it, even with **Where file drags can start › Any app**.
- **What is being dragged (Any app).** Dragging selected text, a link or a web image out of an app is also a real OLE drag. For drags that don't start in File Explorer, FluxKit briefly puts a small, invisible drop target under the pointer. Windows passes it the drag's data object, and FluxKit checks only which formats it offers (file list, shell items, or virtual files without a URL). It never reads the data and always refuses the drop. The picker appears only for files. The check takes a moment, during which the app under the pointer briefly shows a "can't drop" cursor. If you release the button during that moment, nothing is dropped. Links and images dragged out of a browser don't count as files.
- **Not supported in v1:** right-button drags, touch and pen drags.
- **Taskbar and tray drags** never show the selector. A taskbar drag reorders the button and carries no file, and no API maps a (possibly grouped) taskbar button to a single window, so the app behind it can't be moved along. Use a title-bar drag instead.
- **Moving app windows** (title-bar drag) uses the undocumented shell interface to move another app's window, so it needs the shell integration backend and is unavailable in limited mode. Windows of apps running as administrator can't be moved by FluxKit. Apps that draw their own title bar without using the system move loop aren't detected. Pressing Esc puts the window back on its original desktop.
- The selector stays on the monitor where the drag started.
- With more than 9 desktops, only 1–9 have number shortcuts.
- **Elevated apps:** Windows blocks drops from a normal-privilege app (such as Explorer) into an app running as administrator. FluxKit can't change that.
- FluxKit never knows whether the target app accepted the drop. It only knows that the mouse was released.
- No live previews of desktop contents (Windows has no public API for that). Cards show each desktop's wallpaper, or the current wallpaper when a desktop has none set.

## Flux Island

- **One screen at a time.** The island sits at the top centre of the main screen, or (with "Where the mouse is") of the screen the pointer was on when something new showed. It doesn't move while it is open or just because the pointer moves.
- **Keyboard shortcut** fails quietly if another app already uses the same keys; pick another in settings. When Windows refuses to hand focus to the island (it does that while you're busy in another app), the panel still opens but keys go to the other app until you click the panel; press the shortcut again or right-click to close it.
- **Caps Lock and Num Lock** moments come from Raw Input, so keys pressed in apps running as administrator, and on the sign-in or lock screen, aren't seen. Keyboards that switch Caps Lock in their own firmware without sending the key aren't seen either.
- **Bluetooth** moments come from Windows' paired-device list. Devices already connected when FluxKit starts aren't announced. Battery levels aren't shown yet. A headset that appears as both a classic and a Low Energy device gives one moment, not two.
- **Glance** shows the system drive's use only, and network speed as the total of all active adapters (not loopback or VPN tunnels).
- **Music** comes from the Windows media controls (GSMTC). Players that don't publish a media session (some games, older players) don't appear. Album art shows only when the player provides it. The seek bar works only when the player allows seeking.
- **Mic and camera dots** come from the Windows privacy records (the same ones Settings › Privacy uses). They can lag by a moment, and apps that use the device without going through Windows privacy tracking won't show.
- **Charger** moments need a battery. On a desktop PC without one, nothing appears.
- **Full screen** means a window that covers the whole main monitor without a title bar. Borderless "windowed full screen" games count; a maximised window doesn't.
- **Shelf** items are paths kept in memory: they're gone when FluxKit exits, and a file moved or deleted in the meantime shows faded and can't be dragged out. The shelf offers copy or link when you drag out, never move, so the original file always stays where it is. Dropping on the island needs the pointer over the pill itself, which is small until it opens.
- **Volume** changes the default playback device's master volume. Scrolling with a precision touchpad changes it in the same 2 % steps once enough scroll has built up.
- **Click-through** comes from the window region, which follows the pill's resting size. While the pill springs past its size, a few pixels of overshoot can be cut off.

## Testing status

- Automated: 206 unit tests (Core, Drag-to-Switch and Flux Island logic).
- Flux Island: every state was rendered and checked from screenshots with `--preview-island`. The settings page, the transparency and the idle cost (0 ms CPU over 20 s) were checked by running the app. The volume interop was checked with a read-only call. The live checks that need you (music, charger, mic, copy, dropping on the shelf, scrolling, a full-screen game, hover and click) are listed in [ManualTestPlan.md](ManualTestPlan.md) and haven't been run yet.
- Real-drag E2E through the FluxKit selector, and manual tests with real File Explorer, are tracked in [ManualTestPlan.md](ManualTestPlan.md) and [Feasibility.md](Feasibility.md). Only executed results are marked as passed.
- Untested: multi-monitor with mixed DPI, Remote Desktop sessions, ARM64 hardware, Windows 10.
