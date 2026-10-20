## Why

Carbon's dot grid, Blueprint's drafting grid, Ember's diagonal hatch and Rigline's scanlines are a page-level texture
(`pattern`). The theme format accepts and checks `pattern` today, but nothing draws it: Carbon ships without its dots
(`themes-carbon-paper`), and Ember and Blueprint cannot ship until it is drawn. This is the next item in
`docs/design/look-and-feel/STATUS.md` ("Plan from here").

## What Changes

- **Pattern layer.** When a theme sets `pattern.kind` to `dots`, `grid`, `hatch` or `scanlines`, the deck draws that pattern
  over the whole window, above the background and its scrim and under every card, the dock and the page indicator. It does
  not slide with the pages, takes no input, and is drawn whether or not a background is shown (it belongs to the theme).
- **Kinds.** `dots` (radius `thickness`), `grid` (horizontal and vertical lines, every `majorEvery`-th one in the `major`
  style), `hatch` (parallel lines at `angle`, 45 degrees by default, rising to the right as on the design board) and
  `scanlines` (horizontal lines). `spacing` and `thickness` are fractions of a grid cell. A pattern with no `color` takes the
  theme's muted text colour at a low alpha.
- **Crisp and seamless.** The pattern repeats in whole pixels: the spacing is rounded to whole pixels, a hatch's angle is
  adjusted by at most a degree so the lines meet across the repeat, and lines are at least one whole pixel wide.
- **Cheap.** The pattern is made once per theme and window size as one small block of whole repeats and laid out by the
  compositor as tiles, not as a window-sized surface. This replaces the earlier decision to accept a page-sized surface
  (+33 MB, spike S1): the owner chose the tiles in the explore of this change, with the page-sized surface as the fallback if
  the tiles show seams. It is never repainted while the deck runs.
- **Snapshot.** A snapshot draws the same pattern from the same tile, so the goldens show what the panel shows.
- **Goldens.** Carbon's, Blueprint's, Ember's and Rigline's goldens are redrawn with their patterns; every other golden is
  unchanged.

Not in this change: `pattern.scroll` and the motion policy (`motion`; the tiles make scroll a compositor animation there),
the `lines` kind (deferred), the validator's pattern findings (`theme-cli`), shipping Ember and Blueprint (their theme
changes).

## Capabilities

### New Capabilities

- `page-pattern`: the page-level texture a theme draws over the background and under the cards: where it is drawn, the
  kinds and their geometry, how it repeats, its colour default, its cost, and the snapshot.

### Modified Capabilities

None. `theme` already accepts and checks the `pattern` values ("Format 2 Values", "Format 2 Validation"); `backgrounds`
keeps its layer and scrim as they are, and the pattern lies above both.

## Impact

- `src/UrDeck.Engine/Rendering`: a pattern tile (pure Skia: the resolved pattern in pixels, the tile, the block of whole
  repeats) used by the host and by `PageRenderer`.
- `src/UrDeck.Host`: a pattern layer between the background and the pages in `MainWindow`, of compositor sprites over one
  decoded block, applied with the background and released with it.
- `tests/UrDeck.Engine.Tests`: tile tests (periodicity, snapping, sizes, colour default); Carbon, Blueprint, Ember and
  Rigline goldens redrawn. `GoldenTests.cs` is not expected to change (the parallel `theme-cli` change touches it).
- Docs: `docs/themes.md` and the site's theme page (the `pattern` section), `docs/design/theme-format-2-deferred-keys.md`
  (the page-sized surface decision and the `pattern.scroll` row), `docs/design/look-and-feel/STATUS.md`, a perf record, and
  the site's Carbon deck picture.
