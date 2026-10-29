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

## Testing status

- Automated: 69 unit tests (Core and Drag-to-Switch logic).
- Real-drag E2E through the FluxKit selector, and manual tests with real File Explorer, are tracked in [ManualTestPlan.md](ManualTestPlan.md) and [Feasibility.md](Feasibility.md). Only executed results are marked as passed.
- Untested: multi-monitor with mixed DPI, Remote Desktop sessions, ARM64 hardware, Windows 10.
