# Privacy

FluxKit works entirely on your PC.

- **No network.** FluxKit makes no network connections: no telemetry, no update checks, no crash upload.
- **Your files.** FluxKit never reads, opens, copies, moves or uploads the files you drag. The drag itself belongs to Windows and the apps involved. FluxKit only switches desktops while it's in progress.
- **Input.** While Drag-to-Switch is on, FluxKit uses standard Windows low-level mouse and keyboard hooks to notice when a drag starts and where the cursor is. It does not record keystrokes. Key events are only checked for Esc and 1–9 during an active drag. The only input it ever blocks is the 1–9 digits while the selector is open (to switch desktops), and only when number keys are enabled. Turning Drag-to-Switch off removes the hooks.
- **Flux Island** listens only to Windows events, and never polls:
  - **Media:** the title, artist, art and position that players publish to the Windows media controls. Art is held in memory for the current track only.
  - **Microphone and camera:** whether the Windows privacy records say a device is in use. App names are read only to decide that, and are never shown, stored or logged.
  - **Clipboard:** that something was copied, and whether it was text, an image or files. The content is never read.
  - **Shelf:** the paths of files you drop on it, in memory only, gone when FluxKit exits. The shelf never opens, copies or moves the files; dragging one out offers copy or link to the app you drop into.
  - **Charger, network and volume:** the battery level and charging state, whether there's internet, and the master volume (changed only when you scroll over the island).
- **No code injection.** FluxKit doesn't load anything into other processes.
- **What's stored**, in `%LOCALAPPDATA%\FluxKit` only:
  - `settings.json`: your preferences.
  - `Logs\`: diagnostic logs. These contain the FluxKit and Windows version, desktop counts, process names of drag sources (e.g. `explorer`), timings and errors. They **never** contain file names, paths, file contents or window titles. Paths that appear in error messages are replaced by `<path>`.
- **Startup.** If you turn on "Launch when I sign in", FluxKit adds one value under `HKCU\...\Run`. Turning it off removes it.

Deleting `%LOCALAPPDATA%\FluxKit` removes everything FluxKit stored.
