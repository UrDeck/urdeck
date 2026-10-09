## Why

A page file can already hold several pages, but the host only ever shows one and there is no way to change it, so
everything on the owner's Nexus page that is split across pages cannot move over. Pages, swipe and a page indicator
are the next step to a daily driver (roadmap milestone 1, item 8). Touch also exposes a bug that must be fixed first:
tapping the panel activates UrDeck and takes focus away from whatever the user is running, a game included.

## What Changes

- **Focus fix (first, can ship alone):** the window no longer activates when it is touched or clicked
  (`WS_EX_NOACTIVATE`, and the activation message answered with "do not activate"). The taskbar button stays, so the
  app can still be closed from it. **BREAKING (accepted):** the window never has keyboard focus, so Escape no longer
  closes it; a tray icon (milestone 2) will replace that.
- **Runtime page state:** `activePage` becomes the page shown at startup. The page being shown is held by the host and
  swiping never writes the config. A config reload keeps the user on the page they are on.
- **Swipe pager:** a horizontal swipe changes page with a sliding transition. Only the current page is alive: the
  neighbour page is built when a swipe starts, and the page that left is disposed once the slide settles. Swiping past
  the first or last page bounces; it never wraps.
- **Page indicator:** dots with the current page drawn as a pill, drawn by the engine (so `--snapshot` shows it) and
  following the finger while swiping. Tapping a dot switches to that page. A new setting chooses `always` (a reserved
  band at the bottom, as in HYTE Nexus), `fade` (floats over the page and fades out after a page change), `off` or
  `auto` (the default: hidden with one page, otherwise reserved when enough grid rows remain, else floating).
- **Chrome layout and the grid:** the bottom band is taken out of the grid area, so widgets never go under it. The
  column width is unchanged. On the 1100x3840 panel the band fits in the 265 px that the 13 whole rows already leave
  over, so it costs no rows. A widget that no longer fits in the remaining rows is not placed and an error is logged.
- **Theme:** the indicator's look (dot size, pill length, spacing, colours, band height) is theme data, in the same
  resolution-independent units as the card gap.
- **Snapshot:** `--snapshot --page N` renders page N with the reserved band and the indicator.
- **Gesture recognition lives in the engine** (swipe and tap, testable with a fake clock). The host only translates
  pointer events from the window into it. Mouse, touch and pen take the same path.

Deliberately not in this change: any SDK input API (widgets receive no touch), the shortcut widget, the dock, swiping
over a hosted web view, per-page backgrounds, a motion level, and the editor. The SDK is not touched.

## Capabilities

### New Capabilities

- `page-navigation`: the current page as runtime state, swipe and tap recognition, the pager transition and its bounce,
  neighbour-page lifetime, and the page indicator with its four modes.

### Modified Capabilities

- `grid-layout`: a reserved bottom band shrinks the grid area; rows come from the remaining height; a widget that
  does not fit is not placed.
- `host-shell`: the window does not take focus and no longer closes on Escape; the window keeps its place in the
  taskbar; `--snapshot` accepts `--page`; `activePage` means the startup page; the indicator setting is read from the
  configuration.
- `theme`: a theme carries the indicator's look.

## Impact

- `src/UrDeck.Engine`: a gesture recogniser, a page indicator painter, a chrome layout object, `GridLayoutManager`,
  `PageRenderer` (band and indicator), `ThemeDefinition`/`ThemeResolver`, the built-in themes, `ConfigStore` (the
  indicator setting).
- `src/UrDeck.Host`: `MainWindow` (pager, page lifetime, input translation, window style and activation message),
  `SnapshotCommand`, and the pointer plumbing on the window.
- `sdk/`: none. No SDK version change is expected.
- `widgets/`: none. Existing widgets and configs keep working.
- Tests: recogniser and chrome layout in `UrDeck.Engine.Tests` (they count toward the coverage floor), theme tests.
- Docs: `docs/themes.md`, `docs/BACKLOG.md` (item 8, current state), `docs/ROADMAP.md` (milestone 1), `README.md` (the
  window no longer closes on Escape), and `docs/perf/pages-and-pager.md` for the measurements.
- Known gap carried forward: a future web view with text entry needs keyboard focus, which this window never has.
