## Why

The design board shows more than cards and gauges: Carbon's cards carry a short history line and an OK / WARM / HOT word
beside the label, Pop's weather card has a yellow outlined pill and outlined tiles for the forecast days, Rigline sets
its forecast days on a faint rounded backing, Paper and Obsidian separate sections with hairline rules, Blueprint with a
dimension line, and the clocks show a header row (a label, the date, the week, "Rig online") and small seconds. The theme
file already holds the values for all of it (`chip`, `divider`, `well`, `gauge.sparkline`, `gauge.status`, and the SDK has
no component that draws them) and the first-party widgets have no options that ask for it.

`theme-gauges` draws the gauge shapes. This change draws the parts around them and gives the three first-party widgets
that need them (Stats, Clock, Weather) the options and the code to use them. Each draft theme's look depends on these
parts as much as on its cards.

## What Changes

- **Four SDK components:** `Chip` (a status label: plain text, or a pill, tinted by level), `Divider` (the rule between
  sections: `line`, `gradient`, `dimension`), `Well` (a recessed rounded rectangle behind grouped content) and `Sparkline`
  (a polyline of recent values). Each reads the theme's values and, with none set, draws what the first-party widgets draw
  today or nothing.
- **Gauge sparkline and status.** The gauge can draw a sparkline under its readout and a status chip beside its label when
  the widget gives it the history and the status text.
- **Stats:** a short history per slot (kept only when the theme turns the sparkline on and the card is large enough) and a
  status word from the theme's thresholds, or the slot's own warning and critical values.
- **Clock:** the options `showSeconds`, `showWeek`, `subtitle`, `blinkSeparator` and `header` (`date`, `label` or `none`),
  all absent by default, which keeps today's layout and never animates.
- **Weather:** the rule at 4x4 is drawn by `Divider`, the forecast days sit on the theme's `well`, and the condition on the
  theme's `chip`; with none of them set the card is drawn as before.
- **SDK `Theme`** carries the resolved `Chip`, `Divider`, `Well`, `Sparkline` and `Status` values; new members only.
- Documentation of the new widget options and parts.

Not in this change: themed and glyph icons (`theme-icons`), the dimension divider's printed label (the widgets have no
text to give it yet), `chip.shape` values `rect` and `bracket` and `sparkline.fillAlpha` (deferred keys), a real
session timer for the clock's subtitle (the subtitle is plain text), any theme-driven animation beyond the blinking
separator and the seconds.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `components`: new requirements for Chip, Divider, Well, Sparkline, and for the gauge's sparkline and status.
- `widget-stats`: history and status.
- `widget-clock`: the new options and the header layout.
- `widget-weather`: sections drawn with the divider, well and chip.
- `widget-sdk`: the Theme carries the part values.

## Impact

- `sdk/UrDeck.Sdk`: new components `Chip`, `Divider`, `Well`, `Sparkline`; `Theme` members; `GaugeOptions` (history, status).
- `src/UrDeck.Engine/Themes`: `ThemeResolver` fills the part values (no model change: the keys exist).
- `widgets/UrDeck.Widgets.Stats`, `.Clock`, `.Weather`, and their config classes (all new keys optional).
- `tests/UrDeck.Engine.Tests`: component, widget and golden tests; the three v1 themes' goldens must not change.
- Cost: one history buffer of at most 600 numbers per sparkline slot; the Clock repaints once a second only when it shows
  seconds or a blinking separator (design, decision 4).
- Depends on `theme-gauges` for the gauge changes (the sparkline and the status are drawn by the gauge).
