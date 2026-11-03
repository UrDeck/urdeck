## 1. Groundwork

- [x] 1.1 SDK `MotionLevel` enum and `WidgetRenderContext.Motion` (default `full`), mapped from the engine's effective level by the host and the preview renderer; tests that a level change reaches the next paint
- [x] 1.2 Spike: Arabic, Hebrew, Devanagari and emoji through `TextMetrics` with and without `SkiaSharp.HarfBuzz`; record the findings in the design's Open Questions and decide (no code kept)

## 2. Single line

- [x] 2.1 `TextMetrics`: longest-prefix-that-fits search over text elements, the ellipsis width, and a line-breaking function (spaces, then elements, line feeds) with tracking and case applied as in `TextLine`
- [x] 2.2 `TextOverflow` and the optional parameter on `TextLine.Draw` and `Measure`; `Shrink` takes the existing code; tests for ellipsis (fits, cut, emoji sequence at the cut, nothing fits) and clip
- [x] 2.3 Test that a `TextLine` call with no overflow is byte for byte the output of the untouched path (a copy of it in the test)
- [x] 2.4 Marquee: position as a function of time, `TextLine.IsMoving`, clipped drawing, reduced/off as ellipsis; tests for rest, scroll, return, frame-rate independence and the motion levels

## 3. Block and cache

- [x] 3.1 `TextLayoutCache`: bounded LRU keyed by text, step, width, typeface and size, case and tracking; tests for hits, a theme change, eviction, and that a repeated draw measures nothing (a counting hook)
- [x] 3.2 `TextBlock` with draw and measure, line limit, rectangle-height limit, the ellipsis on the last line, alignment; tests for every scenario of the spec

## 4. List

- [x] 4.1 `TextList` items, layout per item, offset and clamping, `ContentHeight`, `MaxOffset`, `ItemAt`, `ScrollBy`, `ScrollToItem`, clipped drawing with the leading mark and second line
- [x] 4.2 Follow mode: scroll animation from `Time`, jump at reduced/off, stop and resume following, capacity with offset adjustment, an edited item
- [x] 4.3 Rotate mode: interval, slide, `NextChangeAt`, nothing rotates when everything fits
- [x] 4.4 Tests with a fake clock for every scenario of "Text List", "Follow Mode", "Rotating List" and "Item At A Point"

## 5. Pictures and cost

- [x] 5.1 Goldens (Windows): ellipsis line, wrapped block with and without a line limit, a marquee at rest and mid-scroll, a list in follow mode, in a dark and a light built-in theme
- [x] 5.2 Measure a marquee and a following list with the perf harness; write `docs/perf/text-layout.md`

## 6. Docs

- [x] 6.1 `CONTRIBUTING.md`: the components, when to pick each overflow, owning a `TextList`, the motion level
- [x] 6.2 `README.md` SDK row, `docs/BACKLOG.md` (items 7 and 8: what is done, the host's scroll interface still open) and `docs/ROADMAP.md`; the `components` spec's stateful exception names the list
- [x] 6.3 `AGENTS.md` layout line for the SDK lists the new components

## 7. Verify

- [x] 7.1 `dotnet build urdeck.slnx -c Release` with 0 warnings, `dotnet test urdeck.slnx -c Release`, the engine coverage floor, `dotnet format urdeck.slnx --severity warn`, the Linux test projects, the site build, and `openspec validate --all --strict`
- [x] 7.2 Close #106 from the pull request description
