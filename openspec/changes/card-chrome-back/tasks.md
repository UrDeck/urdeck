## 1. Groundwork

- [ ] 1.1 The goldens pass on the branch before any change; every later group re-runs them and the v1 ones must not change
- [ ] 1.2 Read `docs/design/card-chrome.md`, `docs/perf/card-chrome.md` and the prototype (`spikes/card-chrome` on `spike/card-chrome-paint`)

## 2. Resolved style and order

- [ ] 2.1 `ResolvedCardStyle` resolved per cell size beside `ThemeResolver`: pixels, colours, `IsPlain`, the hairline rounding with one warning per key under 1 px; the host and `PageRenderer` resolve one for the page and one for the dock and dispose them with their `Theme`
- [ ] 2.2 `CardOrder.Assign(layout)` in `Layout`: placed, visible items by row then column from 0; dock slots by position
- [ ] 2.3 Tests: `IsPlain` for `default-dark`, `default-light`, `glass` and false for each draft; hairline rounding (2.5 stays, 1.6 to 2, 0.6 to 1 with one warning across several resolves); a plain style's border unrounded; the order with a hidden and an unplaced item, per page, and the dock with an empty slot

## 3. Shape and painter

- [ ] 3.1 `CardShape`: round rectangle, or a chamfer path from `cut` (limited to half the smaller side) and `cutCorners`, starting on the top edge
- [ ] 3.2 `CardChrome.DrawBack` and `DrawFront`, the plain branch being today's `DrawCard` and clip moved unchanged; `WidgetPainter.Paint` takes a `CardLook`
- [ ] 3.3 Error card: restore, clear the whole surface, back, error text, front
- [ ] 3.4 `PageRenderer.PaintAt` clips each card to its rectangle; card looks for page and dock
- [ ] 3.5 Tests: a chamfer's area is the rectangle less its triangles; an empty `cutCorners` is a square; a cut larger than half the side is limited; a plain style takes the plain branch; the error card on a chamfered gradient card

## 4. Fill and border

- [ ] 4.1 Fill precedence: gradient, tint by index (without `cardTints`, the fill colour), fill colour
- [ ] 4.2 Border inside the edge for both shapes; `dashed` with the period fitted to the perimeter; `borderGradient` linear and conic (static); `none` and width 0 draw nothing
- [ ] 4.3 `SizedCardChrome` cached on the style by (tint, size); a back with a gradient drawn once into an image and blitted
- [ ] 4.4 Tests: a two-colour gradient at 90 (top and bottom pixels); the tint order on a page of four; a dashed border's corners alike (fitted period) on a square and a chamfer; a conic border unchanged between two paints; the gradient back made once for a hundred paints

## 5. Host

- [ ] 5.1 `PageHost` gives each `WidgetView` its `CardLook`; the dock's slots get theirs by position; `MainWindow` creates and disposes the two styles with the themes
- [ ] 5.2 Run the host with a fixture theme from the tests (Ember) and one v1 theme; look at the panel with the owner

## 6. Verification

- [ ] 6.1 v1 goldens unchanged; new goldens of the sample page for Ember, Blueprint, Obsidian and Pop (what this change draws of them), looked at by eye before they are committed
- [ ] 6.2 Paint benchmark (the method of `docs/perf/card-chrome.md`): each draft at most 1.5 times `default-dark` on the same machine; record the rows
- [ ] 6.3 On the panel: private memory of a gradient theme against `default-dark` on the sample configuration; record it; if the back images cost more than 5 MB, stop and bring the compositor fallback to the owner
- [ ] 6.4 Before archive: `theme-gauges` also modifies the theme spec's "Format 2 Values" (it adds `gauge.layout`). This change's delta carries that addition; if `theme-gauges` is archived after this change, its delta must carry this change's "Card chrome values" scenario, and the other way round
- [ ] 6.5 `dotnet build urdeck.slnx -c Release` 0 warnings; `dotnet format urdeck.slnx --severity warn`; `openspec validate --all --strict`; the engine tests with the coverage floor; the Linux tests

## 7. Documentation

- [ ] 7.1 `docs/themes.md` card section: shape, fill gradient, tint and its order, border styles, hairlines; remove "not drawn yet" for these keys
- [ ] 7.2 The site's theme page; `docs/design/look-and-feel/STATUS.md` (the row for this change)
