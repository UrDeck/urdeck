## Why

The owner's Nexus page has a dock: four launchers at the bottom of the screen that stay put whatever page is shown.
`shortcut-and-tap` built everything a dock item needs (tap input, press feedback, launching, icons, the image tile,
the shortcut widget) and deliberately left the dock itself out. This change adds it, and with it the last piece of
backlog items 7, 8 and 10 that milestone 1 ("daily driver") needs. On the 1100x3840 panel there are 196 pixels under
the thirteenth grid row that nothing uses today, so the dock costs no row there.

## What Changes

- **A dock at the bottom of every page.** A band at the very bottom of the screen holds up to four widgets. It is the
  same on every page, does not move while a page slides, and its widgets stay alive across page changes.
- **The page indicator moves above the dock.** With a dock, the order from the bottom is: dock, indicator, grid. A
  floating (`fade`) indicator floats over the bottom of the grid, just above the dock. Without a dock nothing changes.
- **Four equal, square slots, shown as one centred group.** The slot's side is the dock's height, a theme value
  (default 0.71 of a grid cell: about 195 pixels on the panel, which keeps all 13 rows). The group is as wide as the
  number of entries.
- **Any widget that supports 1x1 can sit in a slot**, normally a shortcut. A slot is a grid cell at a smaller size: the
  theme is resolved for the slot, so the card's padding, corners and text shrink in proportion and the widget is a
  miniature of its 1x1 card, drawn by the same code. A widget type that does not support 1x1 is not shown, with a
  logged warning.
- **The `dock` configuration field is used.** Each entry is a widget object like the ones on a page, without a
  position: its place is its index. Entries beyond the fourth are ignored with a logged warning. An empty `dock`
  reserves nothing. **BREAKING** for the unused item shape (`type`, `command`, `url`) the field had until now: such an
  entry has no `typeId`, so its slot stays empty and a warning is logged. No shipped configuration used it.
- **Input as on a page.** Press feedback and taps reach dock widgets exactly as they reach page widgets. A sideways
  swipe that starts on the dock changes the page; the dock stays where it is.
- **`--snapshot` draws the dock.**

Deliberately not in this change: more than four slots, a scrolling dock, a backdrop bar behind the slots, per-page
docks, drag and drop, and the editor.

## Capabilities

### New Capabilities

- `dock`: the dock: where it sits, its slots and their size, what may be placed in a slot and how it is drawn, its
  configuration, its lifetime across pages, and its input.

### Modified Capabilities

- `grid-layout`: the reserved bottom area may hold two bands, the dock at the bottom and the indicator above it.
- `page-navigation`: where the indicator sits when there is a dock, and how `auto` counts the remaining rows.
- `theme`: adds the dock's height to a theme's contents.
- `host-shell`: the `dock` field of the configuration.
- `widget-input`: taps and presses also reach the dock's widgets, before the page's.

## Impact

- `src/UrDeck.Engine`: `ChromeLayout` reserves the dock band and places the indicator above it; a dock layout (slots
  as layout items, so the existing hit test works on them); `dock.height` in the theme definition and the three
  built-in themes; `UrDeckConfig.Dock` becomes a list of widget objects and `DockItemConfig` is removed;
  `PageRenderer` draws the dock.
- `src/UrDeck.Host`: `MainWindow` resolves a second theme for the slot size and builds the dock; `PagerController`
  keeps the dock outside the pages, hit-tests it before the page, and includes it in reading repaints;
  `SnapshotCommand` creates the dock's widgets.
- SDK: no change. A widget in the dock sees a normal 1x1 render context.
- Config: `dock` entries are widget objects; the sample configuration moves its shortcut into the dock.
- Tests: chrome layout with a dock (the panel keeps 13 rows; a short screen loses rows; `auto`), the dock layout
  (group width, centring, gaps, more than four entries, entries that cannot be shown), hit testing on the slots, the
  theme value, the configuration round trip, a snapshot with a dock.
- Docs: `README.md` (the dock, the `dock` field), `docs/themes.md`, `docs/perf/dock.md`, `docs/BACKLOG.md`,
  `docs/ROADMAP.md`; `docs/handoff/2026-10-09-shortcut-and-dock.md` is deleted, as it says of itself.
- Delivery: on the branch `feat/shortcut-and-dock`, in the same pull request as `shortcut-and-tap` (the owner's
  choice of 2026-10-09).
