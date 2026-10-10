## Context

See `proposal.md` for the motivation. This records what the exploration of 2026-10-09 settled with the owner, what the
code looks like today, and the choices an implementer would otherwise make again.

Settled with the owner: the dock is in the same pull request as `shortcut-and-tap`; the indicator sits above the dock;
four equal slots, as Nexus has; any 1x1 widget may sit in a slot; the slots are shown as one centred group.

What exists (verified in the code on 2026-10-09):

- `ChromeLayout.Compute(screen, mode, pageCount, bandHeight)` splits the screen into the indicator's rectangle and the
  grid, and resolves `auto`. `MainWindow.RebuildLayout` and `PageRenderer.Render` both call it and hand the grid's
  height to `GridLayoutManager`.
- On the 1100x3840 panel: 13 rows are 3575 pixels, the indicator's band is 69, and 196 pixels are unused.
- `PagerController` owns the pages canvas and the overlay canvas. The overlay holds the `IndicatorView`; `Release`
  clears both. Input goes through one recognizer; `Press` and `Tap` look at the indicator first, then at
  `_current.FindView`.
- `PageHost` is a `Canvas` of `WidgetView`s built from a `PageConfig` and a list of `WidgetLayoutItem`s. It skips
  items that are not placed, not visible or not registered, keeps a reading-to-view map and offers `FindView` through
  the engine's `WidgetHitTest`. Nothing in it is about paging.
- `WidgetView` takes its `Theme` as a constructor argument, and `ThemeResolver.Resolve(loaded, cellPx)` turns the
  theme's fractions into pixels for one cell size.
- `MainWindow.RepaintChangedReadings` asks the views of `PagerController.LivePages`.
- `UrDeckConfig.Dock` is a `List<DockItemConfig>` (`type`, `command`, `url`) that nothing reads; the sample has
  `"dock": []`.

## Goals / Non-Goals

**Goals:**

- A dock with no drawing, press, launch or icon code of its own: slots are cells, entries are widgets.
- No change to the SDK and nothing a widget must do to work in a slot.
- The same layout decision for the window and the snapshot, in engine code that tests cover without a window.
- A configuration with an empty dock behaves, and costs, exactly as today.

**Non-Goals:**

- A backdrop bar behind the slots, more than four slots, scrolling, per-page docks.
- A different composition for docked widgets. A widget that looks poor at slot size is fixed in the widget or by the
  theme's dock height, not by the dock.

## Decisions

### 1. The dock is a second reserved band, below the indicator's

`ChromeLayout.Compute` gains the dock's height (a fraction of the column width; 0 for no dock) and its result gains a
`Dock` rectangle. From the bottom: the dock band, then the indicator's band (`always`) and then the grid. In `fade`
mode the indicator's rectangle is the band-high strip at the bottom of the grid area, so it floats just above the
dock. `auto` counts the rows that remain after both reservations. The dock is reserved whenever it has entries, even
below `MinRowsForBand`: the user asked for it, and the indicator is the element that gives way (to `fade`).

Whether there is a dock is decided from the configuration alone (`dock` has at least one entry), not from whether the
entries can be shown, so the layout does not depend on which plugins happen to be loaded.

Alternative considered: the indicator at the very bottom and the dock above it (no move for the indicator). The owner
chose the indicator next to the pages it describes.

### 2. Slots are layout items; the existing hit test and `PageHost` are reused

A pure engine function (working name `DockLayout.Compute(dockRect, entryCount, gapFraction)`) returns one
`WidgetLayoutItem` per shown entry: slot side `s = min(dock height, screen width / 4)`, count `n = min(entries, 4)`,
group width `n * s` centred in the band, each card the slot inset by half of `gapFraction * s` with edges rounded as
`GridLayoutManager` rounds them. `CellPosition`/`CellSize` are the slot, `GridSize` is 1x1.

Which entries can be shown is a second pure step that takes the descriptor lookup: an entry beyond the fourth is
dropped with one warning; an entry whose type is missing, unregistered, or lacks the size 1x1, or that is not visible,
gets `Placed = false` (and, where it applies, a warning), so its slot stays empty. It also yields the configuration
each widget is created from: a copy with `width` and `height` 1, so a widget that picks its composition from its grid
size picks the 1x1 one, and the user's file is not rewritten.

In the host the dock is a `PageHost` built from those items and a `PageConfig` holding the entries. `PageHost` needs
no change: it already skips unplaced items, maps readings to views and finds the view under a point. Its coordinates
are the window's, so it sits at the origin of the overlay like the pages sit in theirs.

Alternative considered: a `DockView` that paints all slots on one surface. It would need its own input mapping, press
feedback and repaint logic, which is exactly what `WidgetView` already is.

### 3. A second resolved theme makes a slot a small grid cell

`MainWindow.RebuildLayout` resolves the theme a second time with `cellPx = s * scale` and passes that `Theme` to the
dock's views. Everything a widget and the card painter read (radius, border, padding, gap, the three text steps)
is a fraction of the cell, so the card is a proportional miniature. `PressStyle` is a pair of ratios and is shared.
The second theme is created only when there is a dock and is disposed with the first.

Cost: a second set of typeface instances (clones of one variable font at a few weights). Measured in task 6.

Alternatives considered: painting the widget at full cell size into an offscreen surface and scaling it down (soft
text, a second surface per slot); scaling the view's composition visual (the press animation already uses that
transform, and hit testing would need the inverse); giving the widget a "compact" flag (SDK API, and every widget
would have to honour it).

### 4. The dock lives beside the indicator, outside the pages

`PagerSetup` gains `BuildDock` (returns null without a dock). `PagerController.Rebuild` builds it once and adds it to
the overlay under the indicator; `Release` disposes it. Page changes (`EnsureNeighbor`, `Slide`, `Settled`) do not
touch it, so it neither moves nor is rebuilt, and its widgets keep their readings and icons. `LivePages` also yields
the dock, so reading changes reach its views through the existing path.

The overlay is above the pages, but the grid never extends into the dock band (decision 1), so nothing is covered.
During a slide a page's views do not enter the band either: pages only move horizontally.

### 5. Input: indicator, then dock, then page

`Press` and `Tap` ask the indicator first (unchanged), then the dock's `FindView`, then the current page's. A point
inside the dock band that hits no card is not passed on to the page: nothing of the page is there. Swipes need no
change, the recognizer does not care where a gesture starts, and input is already dropped while a slide animates.

### 6. Configuration: `dock` becomes a list of widget objects

`UrDeckConfig.Dock` becomes `List<WidgetConfig>` and `DockItemConfig` is removed. An entry in the old shape
deserializes as a widget object with an empty `typeId` and its `type`, `command` and `url` in the extension data: it
round-trips, shows nothing and logs a warning. No migration is written because no released configuration used the
field.

`MainWindow`'s layout key includes what the dock depends on (it is rebuilt with the pages on any configuration reload
today, since a reload forces a rebuild).

### 7. Snapshot

`PageRenderer.Render` takes the dock's widgets beside the page's, calls the same two engine functions, resolves the
second theme and paints each placed slot with `WidgetPainter`. `SnapshotCommand` creates the dock's widgets, and adds
their subscriptions to the readings it waits for; the icon wait is global already.

### 8. Sample configuration and docs

The sample's shortcut to `%USERPROFILE%` moves from row 8 of the page into `dock`, so a first start shows a dock.
`docs/handoff/2026-10-09-shortcut-and-dock.md` is deleted in this change, as it says of itself.

## Risks / Trade-offs

- [A widget's 1x1 composition is unreadable at 0.71 of a cell (a stat's label, a clock's date)] → The second theme
  keeps proportions, so it is as readable as a 1x1 card seen from a little further away. Checked by eye on the panel
  (task 7.1) with a shortcut, a clock and a stat; the theme's dock height is the knob.
- [The dock takes a row on displays without leftover space] → Accepted and specified: the page widget that no longer
  fits is not placed and the log says so. The README states it.
- [Rounding makes the dock one pixel too tall and the panel drops to 12 rows] → 0.71 of 275 is 195 and the bands sum
  to 264 of the 265 pixels to spare; a test pins the 13 rows for the built-in themes.
- [A floating indicator over the last grid row, just above the dock, is easy to hit by accident] → It only takes taps
  while visible, as today; no change in behaviour, only in position.
- [Dock widgets are alive on every page for the whole run] → That is the feature. Shortcuts have no timer; a docked
  clock or stat costs what it costs on a page. Measured in task 7.3.
- [An old-shape `dock` entry silently shows nothing] → One warning per entry in the log names the missing `typeId`.
