## 1. Focus fix (its own pull request, `fix(host)`, lands first)

- [ ] 1.1 Set `WS_EX_NOACTIVATE` on the window's extended style (not `WS_EX_TOOLWINDOW`) and subclass the window procedure to answer `WM_MOUSEACTIVATE` with `MA_NOACTIVATE`; remove the Escape accelerator's effect when the window is not activatable
- [ ] 1.2 `URDECK_ACTIVATABLE=1` skips the style and the subclass and keeps Escape closing the window; log which mode started
- [ ] 1.3 On the panel with another window focused: tap, drag and long-press the deck; the other window keeps focus and stays in front. Repeat with a fullscreen application. Record the result in `docs/perf/pages-and-pager.md`
- [ ] 1.4 If the XAML child window still activates the top-level window, extend the subclass to cover it; only if that fails use the last resort of design decision 1 and say so in the note
- [ ] 1.5 Right-click the taskbar button then Close closes the host; the taskbar button is still there
- [ ] 1.6 `host-shell` spec text is correct for what was built; `README.md` and `AGENTS.md` say that Escape no longer closes the window and how to close it (taskbar) and how to restore it for development

## 2. Spike: the slide (throwaway, result recorded in `docs/perf/pages-and-pager.md`)

- [ ] 2.1 Two pages (a Clock and a Stats page, plus a page with the Weather Lottie icon), a mouse drag moves the page with a composition-layer offset, release animates to the nearest page. Not merged
- [ ] 2.2 The same by touch on the panel: record the smoothness the owner sees, frame pacing with the Lottie icon animating, the neighbour page's build time to first paint, and whether Windows adds contact visuals or press-and-hold behaviour
- [ ] 2.3 Try `InteractionTracker` with a `VisualInteractionSource` for the slide and note whether a redirect decided at pointer-down is workable; keep it only if it is clearly smoother
- [ ] 2.4 Measure the dashes on the incoming page: how long a reading that has never been sampled stays a dash, and whether a reading seen before shows its last value. If the dashes are visible, trial subscribing at pointer-down and record the cost
- [ ] 2.5 Choose the slide mechanism and the starting values for slop, commit distance, velocity and the settle time; update design decision 5 and the constants listed in decision 3

## 3. Engine: page state, chrome layout and grid

- [ ] 3.1 `PageNavigator`: created from the page list and `activePage` (clamped), current index and name, a reload operation that keeps the page by name (first of repeated names), else the same index clamped, and a settle operation
- [ ] 3.2 `PagerConfig` with `Indicator` (`always`, `fade`, `off`, `auto`) in `UrDeckConfig` as `pager.indicator`; absent is `auto`; an unknown value is `auto` with a warning
- [ ] 3.3 Theme: add the `indicator` group (`bandHeight`, `dotSize`, `pillLength`, `spacing`, `active`, `inactive`, `backdrop`) to `ThemeDefinition` with merge, sanitising and resolving, and the starting values in `default-dark`, `default-light` and `glass`
- [ ] 3.4 `ChromeLayout`: from the screen size, the resolved mode, the page count and the theme's band height, return the indicator rectangle, the grid rectangle and the number of whole rows; resolve the mode (`off`; `auto` with one page; `always`; `fade`; `auto` with several pages reserves the band when at least four whole rows remain, else floats)
- [ ] 3.5 `GridLayoutManager` takes the grid height; a widget whose cell rectangle ends below it is returned with `Placed = false` and an error is logged; the result stays index-aligned with the widgets; no change when no band is reserved
- [ ] 3.6 `PageRenderer` and the window skip unplaced items

## 4. Engine: gestures and the indicator

- [ ] 4.1 `GestureRecognizer`: pointer samples in, events out (swipe started with direction, moved, ended with velocity; tap; cancelled), first pointer owns the gesture, thresholds as fractions of the column width and page width as chosen in task 2.5, no timers and no clock reads
- [ ] 4.2 Rubber-band function for the offset past the first and last page, and the commit decision (distance or velocity) as pure functions beside the recogniser
- [ ] 4.3 `PageIndicator` painter: dots, the current page as a pill from a fractional position, opacity, the translucent backdrop for the floating mode, themed colours and sizes; a function that maps a point to a page slot with each slot at least a third of a cell wide
- [ ] 4.4 `PageRenderer` draws the band and the indicator for a page index and count; the indicator is at full opacity in a snapshot

## 5. Host: pager

- [ ] 5.1 Move the view creation of `MainWindow.RebuildLayout` into a page host (a `Canvas` of `WidgetView`s for one `PageConfig`, owning its reading-to-view map); the window's repaint-on-reading-change looks up through the live page hosts
- [ ] 5.2 The pager: one page host at rest, a second built beside it when the swipe direction is decided, moved with the finger, settled with the mechanism from task 2.5, the page that left disposed after settling (views disposed, readings unsubscribed, frame-clock entries removed)
- [ ] 5.3 Translate window pointer events into recogniser samples at the pager's root with pointer capture; views handle none; mouse, touch and pen share the path; ignore a second contact
- [ ] 5.4 Past-the-end swipes use the rubber band and build nothing; no wrap
- [ ] 5.5 A configuration, theme, plugin or placement change during a swipe finishes it at once, then rebuilds; the navigator's reload operation keeps the page
- [ ] 5.6 Generalise `FrameClock` from `WidgetView` to a small interface (frame interval, invalidate) so the indicator can use it
- [ ] 5.7 `IndicatorView`: draws `PageIndicator` in the indicator rectangle; repaints only on a change of position or opacity; tap on a slot goes to that page with the slide; for `fade` it appears on a page change or swipe start, fades after about two seconds, and is not hit-testable while invisible
- [ ] 5.8 Rebuild when the resolved mode, the page count or the theme's band height change; layout uses `ChromeLayout` for the grid height

## 6. Snapshot

- [ ] 6.1 `--snapshot --page N` builds page N's widgets, uses the same chrome layout and indicator as the window, and fails with a logged reason and exit code 1 for an index outside the list; the default page is `activePage`
- [ ] 6.2 Update the `SnapshotCommand` usage comment and the `host-shell` entry-point text if the built wording differs

## 7. Tests

- [ ] 7.1 `PageNavigator`: start page and clamping, reload by name, repeated names, removed page, a single page
- [ ] 7.2 `ChromeLayout`: the 1100x3840 numbers (13 rows with and without the band), the 1000x1000 example, each mode, `auto` with one page, with a reserved band that leaves four rows, and with one that would leave three
- [ ] 7.3 `GridLayoutManager`: a widget that ends below the grid is not placed and logs an error; one that fits exactly is; rows 0 to 7 on the tall panel are unchanged; the item list stays index-aligned
- [ ] 7.4 `GestureRecognizer` with scripted samples: tap, short drag, horizontal swipe in both directions, vertical drag, a diagonal drag decided at the first move past the slop, fast flick versus slow drag, a second pointer ignored, cancel, pointer lost
- [ ] 7.5 Rubber band and commit decisions: monotonic, bounded, zero at zero, half-width and velocity commits
- [ ] 7.6 `PageIndicator`: pixel probes for the pill at positions 0, 0.5 and 1, one page draws nothing in `auto`, opacity zero draws nothing, slot mapping for tap points including beside the dot
- [ ] 7.7 Theme tests: the `indicator` group loads, a partial user theme and an older theme inherit it without a warning, an invalid value falls back with a warning
- [ ] 7.8 Config tests: `pager.indicator` round trip, absent means `auto`, an unknown value means `auto`, an older file without `pager` loads
- [ ] 7.9 Snapshot test with the engine alone: page 1 of 3 shows the pill in the middle; an out-of-range page fails

## 8. Verify

- [ ] 8.1 `dotnet build urdeck.slnx -c Release` with 0 warnings, `dotnet test urdeck.slnx -c Release`, the engine coverage floor, `dotnet format urdeck.slnx --severity warn`, and `openspec validate --all --strict`
- [ ] 8.2 Snapshots at 1100x3840 of pages 0, 1 and 2 of a three-page configuration in `always`, `fade` and `auto`: the band, the pill, widgets not under it
- [ ] 8.3 Plugin hot-reload still works with the page host: rebuild a widget while running, `urdeck.log` shows the reload and `Unloaded plugin context(s) collected`, no "not collected" warning
- [ ] 8.4 On the panel: swipe feel by touch, bounce at both ends, tap on each dot, a swipe that returns, a fast flick, a swipe during a config save, and the page kept across a config save
- [ ] 8.5 On the panel: values on the incoming page (dimmed last value, not dashes, for a reading seen before), the Clock showing the current time after a long absence, no flashing empty cards
- [ ] 8.6 On the panel: idle CPU and GPU with nothing moving are not above the single-page baseline of `docs/perf/render-host-baseline.md`; the frame clock stops after a swipe and after the fade; private bytes at rest and mid-swipe are recorded in `docs/perf/pages-and-pager.md`
- [ ] 8.7 Owner looks at the indicator under the dark, light and glass themes: band height, dot and pill size, spacing, colours, the fade, the translucent backdrop

## 9. Documentation

- [ ] 9.1 `docs/themes.md`: the `indicator` group and which colours it uses
- [ ] 9.2 `README.md`: pages and swipe, the `pager.indicator` setting with its four values, `--snapshot --page`, Escape no longer closing the window, and `URDECK_ACTIVATABLE`
- [ ] 9.3 `docs/BACKLOG.md`: item 8 status and "Current state", with the web view swipe gap and the keyboard-focus gap noted; `docs/ROADMAP.md`: milestone 1's pages line
- [ ] 9.4 `docs/perf/pages-and-pager.md`: the focus result, the spike result and the measurements from 8.6
- [ ] 9.5 Archive the change when every task is ticked and verified
