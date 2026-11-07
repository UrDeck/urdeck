## Why

`MainWindow` and the views under it (`PagerController`, `PageHost`, `WidgetView`) both decide what the deck shows and
show it in XAML. Drawing the deck anywhere else, first on a specialized display (`specialized-display-output`, see
`docs/handoff/2026-10-31-specialized-displays.md`), would mean copying that logic. This change separates the two
halves without changing behaviour, so a second way of presenting the deck can reuse the first half, and so the
deck's decisions get tests that run on Linux. The owner can verify it on the panel as a desktop monitor.

**Amended 2026-11-06 (editor batch):** this change is no longer gated on `specialized-display-spikes`. The editor's
page preview (`editor-shell`) is a second presenter of a deck session in another window, so the editor needs this
refactor first; `specialized-display-output` still waits for the spikes' go. It is now a prerequisite of
`editor-shell` and can be applied in parallel with `settings-metadata` and `config-document`. Design D4 lists what a
second, in-process presenter adds to the session. **Model: Opus** (a behaviour-preserving refactor of the deck's core,
hard to debug).

## What Changes

- A **deck session** that knows nothing of XAML: it reacts to configuration, theme and plugin reloads, resolves the
  theme and the background plan, decides the motion level, keeps the current page and the pager's state, computes
  the layout for a given surface (size and scale), owns the widget instances of the pages in use and their reading
  subscriptions, refresh timers, animation flags and taps, and says which widgets need to be painted.
- A **presenter seam**: the session asks a presenter for its surface and whether it is shown, and tells it what to
  draw. The only presenter in this change is the existing window, rebuilt on the seam: one `SKXamlCanvas` per widget,
  the background layer, the pattern layer, the indicator, composition offsets for the swipe.
- Placement stays with the window presenter (`PlacementController` is the window's way of having a surface).
- The session supports more than one instance in a process, so the editor's preview can be a second presenter (design
  D4).
- No user-visible change: same pictures (goldens unchanged), same log lines, same idle cost, same behaviour for
  sleep, display changes, pages, taps, hot reload and the settings window.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

None: behaviour is unchanged (`skip_specs: true`). The requirements in `host-shell`, `page-navigation`,
`widget-input`, `motion` and `display-targeting` are what the refactor must keep.

## Impact

- `src/UrDeck.Engine`: a `Deck/` folder with the session, the widget slot (one widget's lifetime, subscriptions,
  refresh and tap state), the pager state, and the scheduling seam the session uses instead of `DispatcherQueue`.
  Types that move there from the host (`MotionGate`, parts of `WidgetView`, `PageHost` and `PagerController`) stay
  internal to the engine and the host (`InternalsVisibleTo` as today), not SDK.
- `src/UrDeck.Host`: `MainWindow` becomes the window presenter plus placement; `WidgetView`, `PageHost` and
  `PagerController` keep only what is XAML (surfaces, composition offsets, pointer events turned into samples).
- `tests/UrDeck.Engine.Tests`: session tests with a fake presenter and a fake scheduler (reload, page change,
  hidden deck releases widgets, repaint on reading change, motion gate, plugin reload drops old instances).
- No SDK change, no new package, no configuration change. Engine coverage floor still met.
