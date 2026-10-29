# Flux Island: plan

Flux Island is a FluxKit tool: a small black pill at the top centre of the screen that shows what is happening right now (music, a timer, a charger plugged in, the mic in use) and opens into a panel with controls when you click it. It should feel soft and alive, and cost almost nothing when nothing changes.

The name is our own. "Dynamic Island" is Apple's name and is not used anywhere in the product.

## 1. What we build, and what we can't

FluxKit runs unpackaged, without admin rights, and never injects code into other apps. That decides what is possible.

| Feature | Feasible? | How | Phase |
|---|---|---|---|
| Idle pill with the time | Yes | One-shot timer that wakes once a minute | 2 |
| Hover swell, press squish, spring morph between sizes | Yes | Composition springs on the GPU | 2 |
| Hide in full-screen apps, games and presentations | Yes | `SetWinEventHook` (out of context) on foreground changes, then `SHQueryUserNotificationState` | 2 |
| Now playing: art, title, artist, play/pause, next, previous, seek bar | Yes | `GlobalSystemMediaTransportControlsSessionManager` events | 3 |
| Album-art glow colour | Yes | Average colour of the 64 px thumbnail, once per track | 3 |
| Charger plugged in / unplugged, low battery | Yes (laptops) | `PowerManager` events. Hidden on PCs without a battery | 4 |
| Offline / back online | Yes | `NetworkInformation.NetworkStatusChanged` | 4 |
| Mic and camera in use (privacy dots) | Yes | Registry change notifications on the CapabilityAccessManager consent store. App names are never shown or logged | 4 |
| "Copied" pulse | Yes | `AddClipboardFormatListener`. Only the kind (text, image, files) is checked; contents are never read | 4 |
| Timer and focus timer (25/5) with rolling digits | Yes | Runs inside FluxKit | 5 |
| File shelf: drop files on the island, drag them out later | Yes | The island is a real drop target. It keeps links to the files; the files are not copied or moved | 6 |
| Scroll over the island to change volume | Yes | Core Audio `IAudioEndpointVolume` (COM) | 7 |
| Messages, email and other apps' notifications | **No** | `UserNotificationListener` needs package identity and is off-limits for an unpackaged app | — |
| Calendar reminders | **No** | Needs package identity and calendar access | — |
| Focus / Do Not Disturb state | **No** | No public API | — |
| Wi-Fi, Bluetooth, airplane toggles, brightness | **No** (for now) | Radio APIs need capabilities; brightness needs WMI per monitor. Not worth it for v1 | — |
| AirPods-style device pop-ups with battery | **No** | Battery levels of Bluetooth devices are not exposed reliably | — |
| Real audio visualiser | Not in v1 | Would need a meter poll at 20+ fps. We show a GPU-looped "playing" indicator instead and call it that | — |

These limits go into `Docs/KnownLimitations.md` when the tool ships.

## 2. How it looks and feels

**Shape.** A pill drawn by the compositor (`CompositionRoundedRectangleGeometry`), near-black `#0B0B0F` with a 1 px inner highlight (white at 8 %) so it floats on any wallpaper. The corner radius is always half the height. A soft shadow is a second, larger, low-alpha shape, not a live blur.

**Sizes** (DIPs, all on the 4 px grid):

| State | Width × height |
|---|---|
| Idle | 120 × 32 |
| Compact activity (music, timer, alert) | 220–300 × 36 |
| Alert with icon and two lines | 320 × 52 |
| Expanded panel | 400 × 156 (shelf 400 × 132) |

**Motion.** Every change of size is a spring (`SpringVector2NaturalMotionAnimation`, damping 0.72, period 45 ms): it overshoots a little and settles. Content crossfades with a 6 px slide and a 40 ms stagger between rows. Hover swells the pill to 104 %; pressing a button squishes it to 94 % and springs back. Animations can be interrupted; a new target simply retargets the spring. With reduced motion (the Windows setting, or the tool's **Light** effects mode), springs become 120 ms fades and nothing overshoots.

**Small touches.**
- Digits of timers roll like a counter, one digit at a time, with tabular numerals.
- The charger animation: a ring sweeps around a bolt icon, then the percentage rolls in.
- "Copied": a 1.2 s pulse with a glyph for text, image or files.
- Mic and camera: an orange and a green dot on the right of the pill for as long as they are in use.
- Music: a "playing" indicator of three bars looped on the GPU, and a glow in the album's colour on the panel.
- A file dragged over the island makes it open into a shelf with a glowing drop zone.

**Behaviour.** Hover for 350 ms to peek (the pill grows to show its second line). Click to open the panel. Moving away for 600 ms, or clicking elsewhere, closes it. The island never takes focus from the app you are using.

## 3. How it stays light

- **Nothing runs when nothing changes.** Every source is an event. The clock wakes once a minute. The seek bar ticks once a second only while the panel is open and music plays. The timer ticks once a second only while it runs.
- **The GPU animates.** Springs, fades and the playing bars run on the compositor. Layout only changes when the content changes.
- **Click-through.** The window is a fixed transparent overlay; its window region is the pill's current bounds, so clicks outside the pill reach the app below.
- **Small images.** Album art is decoded at 64 px (compact) and 112 px (panel), only the current track is kept.
- **Full screen pauses everything.** In games and presentations the window is hidden and no updates are drawn.
- **Targets:** idle CPU about 0 %, under 2 % while animating; FluxKit's working set grows by less than 20 MB with the island on.

## 4. Code layout

Same split as Drag-to-Switch: logic in a tool library without XAML, Windows APIs in Platform, views in the App.

```
src/FluxKit.Tools.FluxIsland/
  FluxIslandTool.cs            IFluxTool: starts sources, feeds the arbiter, drives the view
  FluxIslandSettings.cs        ISettingsSection ("utilities/flux-island")
  Activities/
    IslandActivity.cs          what can be shown (kind, priority, text, expiry)
    ActivityArbiter.cs         picks what the pill shows; alerts pre-empt for a few seconds
    FocusTimer.cs              timer and focus/break cycle on TimeProvider
    BatteryAlertPolicy.cs      when to raise charging and low-battery alerts
    ShelfModel.cs              the files on the shelf (paths only)
  Abstractions/
    IIslandView.cs, IMediaSource.cs, IPowerSource.cs, INetworkSource.cs,
    IPrivacySource.cs, IClipboardSource.cs, IFullScreenSource.cs, IVolumeControl.cs
src/FluxKit.Platform/
  Media/MediaSessionWatcher.cs
  System/PowerWatcher.cs, NetworkWatcher.cs, PrivacyWatcher.cs, FullScreenWatcher.cs
  Shell/ClipboardWatcher.cs
  Audio/EndpointVolume.cs
src/FluxKit.App/Modules/FluxIsland/
  IslandWindow.xaml(.cs)       transparent top-centre overlay and its window region
  IslandPill.xaml(.cs)         the pill and all its states; also used by the page preview
  PillShape.cs                 composition geometry, shadow and springs
  RollingText.xaml(.cs)        counter-style digits
  FluxIslandPage.xaml(.cs)     settings page with a live preview
tests/FluxKit.FluxIsland.Tests/
```

## 5. Settings

| Setting | Default |
|---|---|
| Show music | On |
| Show charger and battery alerts | On |
| Show offline and back-online alerts | On |
| Show when the mic or camera is in use | On |
| Show "Copied" | On |
| File shelf | On |
| Scroll over the island to change volume | On |
| Hide in full-screen apps | On |
| Show the time when idle | On |
| Effects: Full or Light | Full (Light is used when Windows animations are off) |

## 6. Phases (one or more commits each; build, format check and all tests after each)

1. **Plan and skeleton.** This document. Tool library, settings, activity model, arbiter, focus timer, policies and their unit tests. Register the tool and add its page.
2. **The island window.** Transparent overlay, window region, DPI and monitor placement, compositor pill with springs, idle clock, hover and press, hide in full screen.
3. **Music.** Media session watcher, compact and panel views, controls, seek bar, art glow.
4. **System moments.** Charger and battery, network, privacy dots, "Copied".
5. **Timers.** Quick timers in the panel, focus cycle, rolling digits, completion burst.
6. **Shelf.** Drop files, list, drag out, clear.
7. **Volume.** Scroll to change volume, inline level bar.
8. **Polish and docs.** Settings page with live preview, reduced motion check, performance check (CPU and memory with the island idle and animating), README, Configuration, KnownLimitations, CHANGELOG.

## 7. Testing

- Unit tests (automated): settings normalisation, arbiter priority and expiry, focus timer, battery alert policy, shelf model, media position maths.
- Checked by running the app (screenshots without synthetic input): every pill state on a real desktop, light and dark wallpapers, the settings page.
- Manual only (the user must run these, and results are recorded only when they were run): playing music in a real player, plugging in the charger, joining a call (mic dot), dropping files on the shelf, scrolling for volume, a full-screen game.
