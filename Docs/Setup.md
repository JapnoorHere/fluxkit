# Developer setup

## Requirements

- Windows 11 (x64 or ARM64)
- .NET SDK **10.0.303** (pinned in `global.json`, newer feature bands roll forward)
- Optional: Visual Studio 2022 17.14+ or Rider. Everything builds with the `dotnet` CLI.

No Windows App SDK runtime installer is needed. The app is unpackaged and self-contained.

## Commands

```powershell
./scripts/build.ps1 [-Configuration Release]   # whole solution, warnings are errors
./scripts/test.ps1 [-Coverage]                  # unit tests (excludes Category=Integration)
./scripts/publish.ps1 [-Runtime win-arm64]      # artifacts/FluxKit-<version>-<rid>.zip
./scripts/e2e.ps1                                # real drags through the running FluxKit (moves your mouse)
```

Run the app from source:

```powershell
dotnet build src/FluxKit.App
src/FluxKit.App/bin/x64/Debug/net10.0-windows10.0.26100.0/win-x64/FluxKit.exe
```

## Command-line switches

| Switch | Effect |
|---|---|
| `--background` | Start in the tray without opening the window. The startup entry uses this. |
| `--settings` | Open the Drag-to-Switch settings page. |
| `--page <name>` | Open a page: `home`, `general`, `about`, or a tool id such as `drag-to-switch`. |

Starting FluxKit a second time doesn't create a second instance. It brings the running one to the front.

## Debugging tips

- Logs: `%LOCALAPPDATA%\FluxKit\Logs`. Set **General › Logging level › Verbose** for per-session details.
- `FLUXKIT_VD_BACKEND=keyboard` forces the keyboard backend, to test Limited mode.
- Attach a debugger to `FluxKit.exe`. Breakpoints in the hook callback can make Windows silently remove the hook, because hooks have a time limit. Prefer logging there.
- `spikes/VdProbe` prints the resolved COM layout and compares it with the registry, without switching anything unless you pass `switch N`.

## Feasibility tools

- `tools/FluxKit.DropProbe`: a drop target that logs received paths to JSONL. `--desktop N` places it on desktop N.
- `tools/FluxKit.DragHarness`: starts a real OLE `DoDragDrop`, drives the mouse with `SendInput`, and checks that DropProbe received the exact file. Modes: `--switch com-direct|manual-keys|fluxkit`, `--scenario drop|cancel|release-on-overlay`.

Both need an **unlocked, interactive** session. Input injection doesn't reach a locked workstation.
