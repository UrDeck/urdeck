## MODIFIED Requirements

### Requirement: Format 2 Values
The host MUST accept the following groups in a theme, each documented key by key, with its range and default, in the
theme reference and in the published schema. A value is read, checked, merged and kept on the loaded theme by this
capability; what is drawn with it is specified by the capabilities that draw it.

- Colours: `accent2`, `ramp` (two to five colours), `cardTints` (one to eight colours), `ink`
- Card chrome: `shape` (`round` or `chamfer`), `cut`, `cutCorners`, `fillGradient`, `borderStyle` (`solid`, `dashed`
  or `none`), `borderDash`, `borderGradient`, `borderAnimate`, `cornerMarks` (`bracket` or `tick`), `shadow`, `glow`,
  `highlight`, `sheen`, `accentStrip`, `tint` (`none` or `cycle`), and `compact`, a partial override of the card values
  for cards of one grid cell or less and for dock slots
- Page: `pattern` (`dots`, `grid`, `hatch` or `scanlines`, with its spacing, thickness, colour and, for a grid, the
  heavier lines)
- Section parts: `divider`, `chip` and `well`
- Typography: per-role fonts for `value`, `unit` and `label`, `valueScale`, `case`, `tracking`, `slant`, `valueFill`
  and `valueGlow`
- Gauge: the styles `meter`, `tach`, `marker` and `thickbar`, `sweep`, `segments`, `outline`, `ticks`, `scale`,
  `redline`, `gradient`, `glow`, `marker`, `crosshair`, `track`, `labelPosition`, `layout` (`auto` or `stacked`),
  `colorBy`, `valueTint`, `sparkline` and `status`
- Icons: `set`, `stroke`, `tint`, `animated`, `glow`
- Background: `default` (a background's name), `static` (a gradient, a colour or a picture file) and `scrim`
- Motion: `level`, the ceiling for animation a theme drives

The product has set some values aside for a later change; they are listed with their definitions in
`docs/design/theme-format-2-deferred-keys.md` and are not part of this requirement.

#### Scenario: Card chrome values
- **WHEN** a theme sets `card.shape` to `chamfer`, `card.cut` to 0.14 and `card.cutCorners` to `["tr", "bl"]`
- **THEN** the loaded theme has those values, and the cards are drawn with them as `widget-card` specifies

#### Scenario: Compact override
- **WHEN** a theme sets `card.compact.shape` to `circle`
- **THEN** the loaded theme has a compact override with that shape and the other compact values absent

#### Scenario: Value from a group the product does not draw yet
- **WHEN** a theme sets `icons.set` to `outline`
- **THEN** the value is kept and no warning is logged
