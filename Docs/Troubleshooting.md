# Troubleshooting

## General tips

- **Copy diagnostic info.** Go to **About › Diagnostic info › Copy** and paste the text into your bug report. It has no file names in it.
- **Read the logs.** Go to **General › Open log folder**. For more detail, set **General › Logging level** to **Detailed**. It switches back to Normal after 24 hours.
- **Reset everything.** Go to **General › Reset all settings**. Or quit FluxKit and delete `%LOCALAPPDATA%\FluxKit\settings.json`.

## Drag-to-Switch

Start with **Drag-to-Switch › Troubleshooting** in the FluxKit window. It shows the virtual desktop support in use, the desktops found, and what happened in your last drag.

### The picker doesn't appear

- Make sure you have **at least two desktops**. Press `Ctrl+Win+D` to add one.
- Make sure Drag-to-Switch is **on**. Check the Home page or the tray menu.
- By default, only file drags that start in **File Explorer or on the desktop** count. To include other apps, set **Where file drags can start** to **Any app**.
- Drag a little further. The drag has to move past the Windows drag distance and last about a tenth of a second.
- The picker stays hidden while a full-screen game or presentation is running.
- If the app you drag from runs as administrator, Windows may hide its input from FluxKit.

### The picker appears when I don't want it to

- Selecting text and dragging scrollbars never show the picker.
- Dragging selected text, a link or a web image doesn't show the picker either. FluxKit checks that a drag carries files before showing it. If a file drag from another app doesn't show the picker, that app may offer its files in a way FluxKit doesn't recognise. Please report the app name.
- To stop the picker for windows you drag by the title bar, turn off **Move windows between desktops**.

### It says "Limited mode"

FluxKit couldn't use the full virtual desktop support on your version of Windows, so it switches desktops with `Ctrl+Win+Left/Right arrow` instead. File drags still work, and switching just takes a moment longer. Moving windows isn't available in this mode.

Select **Check again** after a Windows update or an Explorer restart. If it keeps happening, attach the log to a bug report. The log lists the support FluxKit tried.

### "Couldn't switch"

The desktop didn't change in time, or FluxKit couldn't confirm the change. Try again, or press `Ctrl+Win+Left/Right arrow` while you keep holding the file. Repeated failures are logged as warnings with timings.

### The file didn't arrive

FluxKit only takes you to the other desktop. The app you drop into decides whether to accept the file. Check that:

- you dropped onto an app that accepts files;
- that app isn't running as administrator, because Windows blocks drops into those apps;
- you didn't drop onto the picker itself. The picker never accepts drops, by design.

### A window didn't move

- Windows of apps running as administrator can't be moved by FluxKit.
- Some apps draw their own title bar and don't use the normal Windows move. FluxKit can't detect drags of those windows.
- Window moves aren't available in Limited mode.

## Startup

- **"FluxKit is set to start from an older copy of the app"**: you moved the FluxKit folder. Select **Fix** on the General page.
- **"FluxKit is turned off in Windows Settings › Apps › Startup"**: turn it back on there. FluxKit doesn't override your choice.
