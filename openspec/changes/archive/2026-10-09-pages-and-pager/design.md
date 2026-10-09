## Context

See `proposal.md` for the motivation and scope. What the code does today, and what constrains the approach:

- `UrDeckConfig` already has `pages[]` and `activePage`. `MainWindow.RebuildLayout` builds `CurrentPage` only, and every
  config, theme, plugin or placement change disposes all views and rebuilds. There is no runtime page state.
- Widget surfaces are `WidgetView : SKXamlCanvas`, placed on one `Canvas`. A view subscribes to its readings on
  `Loaded` and unsubscribes on `Unloaded`/`Dispose`. `ReadingHub.Subscribe` is refcounted; the last unsubscribe starts a
  5 second provider linger; a re-subscribe restores the reading as `Pending(lastValue)`. So "a page that is not alive costs
  nothing" and "a returning page shows the last value" are already true; this change relies on them and does not alter
  the hub.
- `FrameClock` is keyed to `WidgetView` and drops a view only when its paint reports it is not animating, so a view that
  is merely hidden would stay in it. Pages that are not shown are therefore disposed, not paused.
- `--snapshot` runs on the engine alone (no XAML), through `PageRenderer`. Anything the user should see in a snapshot
  has to be drawn by the engine.
- The grid is square cells at `width / 4`. On 1100x3840 that is 13 whole rows and 265 px left over, which a bottom
  band can use without costing a row. The window has no `WS_EX_NOACTIVATE` today; the owner confirmed that a tap on the
  panel takes focus.
- Framework code stays inside `UrDeck.Host`; logic that can be tested without WinUI goes into `UrDeck.Engine`, where the
  line-coverage floor applies. The SDK is not touched.

## Goals / Non-Goals

**Goals:**
- A page change that follows the finger, with a bounce at the ends, whose only steady-state cost is one page.
- Everything a snapshot should show (page N, the band, the indicator) comes from the same engine code as the window.
- Decisions about gestures, layout and mode are pure and unit-tested; the host is a thin translation layer.

**Non-Goals:**
- Widgets receiving touch, and an SDK input API. A tap is recognised, but only the indicator uses it.
- Swiping over a hosted web view, per-page backgrounds, the dock, a motion level, and keyboard navigation.
- A general feature-flag mechanism. Chrome elements have one setting each with an `auto` value.

## Decisions

### 1. The window never takes focus

Set `WS_EX_NOACTIVATE` on the window's extended style and subclass the window procedure to answer `WM_MOUSEACTIVATE`
with `MA_NOACTIVATE`. `WS_EX_TOOLWINDOW` is not set. The owner no longer sees a taskbar button for the window (it vanished in an earlier change, cause not found) and does not need one: the tray icon (milestone 2) is the intended way to close it.
Escape stops working because a window that is never active gets no keyboard input; the owner accepts this until the tray
icon (milestone 2). `URDECK_ACTIVATABLE=1` skips the style and the subclass so a developer keeps Escape.

Alternatives: showing the window with `SW_SHOWNOACTIVATE` only (does not stop a later click from activating it); a global
hotkey for quit (more surface for a stop-gap); forcing the previous foreground window back on activation (visible
flicker, a last resort only).

Unknown, and the first task checks it on the panel: WinUI 3 puts its content in a child window that can take focus when
pressed, which may activate the top-level window despite the style. If so, the subclass must also cover the child, or the
last resort above is used.

Known gap: a future web view with text entry needs keyboard focus, which this window never has.

### 2. Current page is host state, held by a small engine type

A `PageNavigator` in the engine holds the page count, the current index and the current page's name. It is created from
the configuration (`activePage`, clamped) and has one operation for a configuration reload: keep the page with the same
name (the first one if names repeat), otherwise the same index clamped. The host asks it which page is current and tells
it when a page change has settled. Pages have names but no id, and the owner's file is hand-edited; a rename of the page
you are on moves you to the same index, which is acceptable. A page id is not added now: it would be a config format
change before there is a format version (backlog item 15).

Swiping never calls `ConfigStore.Save`. `activePage` is only read at startup.

### 3. One gesture recogniser in the engine

`GestureRecognizer` takes pointer samples (id, phase down/move/up/cancel, position in the surface's pixels, a timestamp)
and emits events: swipe started (with the direction), swipe moved (dx), swipe ended (dx and velocity), tap (position),
cancelled. It has no timers and reads no clock: durations come from sample timestamps, so tests feed a script of
samples. The host turns `PointerPressed/Moved/Released/Canceled` and `PointerCaptureLost` into samples and takes them at
the pager's root, with pointer capture while down. Views handle no pointer events. The first pointer to go down owns the
gesture; other pointers are ignored until it ends.

Thresholds are fractions of the column width (slop) or of the page width (commit distance, velocity), so nothing depends
on resolution. Starting values: slop 0.04 of a cell; the direction is horizontal when `|dx| >= |dy|` at the first move
past the slop; commit at half the width or 0.6 pages per second in the swipe's direction; a tap is a press and release
within the slop and 500 ms. These are constants in one place, tuned in the spike.

Alternatives: XAML manipulation events on each view (each view then arbitrates, and nothing can be tested without a
window); `InteractionTracker` for everything (compositor-thread, but it does not cover tap or arbitration and cannot be
unit-tested). `InteractionTracker` stays a candidate for the slide only (decision 5).

### 4. Pager layers and lifetime

The window's content is, back to front: the background, the pager, the indicator view. The pager holds one page host (a
`Canvas` of `WidgetView`s, built from a `PageConfig`) at rest, and two while a swipe is in progress. The code that
`RebuildLayout` has today for creating views moves into the page host, which also owns its reading-to-view map (the
window's `_viewsByReading` becomes a lookup over the live page hosts).

- At rest: one page host, at offset 0.
- Swipe direction decided: if the neighbour exists, build its page host beside the current one (its widgets are created,
  its views subscribe on `Loaded`) and start moving both with the finger. If it does not exist, move the current page
  with the bounce curve and build nothing.
- Release: animate both to the committed or the returning position. When the slide has settled, dispose the page host that
  is no longer shown (views disposed, readings unsubscribed, `FrameClock` entries removed) and report the new page to the
  navigator.
- A configuration, theme, plugin or placement change during a swipe finishes it at once (settle to the current page, no
  animation), then rebuilds as today.

The neighbour is built at the moment the direction is decided, not at pointer-down, so a tap or a vertical drag costs
nothing and starts no provider (a weather fetch on every touch would be a surprise). The price is that a reading never
sampled before shows a dash for the first moments of the slide. A reading seen before shows its last value dimmed (the hub
restores `Pending(lastValue)`). The spike measures how long the dashes last; subscribing earlier is only worth it if they
are visible.

Background: for now the background stays the theme colour and the cards slide over it. The page host is the place where a
per-page background would later slide with its page; nothing in this change forecloses that.

Alternatives: keep all pages built (memory grows with the page count; a page is about 17 MB of surfaces on the Y70);
keep both neighbours built (about +35 MB idle); hide pages and pause them (the frame clock and timers would need a pause
path; disposing is simpler and matches "pay only for what is shown").

### 5. The slide mechanism, decided by the spike

Baseline: the recogniser's dx sets the pager's horizontal offset (a composition-layer offset, so moving does not re-layout
or repaint the widgets), and the settle is a composition animation to the target. The bounce uses a rubber-band curve on
the offset (the distance moved is a diminishing fraction of the drag). `InteractionTracker` with a
`VisualInteractionSource` would move the slide to the compositor thread with inertia and snap points, so it stays smooth
while the UI thread paints a Lottie icon, but the redirect must be decided at pointer-down, before the direction is
known, which may fight decision 3. The spike builds the baseline and measures frame pacing with an animating widget; if it
is smooth, `InteractionTracker` is not used. The recogniser's output does not change either way.

Outcome: the baseline was smooth by touch on the panel, with the Lottie icon animating, so `InteractionTracker` is not used. The
starting values of decision 3 (slop 0.04 of a cell, commit at half a page or 0.6 pages per second, 250 ms settle with an
ease-out curve) were kept. Values on the incoming page showed their last value rather than dashes, so the neighbour is still built when the
direction is decided, not at pointer-down.

### 6. The indicator is drawn by the engine

A `PageIndicator` painter in the engine draws, into a rectangle: one dot per page, the current page as a pill, from a
fractional page position (0 to count-1) so the pill is between two dots mid-swipe, plus an opacity and (for `fade`) the
translucent pill behind. It also answers which page a point selects: the rectangle is divided into one equal slot per
page, each at least a third of a cell wide, centred on the dots. `PageRenderer` calls it for snapshots; the host owns an
`IndicatorView : SKXamlCanvas` in the band that calls it too.

The view repaints only when position or opacity change. Pointer moves drive the position (coalesced to one repaint per
frame); the fade is driven by the frame clock. `FrameClock` is generalised from `WidgetView` to a small interface (a frame
interval and an invalidate), implemented by both views, so there is still one clock that runs only while something
animates.

A floating (`fade`) indicator sits in the rectangle the band would have, over the page. While its opacity is zero the
view is not hit-testable, so a tap goes through; while visible, a tap on a slot changes page.

Alternative: XAML ellipses with composition animations: cheaper to animate, but a snapshot could not show them and the theme
would be applied twice, in two ways.

### 7. Chrome layout and the grid

A `ChromeLayout` in the engine takes the screen size, the resolved indicator mode, the page count and the theme's band
height, and returns the indicator rectangle, the grid rectangle and the number of whole rows. The mode is resolved by one
function: `off` and `auto`-with-one-page give no indicator; `always` reserves the band; `fade` reserves nothing; `auto`
with several pages reserves the band if at least four whole rows would remain, else floats.

The number four is the largest first-party widget (the 4x4 stats widget): below four rows it could not be placed at all, so
a band that costs the last of those rows is not worth reserving. The Y70 has 13.

`GridLayoutManager` keeps its column width and row height and gains the grid height. A widget whose cell rectangle ends
below the grid is returned with `Placed = false` and an error is logged; the result list stays index-aligned with the
widgets so that callers keep working. The window and `PageRenderer` skip unplaced items. The band is the same on every page
and is decided from the page count, not the current page, so it never changes during a swipe.

### 8. Settings and theme

`pager.indicator` in the configuration, in an object so that later pager settings (a transition, a wrap option) have a
home. It is read through the existing configuration reload, so changing the mode applies on save. An unknown value is
`auto` with a warning.

The theme gains an `indicator` group with sizes as fractions of the grid cell, like the card: `bandHeight`, `dotSize`,
`pillLength`, `spacing`, and colours `active`, `inactive` and `backdrop`. Missing values come from the default theme, which
the existing merge already does, so older user themes keep working and the theme format needs no version change. Starting
values (to be tuned on the panel): band height 0.25, dot 0.05, pill 0.14, spacing 0.05.

Timings (the slide, the fade delay of about two seconds, the fade itself) are engine constants, not theme values, until the
motion level exists.

### 9. Snapshot

`--snapshot --page N` asks the navigator logic for page N, builds that page's widgets, and calls the same `ChromeLayout`
and `PageIndicator` as the window, so the reserved band and the pill in the snapshot are the ones the panel shows. A page index
outside the list fails with a logged reason and exit code 1. The indicator is always drawn at full opacity in a snapshot.

## Risks / Trade-offs

- [`WS_EX_NOACTIVATE` may not stop the XAML child window from taking focus] → the first task tests it on the panel before
  anything else is built; the subclass covers the child, and forcing the previous window back is the last resort.
- [A touch may be turned into mouse or press-and-hold behaviour by Windows (a visual contact ring, a right click)] → the
  spike checks it; if it shows, turn off touch feedback for the window.
- [The incoming page paints a frame after it appears, so empty cards flash during a fast swipe] → measured in the spike;
  if visible, hold the slide until every new view has painted once (bounded by a short timeout).
- [Two full pages exist mid-swipe, about 35 MB] → only during the swipe; the idle number stays one page. Measured, noted in
  `docs/perf/pages-and-pager.md`.
- [A reading never sampled before shows a dash while the neighbour slides in] → accepted by the owner; a reading seen before
  shows its last value. See decision 4 for the earlier-subscription fallback.
- [A configuration reload during a swipe] → the swipe finishes immediately, then the rebuild runs as today.
- [Page names are not unique ids, so renames and duplicates can move the user] → accepted until the config format has a
  version and ids can be added.
- [Moving the view code from `MainWindow` into a page host touches the plugin hot-reload path] → keep the existing
  `DisposeViews`-before-rebuild order and re-run the hot-reload check from the earlier changes.

## Migration Plan

No data migration: existing configurations load unchanged (`activePage` keeps its meaning at startup, `pager` is optional)
and existing themes load unchanged. The visible differences for the owner are the indicator band (with the default `auto`
and two or more pages) and Escape no longer closing the window; both are in the README. The focus fix lands first as its own
pull request so that touch is safe to try while the pager is built. Rollback is reverting the pull requests; nothing is
written to disk that older builds would reject (an unknown `pager` setting is ignored by the JSON reader).

## Open Questions

- The exact starting values for the band height, dot sizes and the motion constants are chosen by eye on the panel and
  recorded in `docs/themes.md` and the perf note; they do not change the specs.
