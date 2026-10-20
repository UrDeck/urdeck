## Context

The theme format already reads, checks and keeps `pattern` (`ThemeFormat2.Pattern.cs`, the "too dense" rule in
`ThemeDefinition.Rules.cs`); nothing draws it. The window's root grid holds, bottom to top, `_background` (a `Grid` whose
brush is the theme colour, with the `BackgroundLayer` element inserted at index 0), `_surface` (the sliding pages, each a
`PageHost` with its `UnderlayView` first) and `_overlay` (the dock and the indicator). `PageRenderer` draws a snapshot on
one canvas: clear, `PaintBackground`, `PaintUnderlay`, cards, dock, indicator.

Two facts shape this design:

- Every theme with a pattern (Carbon built in; the Blueprint, Ember and Rigline fixtures) defaults to a **moving shader at
  scale 0.5** (`contour`, `plotter`, `embers`, `horizon`). The background's own surface is half resolution and repainted 30
  times a second, so the pattern cannot be drawn into it.
- Spike S1 measured a window-sized `SKXamlCanvas` at **+33 MB** (`docs/perf/theme-look.md`, "S1 page underlay"), and noted that
  the lifted compositor has no tiling surface brush. The earlier decision accepted the 33 MB
  (`docs/design/theme-format-2-deferred-keys.md`, "Decided by the spikes"). The explore of this change replaced it with
  compositor tiles (decision 2), which `UnderlayView` has shown to be cheap with `LoadedImageSurface`.

On the panel (1100x3840, 100 %) a cell is 275 px: Carbon's dots are 35 px apart with a radius of 2.5 px, Blueprint's grid
50 px with a major line every 250 px, Ember's hatch 22.5 px across the lines, Rigline's scanlines 10 px.

## Goals / Non-Goals

**Goals:**
- One engine routine makes the pattern's pixels; the host's tiles and the snapshot both use it, so the goldens are what the
  panel shows.
- At most 10 MB and 20 ms (warm) on the panel for any kind, made once per theme and size, never repainted.
- No seam between tiles at 100 %, 125 % and 150 %.

**Non-Goals:**
- `pattern.scroll` and `MotionPolicy` (`motion`). Decision 2 is chosen partly so that scroll becomes a looping offset
  animation on the tiles' container there, with no repaint; nothing of it is built here.
- The `lines` kind (deferred; `hatch` with a larger `thickness` covers it).
- Validator findings (`theme-cli` owns them; this change does not edit `ThemeDefinition.Rules.cs`).
- Aligning the pattern to the card grid. The board anchors its CSS backgrounds at the page's top-left, and the drafts'
  spacings do not divide the cell pitch, so alignment would not show anyway.

## Decisions

### 1. `PatternTile` in the engine: resolve, then make one block

A new type in `src/UrDeck.Engine/Rendering` (pure Skia, runs on Linux) takes the loaded theme, the cell size in physical
pixels and the window size in physical pixels, and returns null for no pattern or a resolved pattern:

- **Resolve to pixels.** `spacing × cellPx` rounded to a whole pixel, at least 2. Line widths (`grid`, `hatch`, `scanlines`,
  and `major.thickness`) are `thickness × cellPx` rounded to a whole pixel, at least 1, so lines are crisp. A dot's radius
  stays fractional (anti-aliased), at least 0.5 px. Colour: `pattern.color`, or `colors.textMuted` with its alpha divided by 16.
  `major.color` and `major.thickness` fall back to the pattern's own.
- **The repeat** (the period in x and y, whole pixels): `dots` and `grid` (s, s), or (s × majorEvery) with `majorEvery`;
  `scanlines` (1, s) drawn in a wider block; `hatch` see decision 3.
- **The block**: per axis, the smallest whole multiple of the period that is at least 512 px, but never wider or taller than
  the window (a period larger than the window gives a block the window's size, which then never repeats in that direction).
  The marks are drawn into the block directly, not by repeating a smaller tile, so a block that is cut to the window still
  has every mark in place.
- **Placement in the block**: dots at (k + ½) periods, lines centred at (k + ½) periods, so no mark is cut by the block's edge
  and the window's first marks are half a repeat in. A grid's major lines are the first of each group. A hatch line passes
  through the block's centre.
- **Output**: the block as an `SKImage` (premultiplied, platform colour type), its size, its PNG bytes made on demand for the
  host (as `UnderlayPicture` does), and a `Key` string joining every resolved value, the cell size and the window size. The
  type keeps the decoded host surface it is given (as `UnderlayPicture.HostSurface`), so a page rebuild does not decode again.

*Alternative: a tiny tile (one period) drawn with `SKShader.CreateImage(Repeat)` in the snapshot and a separate block for the
host.* Rejected: two code paths for the same pixels. The snapshot shades with the block itself (repeat mode), so both draw the
same image.

### 2. Host: compositor sprites over one decoded block (the explore's choice)

`PatternLayer` in the host, a `Canvas` with `IsHitTestVisible = false`, is a new child of the root grid **between
`_background` and `_surface`**. It holds a `ContainerVisual` (`ElementCompositionPreview.SetElementChildVisual`) with one
`SpriteVisual` per block position covering the window: `ceil(windowPx / blockPx)` columns and rows (about 3 × 8 = 24 for
Carbon on the panel). Every sprite paints the same `CompositionSurfaceBrush` (`Stretch = Fill`) on one `LoadedImageSurface`
decoded from the block's PNG, as `UnderlayView.MakeBrush` does.

- **Whole physical pixels.** A sprite is `blockPx / scale` DIPs in size, at an offset of `k × blockPx / scale` DIPs, so every
  tile starts on a whole physical pixel. The window's origin is on a physical pixel, so there are no seams by construction;
  checking this at 125 % and 150 % is a task.
- **No sliding.** The layer is not inside `_surface`, so pages slide over it; it lies under the dock and indicator in
  `_overlay` too.
- **When it is made.** `MainWindow` applies it next to `ApplyBackgroundLayer` with the pattern's `Key`; the same key does
  nothing (an unrelated config save, a page swipe and a page rebuild change nothing). A new key drops the old sprites and
  surface and makes the new ones. `Release()` is called where `_backgroundLayer.Release()` is (the deck's page dropped), and
  `Dispose()` where it is disposed. `SetVisible` and the display state do not touch it: it is static.
- **Logging.** One information line when it is made: `Pattern: dots, repeat 35x35 px, block 525x525 px, 3x8 tiles, made in
  N ms`, and `Pattern: none` when a pattern is removed. A failed decode logs a warning once (`LoadCompleted`), and the deck
  goes on without the pattern.

*Alternative: a window-sized `SKXamlCanvas` (the earlier decision).* Kept as the **fallback**: if the tiles show a seam on
the panel at any scaling that the whole-pixel placement cannot fix, the apply switches to a static canvas in the same place,
painted once with the block as a repeating shader, records why in this design's findings, and the spec's 10 MB is raised to
what it measures (about 33 MB) with the owner's approval.
*Alternative: draw into the background's surface.* Rejected: half resolution and 30 repaints a second under a shader, and
no surface at all with no background.
*Alternative: one sprite per mark.* Rejected: about 3,400 sprites for Carbon.

### 3. Hatch: snap the periods, then the angle follows

Lines at angle θ (degrees from horizontal, positive rising to the right on screen, so y goes up while x goes right) and
perpendicular spacing s repeat horizontally every s / |sin θ| and vertically every s / |cos θ|.

- θ within one degree of 0 or 180 is drawn as horizontal lines (`scanlines` geometry); within one degree of ±90 as vertical
  lines. Otherwise:
- `px = round(s / |sin θ|)`, `py = round(s / |cos θ|)`; the drawn angle is `atan(py / px)` with θ's sign, which differs from θ
  by under a degree for every allowed spacing on the panel (45° gives 32 × 32 for Ember, exact).
- When `px` or `py` exceeds the window in its direction (a shallow angle with a wide spacing), that axis is not repeated
  (decision 1, the block is the window's size there); only the other period must be whole, so s is adjusted to make it whole
  and the angle is kept exact.
- The lines are drawn analytically across the block (with one extra line beyond each edge so the corners are covered), not
  per tile.

### 4. The snapshot draws the same block

`PageRenderer.Render` draws the pattern right after `PaintBackground` and before `PaintUnderlay`: one `DrawRect` over the
whole canvas with the block as an image shader in repeat mode, anchored at (0, 0). With the window size passed as the
canvas size, the block is the host's block, so the pixels are the same.

### 5. Goldens

The goldens render without a background, so the pattern shows over the theme colour. Redrawn: `carbon-page`,
`carbon-dock`, `blueprint-page`, `blueprint-dock`, `ember-page`, `rigline-page`, `rigline-dock`, each looked at by eye before
it is committed. Every other golden (the v1 three, Obsidian, Paper, Pop, Prism) must stay byte-identical: that is the proof
that a theme without a pattern draws as before. Update with `URDECK_UPDATE_GOLDENS=1` (`CONTRIBUTING.md`, "Golden images");
`GoldenTests.cs` should not need an edit.

## Risks / Trade-offs

- [Tiles show a seam at a scaling that isn't a whole number] → whole-pixel offsets by construction; looked at on the panel at
  125 % and 150 %; the canvas fallback (decision 2) if it cannot be fixed.
- [The `LoadedImageSurface` decode path costs more than the pixels: the underlay measured +7.7 MB for Paper, mostly that path]
  → measured against the 10 MB budget; the block is at most about 1 to 2 MB of pixels. If over, a smaller block (minimum 256 px)
  is tried before the fallback.
- [The pattern arrives a frame after the cards (asynchronous decode, S1)] → it is made once per theme, not per page; a frame
  without dots at startup is acceptable.
- [The parallel `theme-cli` change edits `GoldenTests.cs` and moves the sample page] → this change only replaces PNGs; whichever
  lands second rebases, and the goldens of the second must be regenerated if the sample page moved.
- [A very large repeat (a `grid` with `majorEvery` 20 at a spacing of 2 cells is 11,000 px) makes the block the window's
  size, about 17 MB on the panel] → drawn correctly, over the budget, and the log line shows the block size. The budget is
  specified for the built-in and drafted patterns only (largest repeat: Blueprint's 250 px). A validator finding for a repeat
  over 1024 px is a note for `theme-cli`, not this change.

## Migration Plan

None: nothing reads a pattern today. Themes without a pattern draw byte-identically (goldens). Rollback is reverting the change.

## Open Questions

- Whether Rigline's 2.5 px scanlines, rounded to 2 or 3 px, look right is for its theme change to judge; the rounding rule
  stays.
