## 1. Groundwork

- [x] 1.1 On `feat/page-pattern`, the build is clean and every golden passes before any change (`dotnet build urdeck.slnx -c Release`, `dotnet test tests/UrDeck.Engine.Tests -c Release --filter Golden`)
- [x] 1.2 Read `UnderlayView` and `UnderlayPicture` (the PNG, `LoadedImageSurface` and kept-surface path this change copies), `BackgroundLayer.Apply`/`Release`, `MainWindow`'s root grid and `ApplyBackgroundLayer`, and `PageRenderer.Render`

## 2. Engine

- [x] 2.1 `PatternTile` in `src/UrDeck.Engine/Rendering`: resolve the theme's `pattern` to pixels for a cell size and a window size (spacing rounded, at least 2 px; line widths whole, at least 1 px; dot radius at least 0.5 px; colour default `colors.textMuted` at 1/16 of its alpha; `major` falling back to the pattern's own); null for no pattern or `kind` `none` (design, decision 1)
- [x] 2.2 The repeat per kind, and the hatch snapping: axis snap within one degree, `px`/`py` rounded, an axis past the window not repeated with the spacing adjusted to keep the other period whole (design, decision 3)
- [x] 2.3 The block: per axis the smallest multiple of the repeat at least 512 px, capped at the window; marks drawn analytically at half-period positions (major lines first in their group, a hatch line through the centre); the image, its PNG on demand, a `Key` of every resolved value and both sizes, and a slot for the host's decoded surface
- [x] 2.4 `PageRenderer.Render`: the pattern after `PaintBackground` and before `PaintUnderlay`, one `DrawRect` with the block as a repeating image shader anchored at (0, 0) (design, decision 4)
- [x] 2.5 Tests (Linux too): each kind's block is periodic (a canvas shaded with the block shows the same pixels one repeat apart, in both directions, across the block's edge); Carbon 35 px, Blueprint 50/250 px, Rigline 10 px and Ember 32x32 at 1100 px wide; a hatch at 30 and at 5 degrees within one degree; 0.5 and 89.5 degrees drawn as axis lines; line widths whole and at least 1; the colour default and the `major` fallbacks; block sizes (multiple of the repeat, at least 512, capped at the window); null and nothing drawn for `none` and for a v1 theme; in a snapshot of Carbon a dot's pixel in a gap and none over an opaque card; the same `Key` for the same inputs and a different one when the theme, cell or window changes

## 3. Host

- [x] 3.1 `PatternLayer` (a `Canvas`, not hit-testable) as a new child of the root grid between `_background` and `_surface`; one `ContainerVisual` of sprites at whole physical pixels (`blockPx / scale` DIPs each), all on one `CompositionSurfaceBrush` over a `LoadedImageSurface` decoded once from the block's PNG and kept on the `PatternTile` (design, decision 2)
- [x] 3.2 Apply it from `MainWindow` beside `ApplyBackgroundLayer` with the pattern's `Key` (same key: nothing happens; new key: old sprites and surface disposed, new ones made); `Release()` beside `_backgroundLayer.Release()`, `Dispose()` beside its dispose
- [x] 3.3 Log one information line when made (kind, repeat, block, tile count, milliseconds), `Pattern: none` when one is removed, and one warning if the surface fails to load (the deck goes on without it)

## 4. Verification

- [x] 4.1 Goldens: redraw `carbon-page`, `carbon-dock`, `blueprint-page`, `blueprint-dock`, `ember-page`, `rigline-page` and `rigline-dock` with `URDECK_UPDATE_GOLDENS=1` (then clear it); look at each by eye against the design board before committing; every other golden byte-identical (`git status` shows only those seven PNGs changed)
- [x] 4.2 On the panel, the S1 method (`docs/perf/theme-look.md`: a Release build copied to a scratch folder, 15 s warm-up, the median of 30 one-second samples, three interleaved runs): private bytes and working set with Carbon against a user-theme copy of Carbon with `"pattern": {"kind": "none"}`, and with the Blueprint, Ember and Rigline fixtures copied into `themes/`; the build time from the log line (warm). Budget 10 MB and 20 ms. If over, try a 256 px minimum block before anything else, and record both
- [x] 4.3 Idle check: a minute with Carbon, its moving background and ticking clocks, a page swipe and an unrelated config save logs exactly one `Pattern:` line
- [x] 4.4 Theme switch while running: Carbon to `default-dark` removes the dots (`Pattern: none`) and back makes them again
- [x] 4.5 **Owner's panel look** (stop and ask the owner; do not tick without them): Carbon on the panel at 100 % (the second look `themes-carbon-paper` asked for, against the board), then at 125 % and 150 % display scaling for seams between tiles, and a page swipe. If a seam shows and the whole-pixel placement cannot fix it, switch to the fallback in design decision 2, record why in a "Findings during apply" section of `design.md`, and get the owner's approval for the memory before going on
- [x] 4.6 `dotnet build urdeck.slnx -c Release` with 0 warnings; `dotnet format urdeck.slnx --severity warn --verify-no-changes`; `dotnet test urdeck.slnx -c Release`; the engine tests with `-p:CollectCoverage=true` (the coverage floor); `openspec validate --all --strict`

## 5. Documentation

- [x] 5.1 `docs/themes.md`, the `pattern` section: "Drawn by" this change; the kinds and their geometry, the angle's direction, the anchor, whole-pixel snapping, the colour default, `angle` and `major` used by `hatch` and `grid` only; `lines` and `scroll` not available yet. The same on the site's theme page (`site/src/content/docs/themes.md`); `cd site; npm run build` passes
- [x] 5.2 `docs/perf/page-pattern.md`: the numbers from 4.2, the tile count and block sizes per theme, and what the panel look found
- [x] 5.3 `docs/design/theme-format-2-deferred-keys.md`: the "Decided by the spikes" line about the page-sized surface now says tiles (this change), and the `pattern.scroll` row's "Restore when" says that scroll is an offset animation of the tiles' container in `motion`
- [x] 5.4 `docs/design/look-and-feel/STATUS.md`: the PR 8 row (done, what was built, the numbers, the panel look), "Plan from here" (page-pattern done; Ember and Blueprint unblocked), and the "Not verified" items this change verified
- [x] 5.5 Re-render the site's sample deck pictures (`pwsh scripts/Render-SiteDecks.ps1`) so Carbon's shows its dots; only Carbon's picture should change
