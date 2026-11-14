# Flux Island v3 plan: the "Pro" revamp

## Context

Flux Island works (v2 is done: 24 moments, 4 panel pages, shortcut, Glance), but the user feels it looks **basic** — like one person's side project rather than a product from a big design-led company. The goal: an island that feels as good as (or better than) Apple's Dynamic Island, and stands out from rivals:

- **Mac:** Alcove (polish and animation), NotchNook and Droppy (file tray, clipboard, transforms, add-ons), Boring Notch (visualiser).
- **Windows:** Notchify (clipboard history, file tray), Nusa (lyrics, to-do), Dynamic Edge, Smart Notch.

User decisions: **Onyx black by default, plus a labs "Glass" style the user can pick**; **stay 100% offline** (privacy is a selling point); **run every phase, build and test after each, one simple one-line commit per phase, no push, no co-author line.**

### What research says "premium" means
- **Apple HIG / WWDC23:**
  - Compact, minimal and expanded layouts should feel like one object.
  - Elements keep their relative places when the island expands.
  - Rounded, thick shapes, and colour as identity.
  - Two activities shown as a "minimal" bubble that buds off the pill.
- **Liquid Glass (iOS 26 / macOS 26):** light that bends rather than blurs, specular edge highlights that react to movement, content-tinted material.
- **Rivals win on:** buttery morphs (Alcove), useful utilities (Droppy's clipboard with OCR, file transforms, add-ons), visualisers (Boring Notch), clipboard history (Notchify).
- **Rivals' weak spots:** they cover content, eat CPU at 120–240 Hz, have no onboarding, little accessibility, and mixed privacy.

### What's "off" today (honest audit)
1. **Flat look:** a black rounded rect, a hairline shadow, plain text. There's no depth, edge light or material, and colour isn't used as identity.
2. **Motion:** one spring for everything, so all transitions feel alike. There's no content blur or scale in morphs, no anticipation, and the bubble pops in with no "bud" between it and the pill.
3. **Layout:** compact and expanded views aren't related (glyph left / text / trail, then a totally different panel). Spacing and type sizes are ad hoc (13/12/15/28), and digits aren't tabular everywhere.
4. **Navigation:** the left icon rail feels like a settings app, not an island. The pages don't swipe.
5. **Discoverability:** gestures (right-click dismiss, tilt to swap, scroll volume, shortcut) are invisible, and there's no first-run tour.
6. **Utility gap vs rivals:** no clipboard history, no quick command bar, no drop-to-convert, no "what did I miss" history, and no call awareness.
7. **Settings page:** a long list of toggles with no sense of what each moment looks like.

---

## Product pillars (what makes us different)

1. **Feels native, not bolted on.** Apple-grade motion, at zero idle CPU. Composition-thread animations only.
2. **Does things, not just shows things.** Command bar, drop actions, clipboard history, call controls.
3. **Private by design.** Fully offline, opt-ins clearly explained, nothing logged. Say it loudly in the UI.
4. **Teaches itself.** First-run tour on the real island, gesture hints, and a "Try it" gallery in settings.

### Unique features no rival has (all offline)
- **Island Command bar:** press the shortcut and type. `5m` or `timer 25` starts a timer; `= 12*8+3` calculates; `#3a7bff` shows a colour preview; `note buy milk` saves a note; `sw` starts the stopwatch; `lock`, `screenshot`, `mute`. Results come with a tiny live preview.
- **Call mode:** the island notices a call (mic plus camera in use by a call app, using data the privacy watcher already has). It shows a running call timer, a big mic mute toggle, camera status, and a "you're muted" pulse when you speak while muted (from the mic peak meter).
- **Drop actions:** drag files over the island and it splits into zones: *Shelf*, *Copy path*, *Zip*, *Convert image* (PNG/JPG/WebP via WIC), *Resize 50%*. Each zone glows as you hover it.
- **Recent moments ("Rewind"):** a page listing the last 30 moments (downloads, screenshots, copied links, devices) with their actions, so nothing you missed is lost. Kept in memory only.
- **Live audio visualiser:** the bars react to the real output level (WASAPI `IAudioMeterInformation` peak meter, polled only while music shows), instead of a fake loop.
- **Clipboard history (opt-in):** the last 25 text, link and colour items, with pin and search, keyboard friendly. In memory only, private copies skipped. It reuses `ClipboardWatcher` plus `CopiedTextClassifier`.

---

## Design system ("Flux Design Language")

New folder `src/FluxKit.App/Modules/FluxIsland/Design/`:

- **`IslandTokens.cs`**
  - Spacing on a 4 pt grid (4, 8, 12, 16, 20, 24).
  - Radii: compact 18, panel 28, chip 16, tile 14.
  - Type ramp in Segoe UI Variable:
    - Caption 11 / Body 13 / Title 15 Semibold.
    - Display 34 Semibold with tabular figures (`Typography.NumeralAlignment`).
    - Hero digits 44.
  - Opacity levels for text: 100 / 64 / 40.
- **`IslandPalette.cs`** (rework)
  - Surfaces: Onyx `#000`, raised `#16161A`, chip `#FFFFFF14`, pressed `#FFFFFF24`, hairline `#FFFFFF1A`.
  - Semantic accents: Focus orange, Charge green, Alert red, Info cyan, Media from the album art, Violet for system.
  - An `AccentFor(ActivityKind)` map so each moment has a fixed identity colour.
- **`IslandMaterials.cs`**
  - **Onyx:**
    - Solid black body.
    - A 1 px inner top highlight (gradient white 18% to 0) for the "edge light".
    - An outer ambient glow in the current accent, 0–24% opacity and breathing slowly for lasting moments.
    - A layered soft shadow.
  - **Glass (labs):** a desktop-acrylic body plus tint, a specular edge stroke, and a stronger highlight. See phase 9.
- **`IslandSprings.cs`:** named spring presets.
  - **Snappy** (damping 0.78, 38 ms): taps and chips.
  - **Smooth** (0.86, 55 ms): page changes.
  - **Bouncy** (0.62, 48 ms): moment arrivals.
  - **Morph** (0.74, 50 ms, with a 3% squash/stretch overshoot on the axis that's growing).
  - With reduced motion, everything becomes a 120 ms fade.

Every panel and compact view uses these tokens instead of literals.

---

## Engineering standards (apply to every phase)

These are from research on WinUI 3, the Windows App SDK and .NET 10 best practice, plus the repo's own `Docs/CodingStandards.md`, which stays the rule book.

**Modular and reusable**
- **Layers stay one-way:** App → Tool → Platform → Core.
  - New logic (`CommandParser`, `NotesModel`, `MomentHistory`, `ClipboardHistoryModel`, `CallDetector`) is plain C# in `FluxKit.Tools.FluxIsland/Activities`, with no XAML, no Windows calls, and unit tests using fakes.
  - Windows calls (peak meter, file transforms) go in `FluxKit.Platform` behind small classes.
  - XAML only renders.
- **One shared contract for panel pages:** `IIslandPage` (`Rows`, `SetInset`, `PreferredSize`, `Update(scene, now)`, `SetLive(bool)`). `IslandPill` treats every page the same, which removes today's long if/else chains.
- **Reusable design primitives in `Design/`:** tokens, springs, materials, `ProgressRingGlyph`, `PageHost`, `ChipButton` style, `Sparkline` and `BlurEffect`, each written once and used everywhere. No colour, size or duration literals in panels.
- **One public type per file;** files over about 300 lines get split. `IslandPill.xaml.cs` (about 1,000 lines) is split into partial classes by concern: `IslandPill.Pages.cs`, `IslandPill.Input.cs`, `IslandPill.Drag.cs`, `IslandPill.Bubble.cs`.
- **All text in `Resources.resw`.** Whole sentences as format strings.

**Lightweight (hard budget)**
- **Idle:** 0% CPU, under 60 MB working set, no timers running while nothing moves.
- **Animations run on the compositor thread:**
  - Composition springs and keyframes, never per-frame UI-thread code.
  - The only exceptions are bounded: the Glass region sync during a morph (≤ 500 ms) and the audio meter (only while bars are on screen, about 30 Hz, stopped otherwise).
- **Defer what isn't shown:** `x:Load="False"` for pages and heavy parts (Rewind, Notes, Command bar, drop zones), so they are created only when first opened.
- **No new heavy dependencies:**
  - No Win2D or Community Toolkit: one small in-house D2D blur effect description instead.
  - Image work uses the built-in `Windows.Graphics.Imaging`.
  - Zip uses the BCL.
- **Fixed-size buffers:** Rewind has 30 items, clipboard history 25, notes 20, sparklines 60 samples. Nothing grows without limit, and thumbnails are decoded small (48–96 px).
- **Trim/AOT-friendly code:**
  - New JSON stores use a `JsonSerializerContext` (source-generated), not reflection.
  - No `dynamic` and no reflection.
  - `{x:Bind}` where binding is used.
- **Fail soft:** every new source or watcher is optional, so one failing (no audio device, no image codec) never takes the island down. This follows the existing `Start<T>` pattern in `FluxIslandTool`.

**Quality gates (every phase)**
- `TreatWarningsAsErrors` and `AnalysisLevel latest-recommended` (already on) stay clean.
- `dotnet format --verify-no-changes` passes.
- New logic gets tests named as sentences.
- Screenshots of every new state are checked before committing.
- One-line plain-English commit, no co-author, no push.

---

## Phases

Every phase ends the same way:
- `dotnet build FluxKit.slnx`, `dotnet format --verify-no-changes`, `dotnet test`.
- Launch with `--preview-island <sample>` and screenshot the new states.
- Update the docs it touches.
- One one-line commit.

### Phase 1: Design tokens and materials
- Add `Design/IslandTokens.cs`, `IslandSprings.cs`, `IslandMaterials.cs`, and rework `IslandPalette.cs`.
- Restyle `IslandStyles.xaml`:
  - Chips: 32 high, radius 16, chip fill, a hover lift (+1 px and brighter), and a pressed state with a 0.96 scale through `PressFeedback`.
  - Icon buttons: 36 px round.
- `PillShape.cs` gains:
  - an edge-light layer (a second rounded-rect geometry with a `CompositionLinearGradientBrush` stroke);
  - an accent glow layer (a `CompositionDropShadow`-style blurred, tinted rect under the body) whose colour and opacity animate;
  - a richer shadow.
- New API: `SetAccent(Color, float intensity)` and `SetMaterial(IslandStyle)`.
- Replace literals in `IslandPill.xaml`, the panels and `IslandPresenter.cs` (colours via `AccentFor`).
- Commit: "Give Flux Island a design system with an edge light, accent glow and shared spacing and type"

### Phase 2: Motion system
- `IslandMotion.cs` rewrite: use the spring presets.
  - **Content transitions (Apple style):**
    - The outgoing content fades, scales to 0.94 and slides 4 px up.
    - The incoming content scales from 1.06 and fades in.
    - Both get a quick blur (a `LayerVisual` plus `CompositionEffectBrush` GaussianBlur, 8 down to 0 px) during the morph.
  - Blur needs effect descriptions. Add a tiny in-house `GaussianBlurEffectDescription : IGraphicsEffect, IGraphicsEffectSource` (D2D1 effect interop, about 60 lines) to avoid taking the Win2D dependency. If interop proves fragile, fall back to adding the `Microsoft.Graphics.Win2D` package.
- **Morph with squash and stretch** in `PillShape`: when growing wider the pill briefly over-stretches 3%; when shrinking it settles with a soft undershoot. Corner radius springs with the size (compact 18 to panel 28).
- **The bud:** when the second activity appears, the bubble grows out of the pill's right edge, joined by a shrinking "neck" for about 220 ms, so the two read as one liquid splitting. When it goes away they merge back.
  - The neck is a `CompositionRoundedRectangleGeometry` between the pill and the bubble whose height and width animate to 0.
- **Arrival flourishes per kind:**
  - Charge: the ring sweeps and the glow pulses green.
  - Timer done: the bell shakes and the island bounces.
  - Copy: the glyph flips.
  - Screenshot: the thumbnail drops in with a 4° tilt.
  - Device: the glyph slides in from the side.
  - Mic muted: the slash draws itself.
- **Hover magnetism:** the pill scales 1.03 and leans up to 2 px toward the pointer, using a `PointerMoved` throttle on the composition translation.
- Commit: "Give Flux Island smoother morphs, blur transitions and a liquid split for the second bubble"

### Phase 3: Compact and minimal layouts (Apple leading/trailing)
- Restructure `CompactView` in `IslandPill.xaml` into **leading** (glyph, art or ring, 24 px), **centre** (title over subtitle, only when there's room) and **trailing** (value, progress ring or bars).
- `CompactContent` gains `Progress` (0–1 or null) and `TrailingKind` (text, ring or bars).
- **Progress rings around the leading glyph** for timer, stopwatch lap progress, downloads and charge. Generalise `ChargeRing.xaml(.cs)` into `ProgressRingGlyph` (arc, glyph and colour).
- **Minimal mode:** a "minimal" bubble style when two lasting activities run, as in Apple's HIG.
- Idle pill: the clock with tabular digits, plus a tiny accent dot for a running background item (for example a stopwatch hidden behind music).
- Compact widths follow content, with token min and max values; the `RollingText` digits are used for every number.
- Commit: "Rebuild the compact island with Apple-style leading and trailing parts and progress rings"

### Phase 4: Panel redesign with swipeable pages
- Replace the left rail with **horizontal pages and a dot indicator** under the content (the iOS look):
  - Two-finger trackpad swipe or Shift+wheel moves between pages, through `PointerWheelChanged` horizontal deltas with snap springs.
  - Left and right arrows move between pages on the keyboard.
  - The dots are clickable.
- New `Design/PageHost.cs` lays the pages side by side and springs a translation.
- Panel size per page (all with radius 28):
  - Media 420×196
  - Timer 420×184
  - Shelf 420×156
  - Glance 420×184
  - Rewind 420×220
- **Media page:**
  - Large art (72) with soft multi-stop glow colours from the art (`AlbumArt.Accent` extended to give 2–3 colours via a simple k-means on a 16×16 downscale).
  - Title marquee if long.
  - A scrubber that thickens on hover, with a time bubble while dragging.
  - Live visualiser bars (phase 6) and a source app chip (for example "Spotify").
- **Timer page:**
  - Hero digits 44 with a circular progress dial (replacing the flat bar).
  - Quick chips 1/5/10/25 and stopwatch.
  - Laps shown in a small list with the best lap in green and the worst in orange.
- **Shelf page:** a tile grid with file-type thumbnails, a hover lift, multi-select, and "Drop here" zone styling (phase 7 adds actions).
- **Glance page:** rings drawn by the composition progress ring with gradient strokes, sparkline mini-charts (60 s history in a ring buffer while the page is visible), and quick actions in a pill-shaped toolbar.
- Commit: "Redesign the Flux Island panel with swipeable pages and new Media, Timer, Shelf and Glance layouts"

### Phase 5: Island Command bar (the unique headline)
- **`Activities/CommandParser.cs`** (pure, unit-tested) turns text into an `IslandCommand` such as:
  - `StartTimer(TimeSpan)`, `StartStopwatch`, `Calculate(double)`, `ShowColor(int rgb)`, `AddNote(string)`
  - `Lock`, `Screenshot`, `ToggleMic`, `OpenPage(page)`, `ClearShelf`
- Grammar:
  - `5m`, `90s`, `1h20`, `timer 10`
  - `=` followed by an expression: a safe recursive-descent calculator with + − × ÷ ^ % and parentheses, with no eval
  - `#hex` or `rgb(…)`
  - `note …`
  - verbs and their aliases
- **`Design/CommandBar.xaml(.cs)`:**
  - The shortcut opens the island as a 420×64 command field.
  - Typing shows a live result row (for example "Timer · 5:00 ▶", "= 99", or a colour swatch).
  - Enter runs it, Tab completes, Esc closes.
- The shortcut opens the command bar. Alt+arrow or clicking moves to the full panel. The old "open the panel" behaviour stays reachable with Down.
- **Notes:** `NotesModel` keeps up to 20 quick notes in `%LOCALAPPDATA%\FluxKit\notes.json` (opt-in "Keep notes"), on a new **Notes** page.
- Tests: `CommandParserTests` (about 40 cases) and `NotesModelTests`.
- Commit: "Add the Island Command bar for timers, sums, colours, notes and quick actions"

### Phase 6: Live audio visualiser and call mode
- **Platform:** `Audio/PeakMeter.cs` wraps `IAudioMeterInformation` for the default render and capture endpoints. It reuses the COM interop style in `Platform/Audio/CoreAudio.cs` and `EndpointVolume.cs`. It is read at about 30 Hz only while the bars or call mode are on screen, through a `DispatcherQueueTimer` that stops otherwise.
- **`PlayingBars`:** driven by the real level, with per-bar easing and an idle fall.
- **Call mode:**
  - New `ActivityKind.Call` (lasting).
  - `FluxIslandTool` raises it when the privacy source reports mic in use by a known call app (`PrivacyWatcher` already reads app names: extend it to return a `CallApp` flag for Teams, Zoom, Meet in a browser, Discord, Slack, WhatsApp and Skype, without storing names).
  - The compact view shows a green phone glyph and the call timer.
  - The expanded view shows a big mute toggle, camera status and the call time.
  - A "You're muted" nudge appears when the capture peak goes over the threshold while muted, at most once per 30 s.
- Tests: tool tests for the call start/stop lifecycle with `FakePrivacy`.
- Commit: "Add a live music visualiser and a call mode with a mute nudge to Flux Island"

### Phase 7: Drop actions
- While dragging files over the island it expands into a **zone strip**: Shelf · Copy path · Zip · Convert · Resize. Image-only zones are greyed out for non-images.
- **`Platform/Shell/FileTransforms.cs`:**
  - `ZipAsync` uses `System.IO.Compression`, writing next to the first file as "Archive.zip" with a unique name.
  - `ConvertImageAsync` and `ResizeImageAsync` use `Windows.Graphics.Imaging` `BitmapDecoder`/`BitmapEncoder`, writing a new file and never overwriting.
- Results arrive as a moment ("Zipped 3 files" with "Show in folder") and land on the shelf.
- Tests: `FileTransformsTests` with temp files for zip naming and image conversion on a small PNG.
- Commit: "Let files dropped on Flux Island be zipped, converted or resized, or have their path copied"

### Phase 8: Rewind and clipboard history
- **`Activities/MomentHistory.cs`:** a ring buffer of 30 `IslandActivity` items with timestamps, fed by `ActivityArbiter.Raise`. Smart-copy text is kept only when clipboard history is on.
- **Rewind page:** a time-grouped list ("Just now", "Earlier") with each moment's action chip; hover shows details.
- **Clipboard history** (opt-in setting `ClipboardHistory`, off by default):
  - `ClipboardHistoryModel` keeps 25 entries of text, link and colour, with dedupe and pins.
  - Paste-back copies the item to the clipboard again and shows a "Copied" moment.
  - It reads text only when opted in (`ClipboardWatcher` `readText`) and skips private copies, which already happens.
- Tests: `MomentHistoryTests` and `ClipboardHistoryModelTests`.
- Commit: "Add a Rewind page of recent moments and an opt-in clipboard history to Flux Island"

### Phase 9: Glass style (labs)
- Setting `Style`: `Onyx` (default) or `Glass`, under a "Labs" settings group.
- **Glass:**
  - `IslandWindow` sets `SystemBackdrop = new DesktopAcrylicBackdrop()` (or a `DesktopAcrylicController` with a custom tint and luminosity) instead of `TransparentBackdrop`.
  - The window region (`WindowHelpers.SetHitRegion`) clips it to the pill shape.
  - `PillShape` swaps the black body for a 10% white tint, a specular top stroke and a stronger edge light.
- **Shape sync:** during morphs the region must follow the spring. While a morph runs (≤ 500 ms) a `CompositionTarget.Rendering` handler reads the animated size from a `CompositionPropertySet` mirror and re-applies the region every frame; it stops after the morph settles. This keeps idle CPU at zero.
- **Spike first** (half a day): if the region and acrylic flicker or mismatch, fall back to "Glass" as a frosted dark tint (`#1C1C22` at 85%, no desktop blur) and say so in KnownLimitations.
- Commit: "Add a labs Glass style for Flux Island"

### Phase 10: Teach the user (onboarding and discoverability)
- **First-run tour on the real island:** after Flux Island is first turned on, the island plays a 25 s scripted demo through `IslandWindow.ShowPreview`:
  1. "Hi, I'm Flux Island"
  2. Music
  3. Hover hint
  4. Click hint
  5. Second bubble
  6. Right-click to dismiss
  7. The shortcut
- It can be skipped with Esc or a click, and replayed from settings. It uses a new `TourScript` sequence over `IslandSamples`.
- **Gesture hints:** the first time each gesture applies, a tiny caption shows under the pill once, for example "Scroll to change volume", "Right-click to dismiss" or "Click the bubble to swap". This is tracked in settings `SeenHints`.
- **Settings page "Studio" redesign** (`FluxIslandPage.xaml`):
  - A big live preview at the top with a **Moments gallery**: cards for every moment with a mini render and a **Try it** button that plays it on the real island.
  - Grouped feature cards with icons and one-line value statements: Now playing, Time, Files, System, Keyboard, Privacy, Labs.
  - A Privacy card listing exactly what is read.
- **Accessibility:**
  - Narrator announcements for alerts through `AutomationPeer.RaiseNotificationEvent`.
  - High contrast: a white 2 px border with no glow.
  - Every control named.
  - Reduced motion respected everywhere.
- Commit: "Add a first-run tour, gesture hints and a new Studio settings page for Flux Island"

### Phase 11: Smart quiet and polish
- **Presentation and screen-share awareness:** hide or dim during PowerPoint slideshow or Teams screen share (process plus full-screen checks already in `FullScreenWatcher`; extend the window class checks).
- **Priority rules** in settings ("What can interrupt"):
  - Everything
  - Important only (timer done, low battery, call, mic muted)
  - Nothing (lasting activities only)
- **Sound (opt-in):** soft clicks for timer done and charge, played with `Windows.Media.Playback` and bundled tiny WAVs.
- **Performance pass:**
  - Measure idle CPU (target 0%), memory (< 60 MB working set), and frame pacing during morphs.
  - Make sure every timer stops when hidden.
- Commit: "Add priority rules, quiet presentation mode and optional sounds to Flux Island"

### Phase 12: Docs, tests and final pass
- README Flux Island section with the new pillars and a feature table.
- CHANGELOG v3, Configuration (all new keys), Privacy (call detection, peak meter, notes, clipboard history, Rewind), KnownLimitations, and ManualTestPlan cases FI-25 onward.
- Add `--preview-island` samples for every new state: `call`, `command`, `drop-zones`, `rewind`, `notes`, `glass`.
- A full rebuild, format check and all tests; screenshots of every sample reviewed.
- Commit: "Update the docs for Flux Island v3"

---

## Critical files

- **Shape and motion:** `src/FluxKit.App/Modules/FluxIsland/PillShape.cs`, `IslandMotion.cs`, `IslandPalette.cs`, `IslandStyles.xaml`, new `Design/*`.
- **Pill, panels and settings:** `IslandPill.xaml(.cs)` (compact layout, page host, command bar, drop zones), the panels (`MediaPanel`, `TimerPanel`, `ShelfPanel`, `GlancePanel`), new `RewindPanel`, `NotesPanel`, `CommandBar`, and `FluxIslandPage.xaml(.cs)`.
- **Window:** `IslandWindow.xaml.cs` (Glass backdrop, region sync, tour).
- **Tool:** `src/FluxKit.Tools.FluxIsland/FluxIslandTool.cs`, `FluxIslandSettings.cs`, `Activities/` (new `CommandParser`, `NotesModel`, `MomentHistory`, `ClipboardHistoryModel`), `IslandActivity.cs` (`Call`), `ActivityArbiter.cs` (priority rules).
- **Platform:** `src/FluxKit.Platform/Audio/PeakMeter.cs` (new), `Shell/FileTransforms.cs` (new), `Watchers/PrivacyWatcher.cs` (call app flag).

## Reuse

- `ClipboardWatcher` (readText, private-copy skip) and `CopiedTextClassifier` for clipboard history and the command bar colour preview.
- `ShelfModel` and `JsonShelfStore` patterns for `NotesModel` and its store.
- `HotkeyWatcher` and `TogglePanel` for the command bar.
- `SessionActions` and `ShellLauncher` for command verbs.
- `SystemStats` for Glance sparklines.
- `RollingText`, `PressFeedback` and `AlbumArt`.
- The `IslandSamples` and `--preview-island` pipeline for screenshots and the tour.

## Verification

- **Each phase:** a clean build, the format check, and all unit tests (new parsers and models are fully unit-tested).
- **Visual:** launch `FluxKit.exe --background --preview-island <sample>[-open]` and screenshot the top of the screen (the same PowerShell capture used for Glance and both-open), then review every new state.
- **Performance:** idle CPU over 30 s (Process CPU time delta) must be about 0, and memory under 60 MB.
- **Manual:** ManualTestPlan cases cover what needs a person: a real call, drag-drop transforms, the command bar, the Glass style on two monitors, and the tour.
