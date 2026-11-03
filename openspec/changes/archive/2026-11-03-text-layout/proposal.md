## Why

Text in the SDK has one answer to "too long": `TextLine` shrinks the font until the text fits. That is right for a
label such as `CPU`, and wrong for everything else. A track title, a calendar entry or a long Home Assistant entity name
becomes unreadably small, and there is no way to show more than one line at all. Several backlog items wait on text that
wraps, truncates or scrolls: RSS headlines (#121), the calendar agenda (#122), tickers and live streams (#127), the alert
widget (#118), the service status grid (#125), and a chat feed such as Twitch, which has no issue yet but is the case
that needs all of it. GitHub issue #106 asks for this as SDK groundwork so each widget does not invent its own.

## What Changes

- **Overflow for `TextLine`**: a new optional parameter says what happens when the text is wider than the rectangle:
  `Shrink` (today's behaviour and the default, byte for byte), `Ellipsis` (cut at a character boundary and end in `…`),
  `Clip` (cut at the edge) or `Marquee` (see below). No existing call changes.
- **`TextBlock`**: a component that wraps text to the rectangle's width at a theme step, with a line limit, ending the last
  line in `…` when the text does not fit. Breaks at spaces, then inside a word that is wider than a line. Layout is cached
  per text, width and font, so an unchanged block measures nothing per frame.
- **Marquee**: a single line wider than its rectangle scrolls sideways, pauses at the start, and runs on a loop. It is a
  function of `WidgetRenderContext.Time`, so it uses the existing animation contract (`IsAnimating`,
  `AnimationFrameInterval`) and stops under the theme's motion level `reduced` and `off` (it then shows the ellipsis).
- **`TextList`**: a vertically scrolling list of items (each an optional leading mark, wrapped text, optional muted second
  line), clipped to the rectangle, with a scroll offset the widget owns. Three behaviours: follow the newest item (a chat
  feed, with items arriving at the bottom), rotate through items one at a time (a headline carousel), and rest at an offset
  set by the widget. `HitTest` returns the item under a point, so a widget that is an `ITapTarget` can open a link.
- **Not in this change**: touch scrolling. A vertical pan reaching a widget needs the host's scroll interface (backlog item 8,
  beside `ITapTarget`); `TextList` takes an offset and an inertia-free `ScrollBy`, so that interface plugs in later without
  changing the component. Also out: rich text beyond a bold run and a bullet, Markdown, emoji images (a chat's emotes), and
  any provider that fetches a feed.

## Capabilities

### New Capabilities

- `text-layout`: wrapping, line limits, ellipsis, marquee and the scrolling list: what each draws, how layout is cached,
  how motion level and the animation contract apply, and what the list reports for a point.

### Modified Capabilities

- `components`: "Text Line" gains the overflow parameter; its shrink behaviour stays the default.
- `widget-sdk`: "WidgetRenderContext" carries the effective motion level, which the SDK does not expose today, so a
  component can stop a marquee and a widget can decide whether to animate its own content.
- `motion`: "What Each Level Allows" says what `reduced` and `off` do to a marquee and to a list's scroll.

## Impact

- `sdk/UrDeck.Sdk/Components`: `TextLine` (overflow parameter, additive), new `TextBlock`, `TextList`, `TextLayoutCache`
  and the helpers in `TextMetrics` (MIT; additive, so the SDK assembly version does not change).
- `sdk/UrDeck.Sdk/Rendering/WidgetRenderContext`: one new member, the effective motion level (additive; the host already
  computes it in `MotionPolicy`).
- Tests in `tests/UrDeck.Engine.Tests` (the SDK's component tests live there) and golden images for ellipsis, a wrapped
  block with a line limit, a marquee frame and a list; Windows and Linux (glyph-exact ones are `[WindowsGlyphFact]`).
- Docs: the SDK section of `CONTRIBUTING.md`; no theme keys, so the generated
  reference does not change.
- Follow-ups it unblocks: the first consumers are the label overflow in the stats and control widgets, then #121, #122 and
  #127. Those are separate changes.
- Closes #106.
