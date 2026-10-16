## Context

The theme file has the values (`chip`, `divider`, `well`, `gauge.sparkline`, `gauge.status`: all in `ThemeFormat2.Parts.cs`
and `ThemeFormat2.Gauge.cs`, merged and sanitised) and no component draws them. The first-party widgets are Stats (a
list of slots, each a gauge or a text, eased fill, `[RefreshOnData]`), Clock (`simple` and `flap` styles, drawn once a
minute, `IsAnimating` only while a flap flips) and Weather (a 4x2 card; at 4x4 a rule, a strip of now, a snow total and a
column per forecast day). Weather already draws a rule with a theme-unaware paint (`TextMuted` at alpha 0x55, one pixel
or 0.15 of the gap), and has its own `motion` setting (`periodic`, `full`, `off`); the Clock has none. The host's motion
policy (`motion.level`, a user `reduceMotion`) is the later `motion` change.

The board (`themes-1-...png`, `themes-2-...png`, in the original package) is the target: Carbon's header row of a label and
a status word, Rigline's header (date left, "RIG ONLINE" right), Paper's week number, Obsidian's two-part date, Pop's pill and
tiles.

## Goals / Non-Goals

**Goals:**
- The parts the drafts use are drawn by SDK components, so a third-party widget can use them.
- A first-party widget under a v1 theme, or with no new option set, is byte-identical (goldens).
- Costs stay small: no history unless it is shown, no per-second repaint unless asked.

**Non-Goals:**
- Themed icons, glyph shortcuts (`theme-icons`); the dimension divider's printed label; deferred keys (`chip.shape`
  `rect` and `bracket`, `sparkline.fillAlpha`); a session timer; the motion policy.
- A history kept by the host for every reading (decision 3).

## Decisions

### 1. Four small static components, the theme's data on `Theme`

`Chip`, `Divider`, `Well` and `Sparkline` are static classes in `UrDeck.Sdk.Components` with `Draw(canvas, theme, rect, ...)`,
like the others, each reading a read-only value on `Theme` (`Theme.Chip`, `Divider`, `Well`, plus the sparkline and status
values on the `GaugeTheme` that `theme-gauges` adds). `ThemeResolver` fills them in pixels from the cell size. A theme with
none gives values that make each component draw the v1 look or nothing: the chip draws plain text, the divider draws the
Weather rule (decision 2), the well draws nothing, the sparkline needs the widget to ask.

### 2. The divider's default is the rule Weather draws today

So that Weather can switch to `Divider.Draw` without a pixel moving under v1 themes, a theme without a divider gives a
default divider equal to Weather's current paint: `TextMuted` at 0x55 alpha, `max(1, gap * 0.15)` pixels. A test compares
the two on the three v1 themes. This is the same pattern as the typography change's kept copy of the v1 text code.

### 3. Stats keeps its own history, in the widget

The widget has no hook for readings arriving; it is asked to draw when `NeedsRender` sees a changed value. So the history
is appended at those draws: one value per second at most (the latest in that second), at most `historySeconds` (10 to 600)
of them, as a small ring of fractions per gauge slot, allocated only while `gauge.sparkline.enabled` is set and the
position is at least 0.9 of a cell tall. Memory is at most 600 floats a slot (2.4 KB). The weakness is a gap while the card
is on a page that is not shown, and a line that is of what the card showed, not of every sample.

*Alternative: the host keeps a history of every reading in `ReadingHub`.* Better data and also useful to a future widget,
but it is a data-provider and engine change with its own cost (a buffer for every subscribed reading), and the
sparkline is used by one draft theme. Left for the day a second widget needs it; the widget-local buffer is behind one
method, so a hub-provided history can replace it.

### 4. The gauge draws the status and the sparkline; Stats supplies them

`GaugeOptions` gains `History` (fractions) and `Status` (text and level). The gauge puts the label at the left and the
status chip at the right of one row above the readout (Carbon's board layout), and the sparkline under the readout; with
neither given the gauge is as `theme-gauges` leaves it. Stats computes the status from the theme's labels and thresholds
(fractions) unless the slot has its own `warning` and `critical`, whose levels win; this is the only place the status is
decided, so another widget can do it differently.

### 5. The Clock's header is opt-in, and the per-second repaint is opt-in

`header` absent keeps the v1 layout (time above, date below). A header row (`date`, `label`, `none`) changes the layout, so
it is a setting, not a theme value: the board's themes do not all want one. The site decks of the themes set it. `showSeconds`
and `blinkSeparator` make the Clock draw once a second (`NeedsRender` true each second, `IsAnimating` unchanged), which
costs one small repaint of one card per second; the budget is checked against today's once-a-minute Clock (task 5.3).
`blinkSeparator` has nothing to obey yet: when the `motion` change adds a policy to `Theme`, it will make the colon steady.
The flap style takes the header and the options; the theme's value fill and glow apply to the digits of the `simple` style
only (documented).

### 6. Weather uses the parts where it already has the places for them

The forecast rule becomes `Divider.Draw`. A well is drawn behind each forecast column's rectangle (the columns are laid out
already; the well is the rectangle the column uses, inflated by a part of the gap). The condition text, today a `TextLine`,
is drawn by `Chip.Draw` in the rectangle it already has, which with the shape `none` is a plain text line in the same
role, so v1 does not move. The 4x2 card has no forecast and no rule and is unaffected by divider and well.

## Risks / Trade-offs

- [The Clock's header changes where the date sits, and the mocks differ per theme (Obsidian splits the date)] → the header
  has three modes and two texts; Obsidian's "TUESDAY / 13 OCTOBER" is `header: "label"` with the subtitle `Tuesday` and the
  date, reached by the site deck; the exact strings are the deck's, not the widget's. Whatever a theme change finds missing
  is a finding for that change.
- [The history has gaps and is of the repaints] → documented; a hub-provided history can replace it later (decision 3).
- [A per-second repaint costs CPU] → opt-in, one card; measured against the once-a-minute baseline before ticking task 5.3.
- [The status chip and the label compete for width on a 1x1 card] → the chip shrinks its text to fit and the label has the
  rest; a 1x1 card has no gauge row, and the status is drawn only at gauge positions.
- [`Well` behind the forecast days may clip an icon or a temperature] → the column's contents are laid out as before and
  the well is drawn around them; checked on the sample page for Rigline's and Pop's values.

## Migration Plan

None for users. No existing configuration, theme or widget draws differently; the new options and the part values are all
optional. This change should follow `theme-gauges`, which it builds on.

## Open Questions

- The week number's text (`Wk 42`) is English; a localised form can come with the rest of the widgets' strings.
