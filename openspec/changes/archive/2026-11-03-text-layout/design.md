## Context

See `proposal.md` for the motivation. Today `TextLine` fits by shrinking the font and measures with `SKFont.MeasureText`,
with letter spacing added per text element (`TextMetrics`). Nothing in the SDK wraps, cuts or scrolls text. Components are
static draw functions, except the Lottie animation, which the widget owns. Widgets animate through `IsAnimating`,
`AnimationFrameInterval` and `NextAnimationAt`, and draw from `WidgetRenderContext.Time`. The motion level is computed by
the engine (`MotionPolicy`, an engine enum) and is not visible to widgets. Touch reaches a widget only as a tap
(`ITapTarget`); a vertical pan is recognised by the host and discarded (backlog item 8).

## Goals / Non-Goals

**Goals:**
- Widgets get wrap, ellipsis, clip, marquee and a scrolling list without measuring or styling text themselves.
- Default behaviour of every existing call is unchanged, byte for byte (the goldens guard it).
- A repaint with unchanged text costs no measuring.
- The list is shaped so that the host's future scroll interface plugs in without a change to the component.

**Non-Goals:**
- Touch scrolling and inertia (the host's job, a later change beside `ITapTarget`).
- Rich text, Markdown, inline images (emotes), selection, text input.
- Shaping for right-to-left and complex scripts (see Open Questions): layout measures like `TextLine` does.
- A provider or widget that fetches a feed; no consumer is built here.

## Decisions

**1. Overflow is a parameter of `TextLine`, wrap is a new component.** `TextLine.Draw` gains an optional `TextOverflow`
(`Shrink` default, `Ellipsis`, `Clip`, `Marquee`). One line stays one component; `TextBlock` is separate because it
returns line counts and cut state, which a line has no use for. *Alternative:* a single `Text` component with every mode.
Rejected: it would change `TextLine`'s result type and widen its signature for every caller.

**2. Cut and break at text elements, measure with the existing metrics.** Cuts use `StringInfo` text elements as
`TextMetrics` already does for tracking, so surrogates, combining marks and emoji sequences stay whole. Wrapping breaks at
spaces and falls back to element breaks. Widths come from `TextMetrics.Measure`, so tracking and case apply exactly as in
`TextLine`. A binary search over the element count finds the longest prefix that fits with the ellipsis. *Alternative:*
SkiaSharp's HarfBuzz shaper for correct bidi and complex scripts. Deferred (Open Questions): it adds a native
dependency to an MIT assembly, and the first consumers are Latin chat and headlines.

**3. A bounded layout cache, keyed by what changes the result.** Key: text, step, width in whole pixels, typeface identity
and size, case, tracking. Value: the broken lines with their widths. Least recently used out beyond a fixed count (a few
hundred entries). It lives in the SDK assembly, so it is per plugin load context and goes away with the plugin; it holds
only strings and numbers, and the typeface is part of the key by its unique id, never by reference, so nothing keeps a
plugin type alive. `TextList` caches per item and invalidates on a width or theme change. Measuring is the cost, not
drawing, so drawing from cached lines is a loop of `DrawText`.

**4. Marquee is a pure function of time.** Position = f(time, line width, rectangle width): rest, scroll at a fixed
speed (a fraction of the step's size per second, so it reads the same at any resolution), rest, return to the start. No
state in the component; `TextLine` only needs `Time`. To let the widget return `IsAnimating`, a static
`TextLine.IsMoving(...)` answers from the same inputs (the line is wider than the rectangle, overflow is marquee, the
motion level is `full`). *Alternative:* a stateful marquee object. Rejected: it adds a lifetime to manage for a function
that has no state worth keeping. The frame interval stays the host's own: a marquee must look smooth.

**5. `TextList` is stateful and owned by the widget, like the Lottie component.** It holds items, wrapped layout, the
offset, the mode and a scroll animation (from-offset, to-offset, start time). That is state a widget would otherwise
rebuild badly on each paint. It is created by the widget, used on the UI thread, and released with it; the `components`
spec's one stateful exception grows to name it (a task). Drawing takes the canvas, theme, rectangle, `Time` and motion
level, and advances its own animation from `Time`. Surface: `SetItems` / `Add` / `Replace`, `Offset`, `ScrollBy`,
`ScrollToItem`, `Mode` (`Manual`, `Follow`, `Rotate`), `Capacity`, `ContentHeight`, `MaxOffset`, `ItemAt(point)`,
`IsMoving`, `NextChangeAt`. *Alternative:* the widget passes the whole item list and the offset on every paint
(stateless). Rejected: follow mode and smooth scroll need the previous state, and each widget would re-implement it.

**6. Follow mode adjusts the offset when items leave the top.** Removing the oldest item subtracts its height from the
offset and from an in-flight animation's endpoints, so what is on screen does not move. Editing an item's text re-lays it
out and, when it is above the view, shifts the offset by the height difference; at the bottom in follow mode it keeps the
bottom.

**7. The motion level reaches components through the render context.** The SDK gets its own `MotionLevel` enum (the engine
keeps its policy type and maps to it; the SDK is MIT and cannot reference the engine) and `WidgetRenderContext.Motion`.
Components take a `MotionLevel` argument rather than reading a global, so tests pass it and a widget that never reads it
is unchanged. The preview renderer and the golden tests set `full`. Reduced and off draw marquees at rest and list
scrolls as jumps; they do not stop a list from following or rotating, since that is the content, not decoration.

**8. Fixed ratios, no theme keys.** Line spacing (1.25 of the step's size), the item gap, the marquee pause (1.5 s) and
speed, and the scroll and slide durations (200 ms) are constants in the SDK. Nothing is added to the theme format, so the
schema and the generated reference are untouched. *Alternative:* theme keys for line height. Rejected for now: no theme
asks for it, and adding a key later is additive.

**9. Taps use the existing interface.** `ItemAt(point)` returns an index; a widget that is an `ITapTarget` calls it in
`CanTap` (an item with a link can be tapped, the rest cannot, so no press shows) and in `OnTap`. No host change.

## Risks / Trade-offs

- [A width or theme change re-lays out every item of a long list] → Layouts are cached per item; 200 items take about
  2 ms (`docs/perf/text-layout.md`), and such a change happens a few times a day at most.
- [A marquee runs a frame loop on a secondary display's CPU budget] → One marquee costs a clipped `DrawText` per frame; the
  motion level ends it for users who ask, and a widget with several long labels can use `Ellipsis` instead. Measure with
  the perf harness and write the numbers into `docs/perf/`.
- [Text measured without shaping is wrong for Arabic, Hebrew and Indic scripts] → Documented; cuts stay whole at element
  boundaries, so the failure is a wrong width, not a broken character. The spike decides on HarfBuzz.
- [`TextLine` must stay byte for byte] → The shrink path is the existing code, taken when no overflow is given; a test
  compares the output with a copy of it, as the typography change did.
- [Glyph-exact goldens differ between Windows and Linux] → The existing split: `[WindowsGlyphFact]` for pixel-exact text,
  geometry assertions (line counts, widths, offsets) for the rest.

## Migration Plan

Additive. No configuration, theme or widget changes. The first consumers (the stats and control labels, then the feed
widgets) adopt the components in their own changes. Rollback is a revert.

## Open Questions

- Do right-to-left and complex scripts need HarfBuzz? **Spike done (2026-11-03), decided not in this change.** Measured
  with `SKFont.MeasureText` against `SkiaSharp.HarfBuzz`'s `SKShaper` at 40 px on Windows: Arabic 426 px unshaped against 326 px
  shaped (31 % too wide: no joining forms), Devanagari 315 against 291 (8 %), Hebrew and Latin within 1 px. Skia's
  `DrawText` does not shape either, so Arabic and Devanagari already draw wrongly in `TextLine` today; layout would only
  inherit that. Adopting the shaper means `SkiaSharp.HarfBuzz` (needs SkiaSharp 4.154.0 or later, one step past the 4.153.1 the
  repository pins, so it moves with the next SkiaSharp bump) plus a native `libHarfBuzzSharp` of about 2 MB per platform in
  an MIT assembly, and shaped drawing in every text component. That is a change of its own; the layout components keep
  cutting at text elements, so the specs hold either way. Recorded as a follow-up.
- A Twitch chat widget wants emotes (images in line). Out of scope here; an item could later carry inline image slots
  measured like a word.
