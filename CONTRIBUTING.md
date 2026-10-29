# Contributing to FluxKit

Thanks for helping. FluxKit is a collection of small Windows tools, and it grows one tool at a time. Bug reports, ideas, docs fixes and code are all welcome.

## Ways to help

| You want to… | Do this |
|---|---|
| Report a bug | Open an issue with the steps to reproduce it, what you expected, and what happened. Paste the text from **About › Diagnostic info › Copy**. It has no file names in it. |
| Suggest a tool | Open an issue that describes the problem the tool solves and how you'd use it. |
| Fix a typo or improve the docs | Send a pull request straight away. |
| Change code | For anything bigger than a small fix, open an issue first so we can agree on the approach. |

## Set up

1. Install Windows 11 and the [.NET 10 SDK](https://dotnet.microsoft.com/download). Visual Studio 2022 or VS Code is optional.
2. Clone the repository and build it:

   ```powershell
   ./scripts/build.ps1
   ./scripts/test.ps1
   ```

3. Run `src/FluxKit.App/bin/x64/Debug/<framework>/win-x64/FluxKit.exe`.
   - Quit FluxKit from the tray icon before you rebuild, because a running copy locks its files.
   - Logs are in `%LOCALAPPDATA%\FluxKit\Logs`. Set **General › Logging level** to **Detailed** for more detail.

More detail is in [Docs/Setup.md](Docs/Setup.md).

## Project layout

| Folder | What's in it | Rules |
|---|---|---|
| `src/FluxKit.Core` | Settings, logging, the tool registry | No Windows-specific code, so it's easy to test |
| `src/FluxKit.Platform` | Windows APIs: input hooks, virtual desktops, windows, tray | No UI |
| `src/FluxKit.Tools.<Tool>` | One project per tool, holding its logic and settings | No XAML, so the logic can be unit tested |
| `src/FluxKit.App` | The WinUI app: shell, pages, tool UI under `Modules/<Tool>` | Composition root |
| `tests/` | xUnit tests for Core and each tool | |
| `tools/`, `spikes/` | Test helpers and experiments | Not shipped |
| `Docs/` | User and developer docs | |

References only point down: App → Tool → Platform → Core.

## Adding a tool

A tool is a module that the FluxKit shell hosts. The shell builds its rail item, Home tile, status dot and tray toggle automatically.

1. **Create the project.** Add `src/FluxKit.Tools.<Name>`, referencing `FluxKit.Platform`, and add it to `FluxKit.slnx`.
2. **Add settings.** Create a class that implements `ISettingsSection` with a `Section` constant built from `SettingsKeys`, such as `SettingsKeys.ToolPrefix + MyTool.Id` (this gives `"utilities/my-tool"`; the saved prefix stays "utilities", see ADR-013). Give every property a sensible default. The settings file must never break the app, so check and clamp values.
3. **Implement `IFluxTool`.**
   - `Descriptor`: a unique kebab-case id, a display name, a one-line description, a [Segoe Fluent Icons](https://learn.microsoft.com/windows/apps/design/style/segoe-fluent-icons-font) glyph, a sort order, and optionally a colour icon file name.
   - `StartAsync`/`StopAsync`: these must be safe to call more than once. Report problems through `State` (`Degraded` or `Faulted` with a short message for the user) instead of throwing.
4. **Add an icon (optional).** Draw it in `scripts/generate-icons.ps1`, run the script, and set the file name (for example `"MyTool.png"`) as the descriptor's `IconAsset`.
5. **Add the settings page.** Put it under `src/FluxKit.App/Modules/<Name>/` and map it in `MainWindow.ToolPages`. Build it the same way as the Drag-to-Switch page: a `PageHeader` with the on/off switch, then a `ToolPageLayout` with a preview on one side and `SettingsGroup`s of `SettingRow`s on the other. Put its text in `Strings/en-US/Resources.resw` (`x:Uid` in XAML, `Strings.Get` in code).
6. **Register the tool.** In `AppHost.StartAsync`, add `Registry.Register(new MyTool(...))`.
7. **Write tests** in `tests/FluxKit.<Name>.Tests`. Use fakes for Windows APIs and `FakeTimeProvider` for timing.
8. **Document it.** Add a row to the tool table in the README, its settings to [Docs/Configuration.md](Docs/Configuration.md), and anything it can't do to [Docs/KnownLimitations.md](Docs/KnownLimitations.md).

[Docs/Architecture.md](Docs/Architecture.md) explains threads, settings and how Drag-to-Switch is built. Use it as a working example.

### What every tool must respect

- **No admin rights, drivers, services or code injection** into other apps.
- **No network access and no telemetry.**
- **Never log file names, file contents or window titles.** Log counts and process names instead.
- **Never pretend something worked.** If the tool can't confirm a result, don't show it as done.
- **Don't change system-wide settings** unless that is clearly the tool's job and the user turned it on.
- **Keep the UI thread free.** Do Windows API calls, hooks and COM on background or dedicated threads.

## Code style

- Follow [Docs/CodingStandards.md](Docs/CodingStandards.md): principles, folders, names, formatting, reuse and tests. Run `./scripts/build.ps1 -Verify` before you push; it checks formatting too.
- C# with nullable reference types on. Warnings are treated as errors, so the build must stay at 0 warnings.
- Write user-facing text in plain, friendly English: short sentences, everyday words, no jargon. Say "Turn on", not "Enable". Say "couldn't", not "could not be performed".
- Use theme resources for colours (`{ThemeResource …}`), so light, dark and high-contrast modes all work.
- Give controls accessible names when their text alone isn't clear.

## Pull request checklist

- [ ] `./scripts/build.ps1 -Verify` builds with 0 warnings and clean formatting.
- [ ] `./scripts/test.ps1` passes, and new behaviour has tests.
- [ ] You ran the app and tried the change yourself.
- [ ] Docs are updated (README, Configuration, KnownLimitations, CHANGELOG) when behaviour changes.
- [ ] Test results are honest. In [Docs/ManualTestPlan.md](Docs/ManualTestPlan.md), mark a test as passed only if you actually ran it.
- [ ] Commit messages use [Conventional Commits](https://www.conventionalcommits.org/), e.g. `feat(drag-to-switch): …` or `fix(app): …`.

## Reporting security problems

Please don't open a public issue for a security problem. Contact the maintainers privately first, and give us time to fix it before you share details.

## License

By contributing, you agree that your contributions are licensed under the [MIT License](LICENSE).
