<p align="center">
  <img src="src/FluxKit.App/Assets/FluxKit.png" width="96" alt="FluxKit logo">
</p>

<h1 align="center">FluxKit</h1>

<p align="center">
  <b>Small tools that make Windows easier to use.</b><br>
  A free, open-source toolkit for Windows 11, built with WinUI 3 and .NET.
</p>

---

FluxKit is a collection of small tools. Each tool does one job well, and you turn on only the ones you want. Everything runs on your PC, with no admin rights, no account and no internet connection.

FluxKit is new, so it has two tools today, with more on the way.

| Tool | What it does | Status |
|---|---|---|
| <img src="src/FluxKit.App/Assets/DragToSwitch.png" width="20" align="center"> **Drag-to-Switch** | Move files and windows to another virtual desktop while you drag them. | Preview |
| <img src="src/FluxKit.App/Assets/FluxIsland.png" width="20" align="center"> **Flux Island** | A live pill at the top of the screen for music, timers, files and quick status. | Preview |

Have an idea for a tool? [Open an issue](../../issues) and tell us about it.

## Drag-to-Switch

Windows lets you have several virtual desktops, but dragging a file from one desktop to an app on another is awkward. Drag-to-Switch fixes that.

**Drag a file:**

1. Start dragging a file in File Explorer or on the desktop.
2. A desktop picker appears at the bottom of the screen.
3. Hold the file over a desktop. A bar fills up, and after a moment FluxKit takes you there.
4. Drop the file into any app on that desktop.

**Drag a window:**

1. Drag any window by its title bar.
2. Hold it over a desktop in the picker.
3. The window moves to that desktop with you. Let go to place it.

**Shortcuts while dragging:**

| Key | What it does |
|---|---|
| `1`–`9` | Go straight to that desktop |
| `Esc` | Cancel. A moved window goes back to its first desktop. |

Your drag is never interrupted, and FluxKit never copies, moves, opens or uploads your files. The app you drop into handles the file, just like a normal drag.

## Flux Island

A small black pill sits at the top centre of your screen. It shows the time when nothing is happening, and grows to show what is. Every moment has its own colour and a soft glow, it springs between shapes like a living thing, and when two things happen at once the second buds off into a bubble.

**Three things no other island does:**

- **Island Command.** Press **Ctrl+Alt+I** and type: `5m` starts a timer, `= 12*8` calculates (Enter copies the answer), `#3a7bff` shows a colour, `note call Sam` saves a note, and words like `lock`, `screenshot`, `pause` or `glance` do what they say.
- **Call mode.** During a Teams, Zoom or browser call, the island shows the call time and a mute button, and nudges you if you start talking while muted.
- **Drop to act.** Drag files onto the island to keep them on the shelf, copy their paths, zip them, or convert or halve images. The results land on the shelf.

| Moment | What you see |
|---|---|
| Music or video playing | The album art and title, with bars that move to the real sound. Click to open play, skip and a scrubber over a glow in the album's colours. |
| Timer | Quick 1, 5 and 10 minute timers and a focus cycle (25 min focus, 5 min break by default), in a ring that empties as time runs out, with a dial, rolling digits and a bounce when time's up. |
| Charger | A ring that sweeps to the battery level when you plug in, and a warning when the battery runs low. |
| Connection | A note when the internet drops, and when it comes back. |
| Microphone or camera | A small orange or green dot while an app uses them. |
| Copy | A short "Copied" when you copy text, an image or files. |
| Shelf | Drop files on the island to keep them, then drag them out later into any app. |
| Volume | Scroll over the island to change the volume. Volume changes from the keyboard or anywhere else show too. |
| Sound and microphone | The device sound moves to (such as headphones), and microphone mute, with a reminder while a call listens to a muted mic. |
| Desktops | The name of the virtual desktop you switch to. |
| Downloads and screenshots | A download while it arrives and when it's done, with a button to find it. A new screenshot shows a small preview and opens on click. |
| Smart copy (opt-in) | A copied link, email address, phone number or colour, with a button to open it or a swatch. |
| Brightness and battery saver | The brightness level when it changes, battery saver turning on or off, and the time left or until full. |
| Notifications (opt-in) | Messages, mail and reminders from your apps, with the app's logo and colour, in place of Windows' corner banner. Bursts group into "3 new", a Notifications page groups today's by app, and you choose how much text shows. |
| Caps Lock and Num Lock | A short note when they turn on or off. |
| Bluetooth | When paired earbuds, a mouse or another device connects or disconnects. |
| Stopwatch | A stopwatch with laps, beside the timers, and +1 min while a timer runs. |
| Glance | A page with live CPU, memory, disk and battery rings, network speed, and buttons to take a screenshot, lock the PC, turn the screen off, mute the mic or open settings. |
| Notes | Quick notes from the command bar, on their own page. Click to copy. |
| Recent | The last 30 moments worth a second look (downloads, screenshots, copied links, devices), so nothing you missed is lost. |
| Clipboard (opt-in) | The last 25 pieces of text you copied, with pins. |

Hover over the island to peek at more, and click it to open the panel; swipe sideways (or press PageDown) to move between its pages. In the command bar, press Down to open the panel instead. When two things happen at once, the second shows in a small bubble beside the pill; click it to swap. Right-click waves a moment away. You choose what may interrupt (everything, important only, or nothing) and whether moments make a sound. When an app goes full screen, the island steps out of the way.

New to it? The island gives a 30-second tour the first time it runs, shows a one-time tip the first time each gesture helps, and its settings page has a **Try it** gallery that plays any moment on the real island. There's also a labs **Glass** look.

It runs only on events: with nothing changing it uses no CPU, and everything stays on your PC.

### Liquid: what no other island does

Flux Island behaves like a drop of liquid (ADR-015):

- A moment **drips in**: the island pulls a drop out of its underside and drinks it back.
- A moment waved away **sheds** a drop that falls and fades.
- The underside **ripples like ferrofluid** with your music, and when your pointer touches it.
- **Mercury** (liquid metal with a highlight that follows the pointer) and a **light halo** that spills the island's colour onto the desktop are in labs.

**Gestures:**

- **Flick a moment:** throw it down to put it away (it stays in Recent), up to let it evaporate, or sideways to snooze it for 10 minutes.
- **Tear it off:** pull music, a timer or the stopwatch down off the island. It becomes a floating droplet you can park anywhere and click to play, pause or resume. Drop it back on the island (or right-click it) to dock.
- **Scrub today:** scroll sideways over the island and it steps back through today's moments; click one to open it.

**Live Activities for anything (labs, ADR-016).** Any script, terminal, build or AI agent can put its own live progress on the island:

```powershell
FluxKit.exe island set  --id build --title "Build" --detail "Compiling" --progress 40 --glyph build | Out-Null
FluxKit.exe island done --id build --detail "All tests passed" | Out-Null   # or: fail / clear
```

To have Claude Code tell you when it finishes, add a `Stop` hook to `.claude/settings.json`:

```json
{ "hooks": { "Stop": [ { "hooks": [ { "type": "command", "command": "FluxKit.exe island done --id claude --title \"Claude finished\" --glyph ai" } ] } ] } }
```

Glyphs: `spark`, `build`, `test`, `deploy`, `download`, `upload`, `ai`, `sync`, `terminal`, `music`. Updates are display only: they can never make FluxKit run anything.

## Install

1. Download the latest `FluxKit-<version>-win-x64.zip` (or `win-arm64`) from [Releases](../../releases).
2. Unzip it anywhere, for example `%LOCALAPPDATA%\Programs\FluxKit`.
3. Run `FluxKit.exe`. FluxKit opens its window and then keeps running in the system tray.
4. Optional: go to **General** and turn on **Start FluxKit when I sign in**.

FluxKit isn't code-signed yet, so Windows SmartScreen may warn you the first time. Select **More info › Run anyway**.

**Requirements:** Windows 11 (22H2 or later), x64 or ARM64. Tested on Windows 11 26H2, build 26300.

**To uninstall:** turn off **Start FluxKit when I sign in**, quit FluxKit from the tray icon, then delete the FluxKit folder and `%LOCALAPPDATA%\FluxKit`.

## How it works

FluxKit is one app that hosts many tools. The app provides the parts every tool needs: the settings window, the tray icon, settings storage, logging and startup. Each tool is a separate module that plugs in.

**The window.** A slim rail on the left lists Home, each tool, General and About. Home has a tile for every tool, with its on/off switch and status. Each tool has its own page, with a live preview of the tool next to its settings. Changes are saved as soon as you make them.

```
FluxKit.exe
├─ Shell: settings window, tray icon, start with Windows
├─ Core: settings (JSON), logging, tool registry
├─ Platform: Windows APIs (input hooks, virtual desktops, windows)
└─ Tools
   └─ Drag-to-Switch
```

**Inside Drag-to-Switch:**

1. **It notices a drag.** FluxKit watches mouse input with standard Windows low-level hooks. It doesn't inject code into other apps. When you press and move the mouse, FluxKit checks what kind of drag it is:
   - It's a **file drag** if Windows has started a real drag-and-drop. During one, Windows gives the mouse to a special system window, and FluxKit looks for that.
   - It's a **window drag** if Windows is moving a window by its title bar.
   - Anything else is ignored, such as selecting text or dragging a scrollbar.
2. **It shows the picker.** The picker is a small window that stays on top and never takes focus, so your drag carries on.
3. **It switches desktops.** When you hold over a desktop, FluxKit switches to it through the Windows shell. For a window drag, it moves the window first. Each switch is checked before the picker says it's done.
4. **You finish the drag.** You drop the file or let go of the window. FluxKit never pretends to deliver anything.

If the full virtual desktop support isn't available on your version of Windows, Drag-to-Switch switches desktops with the `Ctrl+Win+Arrow` shortcut instead. The app shows this as **Limited mode**.

For the full design, see [Docs/Architecture.md](Docs/Architecture.md).

## Privacy

- No telemetry and no network connections.
- FluxKit never reads, copies, moves or uploads your files. The Flux Island shelf keeps only the paths of files you drop on it (saved on this PC if you keep the shelf after restart).
- Settings and logs stay on your PC. Logs never include file names, file contents or window titles.
- Notifications (off by default) are read only with Windows' permission, shown on screen, kept in memory only, and never logged. Windows' banner switch is changed only for apps you choose, and put back exactly.

Details: [Docs/Privacy.md](Docs/Privacy.md).

## Build from source

You need Windows 11 and the [.NET 10 SDK](https://dotnet.microsoft.com/download). Visual Studio is optional.

```powershell
git clone <repository-url> fluxkit
cd fluxkit
./scripts/build.ps1     # build everything
./scripts/test.ps1      # run the unit tests
./scripts/publish.ps1   # make a self-contained zip in artifacts/
```

The built app is in `src/FluxKit.App/bin/x64/Debug/<framework>/win-x64/FluxKit.exe`. See [Docs/Setup.md](Docs/Setup.md) for details.

## Contributing

Contributions are welcome: bug reports, ideas for new tools, docs fixes and code.

- **Found a bug?** Open an issue. Include the steps to reproduce it and the diagnostic info from **About › Diagnostic info › Copy**.
- **Want to add a tool?** Open an issue first, so we can agree on what it does before you build it. Then follow [Adding a tool](CONTRIBUTING.md#adding-a-tool).
- **Sending code?** Read [CONTRIBUTING.md](CONTRIBUTING.md) for the setup, code style and pull request checklist.

## Documentation

| Document | What's in it |
|---|---|
| [CONTRIBUTING.md](CONTRIBUTING.md) | How to contribute and how to add a tool |
| [Docs/Architecture.md](Docs/Architecture.md) | Projects, threads, the drag state machine |
| [Docs/CodingStandards.md](Docs/CodingStandards.md) | Coding standards: principles, folders, names, formatting, reuse |
| [Docs/Configuration.md](Docs/Configuration.md) | Every setting and where files are stored |
| [Docs/Troubleshooting.md](Docs/Troubleshooting.md) | Fixes for common problems |
| [Docs/KnownLimitations.md](Docs/KnownLimitations.md) | What doesn't work yet, and why |
| [Docs/Privacy.md](Docs/Privacy.md) | What FluxKit watches and stores |
| [Docs/ManualTestPlan.md](Docs/ManualTestPlan.md) | Manual tests and their results |
| [Docs/Feasibility.md](Docs/Feasibility.md) | Early experiments that shaped Drag-to-Switch |
| [Docs/adr](Docs/adr/README.md) | Architecture decision records |

## License

FluxKit is released under the [MIT License](LICENSE). See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for the projects it builds on.
