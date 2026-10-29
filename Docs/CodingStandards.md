# FluxKit coding standards

These are the rules every change to FluxKit follows. Mechanical rules (formatting, naming) are checked by tools. The rest are checked in review. When this page and the code disagree, fix one of them in the same pull request.

## 1. Principles

| Principle | What it means in FluxKit |
|---|---|
| **One-way layers** | App → Tool → Platform → Core. Core has no Windows APIs. Platform has no UI. A tool's logic project has no XAML. Nothing references App. |
| **One job per class** | A class has one reason to change. A file over about 300 lines is a sign it does too much. |
| **Abstractions at the edges** | Input, desktops, windows and time are reached only through interfaces (`IVirtualDesktopService`, `ISystemInputSource`, `IWindowProbe`, `TimeProvider`). Logic is tested with fakes, never with the real OS. |
| **Composition over inheritance** | Combine small helpers and controls. Avoid deep base-class hierarchies. |
| **Don't repeat what must stay in sync** | Extract shared code when it appears a third time, or straight away when the copies must match (P/Invoke, resource keys, settings keys). Don't merge code that only looks alike. |
| **Safe by default** | No admin rights, network, telemetry or code injection. Never log file names, file contents or window titles. Never claim something worked when it can't be confirmed. See [CONTRIBUTING › What every tool must respect](../CONTRIBUTING.md#what-every-tool-must-respect). |
| **Fail soft** | A tool reports `Degraded` or `Faulted` instead of crashing the app. Platform calls return typed results and have timeouts. |
| **Honest tests** | New behaviour gets a unit test. Manual test results are recorded only if the test was really run. |

## 2. Folders and files

```
src/
  FluxKit.Core/                 Settings, logging, the tool registry (no Windows APIs)
  FluxKit.Platform/             Native/  Input/  Threading/  Windows/  Shell/  System/  VirtualDesktop/
  FluxKit.Utilities.<Name>/     One project per tool: the tool class, its settings, its logic folders
  FluxKit.App/
    Shell/                      Main window and the Home, General and About pages
    Controls/                   Reusable XAML controls
    Theming/                    Colours, sizes, styles, motion
    Strings/en-US/              User-visible text (Resources.resw)
    Modules/<Name>/             A tool's settings page and windows
    Assets/                     Icons
tests/FluxKit.<Project>.Tests/  Same folders as the project under test, plus Fakes/
tools/  spikes/  scripts/  Docs/
```

- **One public type per file**, named after the type. A few small enums that belong to one type may share its file.
- **Namespace = folder path.** `FluxKit.Platform.Windows` lives in `src/FluxKit.Platform/Windows/`.
- **Files FluxKit writes at run time** all live under `%LOCALAPPDATA%\FluxKit\` (`settings.json`, `Logs\`). The only thing written elsewhere is the documented startup entry in `HKCU\...\Run`.
- Build output goes to `bin/` and `obj/`, and `artifacts/` for publish and test results. All three are ignored by git.

## 3. Names

- `PascalCase`: types, methods, properties, events, constants, enum members, static readonly fields.
- `_camelCase`: private fields. `camelCase`: locals and parameters. Interfaces start with `I`.
- Async methods end with `Async`, and take a `CancellationToken` when the work can be cancelled.
- Win32 constants use the SDK name in PascalCase (`WsExToolWindow`). Win32 structs keep the SDK name (`MSG`, `POINT`).
- Test methods read as sentences: `Scenario_expected_result`.
- Use the product's words in user text: **tool**, **desktop picker**, **desktop**.
- XAML `x:Name`: PascalCase with the role at the end (`DelaySlider`, `StatusBar`, `OptionsPanel`).

## 4. Formatting

`.editorconfig` is the rule book, and `dotnet format` applies it.

- 4 spaces in C#, 2 in XML, XAML and JSON. LF line endings (PowerShell scripts: CRLF). UTF-8. Every file ends with a newline.
- File-scoped namespaces. `using` directives go outside the namespace, `System` first, with no blank lines between groups.
- Braces on their own line, and always used.
- `var` only when the type is obvious from the right-hand side.
- Expression bodies for one-line members.

Check before you push:

```powershell
./scripts/build.ps1 -Verify      # build + formatting check (CI runs the same)
dotnet format FluxKit.slnx       # fixes formatting
```

XAML: when an element has more than three attributes, put one per line with `x:Name` first. Use the spacing and size tokens from `App.xaml` instead of raw numbers.

## 5. C# rules

- Nullable reference types are on, and warnings are errors (`Directory.Build.props`). The build stays at 0 warnings.
- No `async void`, except XAML event handlers. Those wrap their body in `try`/`catch` and log the error.
- Every call that crosses a thread or process boundary has a timeout. COM objects are used only on the thread that created them.
- A class with shared state has one private lock object. Avoid raising events or calling the UI while holding it.
- **Logging** uses Serilog message templates (`"Switched to desktop {Index}"`), never string interpolation.
  - Log counts and process names. Never log paths, file names or window titles.
  - Information: lifecycle and drag sessions. Debug: detail. Warning: degraded. Error: faults.
- **Comments:**
  - XML doc comments on public members.
  - Inline comments say *why*, not *what*.
  - Match how much the surrounding code comments.
- **P/Invoke:**
  - `LibraryImport` by default. `DllImport` only when marshalling needs it (delegates, COM interfaces), with the reason in the `#pragma` comment.
  - Declarations used by more than one file live in `FluxKit.Platform/Native/`.

## 6. Reuse and modules

- **A tool is a module.** It is an `IFluxUtility`, plus a settings class (`ISettingsSection`), plus a settings page. The shell finds it through the registry. Adding a tool needs one registration line in the shell and nothing else.
- **Shared UI first.**
  - Build settings pages from the shared controls in `FluxKit.App/Controls`.
  - Don't hand-build `Border`/`StackPanel` trees in code-behind.
  - Colours come from theme resources, so light, dark and high contrast all work.
- **User text in resources.** Visible text belongs in `Strings/en-US/Resources.resw`. Use format strings, and never glue sentences together.
- **One helper per job.** Settings keys, app folders, message-loop threads and Win32 declarations each have one implementation that everything else reuses.

## 7. Tests

- Arrange, act and assert, separated by blank lines.
- Fakes live in `tests/<Project>/Fakes/`, one per file.
- Time is controlled with `FakeTimeProvider`. No `Thread.Sleep` in unit tests.
- Every bug fix adds a test that failed before the fix.
- Aim for at least 80 % line coverage in Core and tool logic: `./scripts/test.ps1 -Coverage`.

## 8. Commits

- One topic per commit, described in plain English (for example "Split the drag coordinator into smaller classes").
- Build, format check and tests pass before every commit.
