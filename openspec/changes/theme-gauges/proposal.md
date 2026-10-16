## Why

Format 2 lets a theme choose a gauge style (`meter`, `tach`, `marker`, `thickbar` beside `ring`, `bar` and `verticalBar`)
and shape it (`sweep`, `segments`, `ticks`, `scale`, `redline`, `marker`, `gradient`, `glow`, `track`, `outline`, and
more). The values are read, checked and kept, and the gauge draws none of them: a theme that asks for a tach receives a
ring. Seven of the eight drafted themes set a gauge value (Ember's tach with a redline, Blueprint's protractor dial,
Obsidian's jewel marker, Pop's outlined bar, Paper's meter, Carbon's segmented bar, Rigline's gradient ring, Prism's full
ring), and the design board shows them. Until the gauge draws them none of those themes can look like its mock.

The SDK also lacks what a gauge needs from the theme: the second accent, the colour ramp and the ink colour exist in the
theme file and not in `Theme`.

## What Changes

- **Four new styles** `meter`, `tach`, `marker` and `thickbar`, appended to `GaugeStyle` (existing values unchanged). A
  style a theme or a Stats slot asks for is drawn as asked, not as a ring.
- **The theme's gauge values are drawn:** `sweep`, `segments`, `track`, `outline`, `ticks` (with numbers), `scale`,
  `redline` (with `lightsAbove`), `marker`, `crosshair`, `gradient` (including `ramp`), `glow`, `colorBy`, `valueTint` and
  `labelPosition`. They apply where they make sense and are ignored silently elsewhere.
- **One new theme key, `gauge.layout`** (`auto` or `stacked`, default `auto`): `stacked` draws a `bar` with the readout
  above, left-aligned, and the track at the bottom, which is how the board draws Carbon's bars (see the design, decision 3).
  `meter` and `thickbar` are stacked by definition.
- **The SDK `Theme`** carries `Accent2`, `Ink`, `Ramp` and the resolved gauge values in pixels and fractions, all with
  "no effect" defaults. New members only; the package's minor version moves.
- **Stats slots** accept the four new style names.
- **v1 stays byte-identical:** a ring, bar or vertical bar under a theme that sets none of the new values draws the same
  pixels as today (the goldens and a kept copy of the v1 drawing prove it).
- Documentation: `docs/themes.md` and the site's theme and Stats pages.

Not in this change: the sparkline and the status chip (they need Stats to keep a history; see `theme-widget-parts`), the
`marker` shapes `dot` and `line` (deferred keys), animation of any kind, and the layout of the Stats card itself.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `components`: Gauge Styles and Gauge Colours change; new requirements Gauge Geometry Values and Gauge Label Position.
- `theme`: Theme Contents (a widget now receives the default gauge style whichever it is) and Format 2 Values (`layout`).
- `widget-sdk`: the Theme a widget receives carries the second accent, the ink, the ramp and the gauge values.
- `widget-stats`: a slot's style accepts the four new names.

## Impact

- `sdk/UrDeck.Sdk`: `GaugeStyle`, `Theme`, `Gauge` (split into one file per style), a `GaugeTheme` type and a small cache.
- `src/UrDeck.Engine/Themes`: `ThemeResolver` (fills the new members), `ThemeDefinition` and `ThemeFormat2.Gauge`
  (`layout`), `ThemeGaugeDefinition.Parse`, the schema `docs/schemas/v2/theme.json`.
- `widgets/UrDeck.Widgets.Stats`: the style names.
- `tests/UrDeck.Engine.Tests`: gauge tests per style, the v1 invariance test, goldens of the sample page under the drafted
  themes' gauge values (as fixtures; the themes are not built in yet).
- Cost: gauges repaint when a value changes, not per frame; a gradient shader and a blurred glow are cached per size
  (design, decision 6).
