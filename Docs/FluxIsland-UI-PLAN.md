# Flux Island UI/UX plan: from "works" to "wow"

v3 gave the island the right features and a motion system. It still looks generic: thin grey icons, small type, grey chips and flat rows. This plan is only about how it looks and feels. It is specific down to sizes, colours, springs and every micro-interaction, so each part can be built and checked against it.

## 1. What the research says

**Trends that hold up in 2026** (agency surveys, Apple Liquid Glass, design-engineering practice):
- **Purposeful motion.** Micro-interactions are signals ("that worked", "this is live"), not decoration. Keep them short, tied to a trigger, interruptible, and fully off under reduced motion.
- **Motion tokens.** Durations, easings and springs live in the design system like colours do. We have them (`IslandSprings`) and will use them strictly.
- **Depth through layers, not borders.** Translucent and tinted layers separate content, especially on dark UI. Avoid outlines and dividers.
- **Restrained glass.** It's used surgically, never everywhere.
- **Bento grouping.** Information sits in soft rounded tiles of different sizes, and live tiles animate their content.
- **Anti-pattern.** Kinetic gradients and stacks of effects look good in a review and tire people in daily use. Calm by default; alive when something happens.

**iOS Dynamic Island and Live Activities** (Apple HIG, WWDC23 "Design dynamic Live Activities", WWDC23 springs, WWDC18 "Designing Fluid Interfaces"):
- **Black, always.** The island is black with white text. Identity comes from colour in the content (icons, numbers, progress), never the background.
- **Compact is leading plus trailing.** One glanceable thing at each end, like an icon on the left and a big coloured value on the right. Detail lives in the expanded view.
- **Expanded keeps relative placement.** What sits left when compact sits left when expanded; it just grows. Use rounded, thick shapes and a liberal use of colour.
- **Minimal.** With two activities, the second becomes a small circle holding just an icon or a ring.
- **Physics, not curves.** Springs are the only animation that stays continuous when interrupted. Everything is interruptible and redirectable: a new state starts from wherever the old one is now.
- **Restraint.** Don't cram. One idea per moment, with big numbers and few words.

**Design-engineering detail** (Emil Kowalski, Family's "Family Values", Rauno-style interaction work):
- Press to 0.97 (we use 0.94: too much). Enter from at least 0.95 scale, never from 0. UI motion under about 300 ms. A 2 px blur hides imperfect motion (we can't blur cheaply; we lean on scale and opacity instead).
- **Continuity.** The app is one space. Elements that persist across states move rather than disappear and reappear (the art, the number, the ring).
- **Delight in the small things.** Chevrons that morph, a checkmark that draws, a deleted item that tumbles out, a sound or flourish on completion. Each is used once, where it means something.

## 2. Principles for Flux Island

1. **One glance, one idea.** Compact shows one icon and one value. Never a sentence.
2. **Colour is identity, black is the stage.** Each moment owns one colour: its badge, numbers, ring, glow and buttons. Everything else is white, grey or black.
3. **Big numbers win.** Times, percentages and levels are the largest thing in any state, in heavy rounded figures that never jiggle.
4. **Everything persists, nothing pops.** Opening the island moves the icon, art, number and ring to their new places; they don't vanish and reappear.
5. **Physical and interruptible.** Every animation is a spring that can be caught mid-flight.
6. **Calm, then alive.** At rest nothing moves. Live things (music, a running timer, a call) carry one gentle motion. Arrivals get one flourish and then settle.
7. **Every action answers.** Hover, press, success and failure each get a visible response within 100 ms.
8. **Accessible by default.** Reduced motion means fades only. High contrast means outlines. Every control has a name, and focus is always visible.

## 3. Visual system v2

### 3.1 Type
Segoe UI Variable is built into Windows 11, so it ships nothing. Its **Display** optical size is the "SF" equivalent for big numbers. Numbers use **tabular figures** (`Typography.NumeralAlignment="Tabular"`) so they don't jiggle as they change.

| Token | Font, weight | Size / line | Use |
|---|---|---|---|
| `Hero` | Variable Display, Bold | 44 / 48, tracking −3 % | Timer and stopwatch digits, call time (expanded) |
| `Display` | Variable Display, Semibold | 28 / 32, tracking −2 % | Percentages, Glance values, big results (command) |
| `Title` | Variable Text, Semibold | 16 / 20 | Song title, moment titles (expanded) |
| `Compact` | Variable Display, Semibold | 15 / 20 | Compact pill text and values (iOS uses 15/22) |
| `Body` | Variable Text, Regular | 13 / 18 | Artist, details, list rows |
| `Caption` | Variable Text, Semibold, ALL CAPS, +6 % tracking | 10 / 14 | Section labels ("FOCUS · ROUND 1", "NOW PLAYING") |

### 3.2 Colour
- **Stage:** `#000` body. Raised tiles `#1C1C1E` (iOS secondary system background, dark). Pressed `#2C2C2E`. Hairlines are not used.
- **Text:** primary `#FFFFFF`, secondary `#EBEBF5` at 60 %, tertiary `#EBEBF5` at 30 % (iOS label colours).
- **Identity colours:** iOS dark-mode system colours, so they feel native next to an iPhone.
  - Orange `#FF9F0A`, Green `#30D158`, Red `#FF453A`, Blue `#0A84FF`
  - Teal `#64D2FF`, Indigo `#5E5CE6`, Pink `#FF375F`, Yellow `#FFD60A`, Purple `#BF5AF2`
- **Tints:** every identity colour has a tint, the colour at 22 % on black. It's used behind badges and tinted buttons, like iOS's orange pause button on a dark-orange circle.
- **Gradients, used sparingly:**
  - Progress rings get a two-stop gradient along the stroke: the colour at 100 %, then lighter at the tip.
  - The music panel gets an art wash (3.6).
  - Nothing else.

### 3.3 Shapes and spacing
- **Corners:** compact pill radius = height / 2. Panel radius 32, tiles 18, buttons fully round. Inner corners follow concentric radii (inner = outer − padding).
- **Grid:** 4 px. Panel padding 20. Gaps 12 between tiles and 8 inside them.
- **Compact height:** 36. Leading and trailing items are 24 × 24, inset 6 from the ends (so a 24 px badge sits concentric to the pill end).

### 3.4 Icons
- **Badges.** A glyph centred in a filled circle of its tint, with the glyph in the identity colour. Compact badges are 24; expanded badges are 40, or 52 for heroes. This is what makes moments read like iOS apps rather than Windows Settings.
- **Filled glyphs where Segoe Fluent has them:** PlaySolid `F5B0`, PauseSolid `F8AE`, solid stop, solid mic and bolt. Outline glyphs stay only for secondary actions.
- **Icon life** (section 5, I-series):
  - The mic pulses while live.
  - The bell rings.
  - The bolt flashes once.
  - The download arrow drops once.
  - The checkmark draws itself.

### 3.5 Buttons
- **`TintButton`:** round, 44 (expanded) or 32 (rows). The background is the moment's tint and the glyph is the identity colour. Used for the main action of a moment: pause the timer (orange), mute in a call (red when muted), play (white on `#2C2C2E` for music, iOS-style).
- **`GhostButton`:** round, transparent, white glyph at 60 %, with a hover fill of `#2C2C2E`. For secondary actions (×, skip, more).
- **`PillChip`:** height 30, fill `#1C1C1E`, text 13 Semibold. For choices (1 min, 5 min, Focus 25).
- **States:**

  | State | Look |
  |---|---|
  | Hover | +6 % brightness on the fill |
  | Press | Scale 0.97 (Snappy) and −4 % brightness |
  | Focus | 2 px white ring, 2 px outside |
  | Disabled | 35 % opacity |

### 3.6 Media colour wash
- The music panel's background is the album art itself, decoded at **8 × 8 pixels and stretched to fill**. Smooth scaling turns it into a soft blur of the album's colours at no cost (no blur effect, no Win2D).
- Over it sits a black-to-transparent gradient, so text stays readable (contrast checked: white text stays above 4.5:1 on the darkest 40 %).
- The wash cross-fades (400 ms) when the track changes.

## 4. Every surface, specified

### 4.1 Compact pill (one activity)
Anatomy: `[leading 24] — gap — [title, optional] — flexible — [trailing value]`, height 36. Rules:
- **Leading:** a badge (24), art (24, radius 6), or a ring badge (an icon inside a progress ring). Never a bare grey glyph.
- **Trailing:** the value in `Compact` Semibold in the identity colour. Live items get one motion (bars, a pulse).
- **Middle text:** only when the value needs a label. Max about 14 characters, white at 60 %.
- **Width:** content-sized between 160 and 300. The pill springs to its new width, and its text cross-fades.

| Moment | Leading | Middle | Trailing |
|---|---|---|---|
| Music | Art 24 (or pink note badge) | Title, scrolling if long | Live bars (pink or the art's colour) |
| Timer / Focus | Orange ring badge (ring empties) | — | **18:40** orange, tabular |
| Stopwatch | Orange ring badge (seconds hand) | — | **1:26** orange |
| Call | Green phone badge (red while muted) | — | **12:36** green (red when muted) |
| Charging | Green bolt in a ring that sweeps to the level | "Charging" | **76 %** green |
| Low battery | Red battery badge (shakes once) | "Low battery" | **12 %** red |
| Copied | Indigo copy badge (flips) | "Copied" or the link host | Kind ("Link") 60 % |
| Volume / Brightness | White badge | Level bar (130 × 6, white fill) | **64** |
| Download | Blue arrow badge (arrow drops) | File name | **148 MB** blue |
| Screenshot | Thumbnail 24 (drops in with a tilt) | "Screenshot" | — |
| Device / Audio | Teal badge (slides in) | Device name | — |
| Mic muted | Red mic-slash badge (slash draws) | "Mic muted" | Unmute button on peek |
| Desktop | Indigo badge | Desktop name | **2/4** |
| Idle | — | **16:24** in `Compact`, centred | Privacy dots |

### 4.2 Two at once (minimal)
- The secondary sits in a 36 px circle beside the pill (we have this). It shows only its badge or ring, never text.
- Timers show their ring in the bubble, so time is still readable at a glance.
- The bud animation from v3 stays.

### 4.3 Peek (hover)
- Height grows to 56 and a detail line appears: artist, "Full in 45 min", the file name.
- The trailing action becomes a `TintButton` (32) in the moment's colour ("Unmute", "Open", "Show").
- Leading and trailing stay where they were (continuity).

### 4.4 Expanded pages (420 wide, radius 32, padding 20)
Each page has a **hero row**: badge or art at left, `Caption` label over the main text, and a hero number or control at right. Below it are **bento tiles** or controls. The page dots stay at the bottom.

- **Music:**
  - Art wash background.
  - Art 72 (radius 16, soft shadow), with `Caption` "NOW PLAYING · Spotify" above `Title` and the artist below.
  - Live bars at top right.
  - The scrubber is 4 px, grows to 8 px with a 14 px white knob on hover, and shows a time bubble above the knob while dragging. Elapsed and remaining are tabular.
  - Controls: ⏮ (ghost 44), ▶/⏸ (white 56 circle with a black glyph), ⏭ (ghost 44).
- **Timer (idle):**
  - `Caption` "TIMERS" with the clock (`Display` 28) and date at top right.
  - A bento row of four tiles (1, 5, 10 min and Focus 25), each 86 × 64 on `#1C1C1E` with the number in `Display` and the unit in `Caption`.
  - Stopwatch as a ghost button.
- **Timer (running), the iOS timer layout:**
  - Left: two `TintButton`s 44 (pause orange, × grey). Right: the `Hero` digits in orange, with `Caption` "FOCUS · ROUND 1" above.
  - The dial moves into the leading badge position (continuity with compact).
  - A +1 min chip.
  - Laps in a tile list: the best in green and the slowest in orange, with the lap number at 60 %.
- **Shelf:**
  - Tiles 84 × 84 on `#1C1C1E`, radius 18, with a 40 icon or thumbnail and a 2-line name.
  - Hover lifts the tile (scale 1.03, raised shadow). The empty state is a dashed tile with "Drop files here".
- **Glance (bento):**
  - A 2 × 2 grid of tiles: CPU, Memory, Disk, Battery. Each has a 44 gradient ring, the value in `Display`, and a `Caption` label.
  - A full-width network tile with ↓ and ↑ in `Body` and a tiny live sparkline (60 samples).
  - A row of round ghost buttons for quick actions.
- **Notes / Recent / Clipboard:**
  - Rows 44 high, radius 12, on hover fill `#1C1C1E`.
  - Leading badge 28, text `Body`, trailing time `Caption`. Row actions appear on hover only (iOS swipe-action feel).
  - Removing a row collapses its height and fades it out (no jump).
  - The empty states have an illustration-style badge (52) and one sentence.
- **Command bar:**
  - 420 × 56 input row: a magnifier badge, the text in `Title`, and "esc" in a key-cap style.
  - The result row is a tile with a badge (40, the result's colour), the result in `Display` (e.g. "= 99"), the detail in `Body`, and a ↵ key cap.
  - While typing, the result tile cross-fades between kinds. The badge colour springs, and the number rolls (RollingText).
- **Drop zones:**
  - Five tiles 72 × 84, each with a 40 badge in its colour.
  - The hovered tile lifts (1.06), its badge fills solid, and the others dim to 60 %.
  - Dropping plays a "suck in" (the tile shrinks to 0.9 and springs back), then the result moment.

### 4.5 Hint chip and tour
- A `#1C1C1E` capsule with a 1 px top highlight, and a 16 badge with "?" or the gesture icon.
- It enters from y −6 with a fade and 0.96 scale.
- The tour adds dots that show the step (●○○○○) inside the chip.

## 5. Micro-interaction catalogue
Every item: the trigger, what moves, the spring or duration, and the reduced-motion fallback.

| ID | Trigger | Motion | Spec | Reduced motion |
|---|---|---|---|---|
| M1 | Hover pill | Scale 1.03, lean toward pointer ≤ 3 px | Smooth | none |
| M2 | Press anything | Scale 0.97, −4 % brightness | Snappy | brightness only |
| M3 | Release | Spring back to 1.0 with a slight overshoot | Bouncy | instant |
| M4 | Open the panel | Shape morph; leading item and value travel to their expanded spots; other rows stagger in (40 ms) from 0.96 scale and 0 opacity | Morph, stagger 40 ms | 120 ms fade |
| M5 | Close the panel | Reverse of M4, rows out in 90 ms | Morph | fade |
| M6 | Change page | Slide ±28 px with fade; dots: the active pill stretches (16↔6) | Smooth | fade |
| M7 | Number changes | Changed digits roll vertically, unchanged stay | 220 ms ease-out | swap |
| M8 | Ring progress | Arc springs; the tip has a 3 px glow dot | Smooth | jump |
| M9 | Moment arrives | Glow flare 0→1→rest, badge pop 0.6→1.12→1 | 420 ms | none |
| I1 | Mic live / call | Badge breathes (scale 1↔1.06, 1.6 s) while live; stops on mute | keyframes loop | static |
| I2 | Timer done | Bell rings ±18° decaying, ring burst ×2, pill bounce | 640 ms | static |
| I3 | Charger in | Ring sweeps 0→level, bolt flashes once (opacity 1→0.3→1) | 700 ms | static ring |
| I4 | Download done / file ready | Arrow drops into a tray; checkmark draws (trim 0→1) | 360 ms | static ✓ |
| I5 | Copied | Badge flips (scaleX 0→1.12→1) | 380 ms | none |
| I6 | Mic muted | Slash draws across the mic (trim) | 240 ms | static |
| I7 | Device connects | Badge slides in from the left with overshoot | Bouncy | none |
| I8 | Scrubber hover | Track 4→8 px, knob 0→14 px | Snappy | instant |
| I9 | Scrub drag | Time bubble follows the knob; music time updates live | per-frame | same |
| I10 | Row remove | Height 44→0, opacity 1→0, neighbours slide up | 200 ms | instant |
| I11 | Pin | Pin icon rotates −30° and fills yellow | Bouncy | colour only |
| I12 | Drop hover | Tile lifts 1.06, badge fills, others dim | Bouncy | colour only |
| I13 | Command result change | Tile cross-fades; badge colour springs; number rolls | 200 ms | swap |
| I14 | Command success | Tile shows a ✓ that draws, a green flash, then closes | 360 ms | ✓ |
| I15 | Bubble split / merge | Neck thins (v3 bud) | 320 ms | none |
| I16 | Error (file job failed) | Pill shakes horizontally ±6 px ×3, red flash | 360 ms | red flash |
| I17 | Hint appears | Slide from −6 px, fade, 0.96→1 | Smooth | fade |
| I18 | Swipe beyond the last page | Rubber-band 12 px and back | Bouncy | none |

**Budget:** at most one looping motion at a time (I1 or the music bars), and none at idle. Loops run on the compositor and stop as soon as their element is hidden.

## 5b. UX details that remove friction
- **Hit targets** ≥ 32 px, even when the visual is smaller (dots get 24 px targets).
- **Hover intent:** peek after 350 ms, close 600 ms after leaving. The panel never closes while a menu or drag is active (already done).
- **Undo:** Clear (notes, clipboard, recent, shelf) shows an "Undo" chip for 4 s instead of a confirm dialog.
- **Feedback for silent actions:** copy path, copy result and pin each confirm with I14 and a short title.
- **Empty states** say what to do ("Drop files here", "Copy something and it shows here"), never just "Nothing".
- **Truncation:** long titles marquee once on hover (8 s loop, paused at the ends) instead of a hard ellipsis.

## 6. Building blocks (code)
These new reusable controls go in `Modules/FluxIsland/Design/`. They're built once and used everywhere:
- `IslandType`: text styles from 3.1 as XAML styles, with tabular numerals.
- `IconBadge`: a glyph in a tinted circle. Properties: size, colour, glyph, filled or tinted, a pulse (I1), and a draw-in check (I4).
- `TintButton`, `GhostButton`, `PillChip`: styles plus a shared `Interactions.Attach(element)` that does M2/M3, the hover brightness and the focus ring.
- `BentoTile`: a rounded `#1C1C1E` tile with a hover lift.
- `ArtWash`: the 8 × 8 art background with a cross-fade.
- `Scrubber`: a track with hover growth, the knob and a time bubble.
- `ProgressGlyph` gains a gradient stroke and a tip dot (M8).
- `RollingText` gains tabular numerals and a size parameter.
- `Marquee`: scrolls long text once on hover.

## 7. Phases (build, test, screenshot every state, one commit each)
1. **Type, colour and buttons:** the v2 tokens, `IslandType` styles, `IconBadge`, button styles, `Interactions` (M1–M3). Restyle every page with no layout changes yet.
2. **Compact pill:** the anatomy in 4.1 for every moment, bubble rings (4.2), peek with `TintButton` (4.3), and icon life I1–I7.
3. **Music page:** `ArtWash`, art 72, `Scrubber` (I8, I9) and the new controls.
4. **Timer page:** the bento quick timers, the iOS running layout, hero digits and lap tiles.
5. **Glance:** the bento grid, gradient rings and sparkline.
6. **Lists, shelf, command bar and drop zones:** new rows, I10–I14, the undo chip, and empty states.
7. **Continuity (M4/M5):** the leading item and value travel between compact and expanded.
8. **Polish pass:** I16–I18, marquee, the hint chip with tour dots, contrast checks, reduced motion and high contrast across every state, and a performance check (idle CPU is still 0).

## 8. How we check it
- Screenshot every sample (`--preview-island`) before and after each phase, and compare side by side against 4.1–4.4.
- **Contrast:** primary text ≥ 7:1 on black and ≥ 4.5:1 on the art wash, checked with the colour values.
- **Motion:** each M or I item is checked live once, plus the reduced-motion fallback (Windows animation effects off).
- **Idle CPU:** 0 over 30 s. No looping animation runs while hidden.

## Sources
- Apple HIG, Live Activities: https://developer.apple.com/design/human-interface-guidelines/live-activities
- WWDC23 "Design dynamic Live Activities": https://developer.apple.com/wwdc23/10194
- WWDC23 "Animate with springs": https://developer.apple.com/wwdc23/10158
- WWDC18 "Designing Fluid Interfaces": https://developer.apple.com/videos/play/wwdc2018/803/
- Infinum, designing for the Dynamic Island: https://infinum.com/?p=28092
- Emil Kowalski, "Great animations": https://emilkowal.ski/ui/great-animations and "7 practical animation tips": https://emilkowal.ski/ui/7-practical-animation-tips
- Benji Taylor, "Family Values": https://benji.org/family-values
- 2026 trend round-ups: https://www.mindinventory.com/blog/mobile-app-ui-ux-design-trends/, https://midrocket.com/en/guides/ui-design-trends-2026/, https://www.wavespace.agency/blog/ui-design-trends
