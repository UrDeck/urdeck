# Themes

A theme is a folder holding one settings file and, optionally, font files. The theme decides how every card on a page
looks; widgets only draw content. Three themes are built in (`default-dark`, `default-light` and `glass`, frosted translucent cards on a blue background). Your own go in a `themes`
folder next to `UrDeck.Host.exe`:

```
themes/
  my-theme/
    theme.json
    MyFont.ttf        (optional, referenced from theme.json)
```

Select a theme with the `theme` key in `urdeck-config.json`. Themes are re-read whenever the config is reloaded, so
after editing a theme, save the config file to apply it. A theme with the same name as a built-in one replaces it.

## Settings file

Every value is optional. Anything you leave out comes from `default-dark`, so a theme can be a few lines. An invalid
value (for example a colour that does not parse) is replaced by the default's value and a warning is written to
`urdeck.log`; a file that is not valid JSON makes the theme unavailable, and the default is used.

```json
{
  "colors": {
    "background": "#0f0f1a", "cardFill": "#b31a1a2e", "cardBorder": "#00000000",
    "text": "#ffffff", "textMuted": "#b3ffffff",
    "accent": "#4a9eff", "accentDim": "#664a9eff",
    "good": "#3ddc84", "warning": "#ffb020", "critical": "#ff5a4d"
  },
  "card": { "radius": 0.08, "borderWidth": 0, "gap": 0.06, "padding": 0.065 },
  "typography": {
    "font": "InterVariable.ttf",
    "weights": { "value": 600, "unit": 500, "label": 400, "body": 400, "title": 300 },
    "labelSize": 0.091, "bodySize": 0.0845, "titleSize": 0.11,
    "unitRatio": 0.4
  },
  "stroke": { "thickness": 0.08, "cap": "round" },
  "gauge": { "style": "ring" },
  "indicator": {
    "bandHeight": 0.25, "dotSize": 0.05, "pillLength": 0.14, "spacing": 0.05,
    "active": "#ffffff", "inactive": "#59ffffff", "backdrop": "#8c0f0f1a"
  },
  "press": { "scale": 0.96, "opacity": 0.85 },
  "dock": { "height": 0.71 }
}
```

- **Colours** are `#RRGGBB` or `#AARRGGBB` (alpha first).
- **Sizes** (`card.*`, `typography.*Size`) are fractions of one grid cell (a quarter of the screen width), so a theme
  looks the same on any display. `card.radius` is limited to half of a card's smaller side; `0` gives flat tiles.
  `card.gap` is the space between neighbouring cards.
- **`typography.font`** is either an installed font family name or the name of a `.ttf`/`.otf` file in the theme folder.
  A path that leaves the theme folder is rejected. Weights (1 to 1000) are applied through the variable-font weight
  axis; a static font is used as it is. A weight the file cannot make is warned about once (see `typography` under
  Format 2).
- **`typography.unitRatio`** is the size of a readout's unit as a fraction of its value's size.
- **`stroke`** is for components that draw lines: thickness as a fraction of the drawn element and `round` or `square`
  ends.
- **`gauge.style`** is the shape a gauge takes when a widget asks for "the theme's" gauge: `ring`, `bar` or `verticalBar`
  (format 2 adds `meter`, `tach`, `marker` and `thickbar`, see [Format 2](#format-2); a widget that asks for one of those
  receives a `ring` until the gauge can be drawn that way). `plain` is not allowed here (a gauge that draws no shape is no
  gauge); it and any unknown name fall back to `ring` with a logged warning. A widget's own explicit style wins over it.

- **`indicator`** is the page indicator (see [Pages](https://urdeck.app/docs/#pages)). `bandHeight`, `dotSize`, `pillLength` and `spacing`
  are fractions of one grid cell: `bandHeight` is the height of the band reserved at the bottom of the screen when the
  indicator is always shown, `dotSize` is the diameter of a dot (and the height of the pill), `pillLength` is the length
  of the current page's pill and `spacing` is the gap between neighbouring marks. `active` is the pill's colour,
  `inactive` the other dots', and `backdrop` the translucent pill behind a floating indicator (`fade` mode). A theme
  written before the indicator existed takes all of these from `default-dark`, with no warning.
- **`press`** is how a card looks while a finger is down on a widget that accepts taps (a shortcut): `scale` is the
  card's size and `opacity` its opacity, each as a ratio where `1` means unchanged. The whole card, border included, is
  scaled around its centre and dimmed by the compositor; the widget is not repainted and draws no pressed state of its
  own. `scale` must be between `0.5` and `1` and `opacity` between `0.1` and `1`; a value outside that is replaced by
  the default's with a logged warning. Set both to `1` to switch the feedback off. The built-in themes use `0.96` and
  `0.85`, settled by eye on the Y70 panel. A theme written before these existed
  takes them from `default-dark`, with no warning. How fast the card moves is not a theme value.
- **`dock`** is the dock (see [The dock](https://urdeck.app/docs/#the-dock)). `height` is the height of the band reserved for it at the bottom
  of the screen, as a fraction of one grid cell, and also the side of a dock slot: the theme's sizes (`card.*`,
  `typography.*Size`) are applied to a slot as they are to a grid cell, so a docked card is a miniature whose corners,
  padding and text shrink with this value. It must be between `0.2` and `1`; a value outside that is replaced by the
  default's with a logged warning. The built-in themes use `0.71`: on the 1100x3840 panel that is 195 pixels, which
  together with the indicator's band of 69 fits in the 265 pixels left under the thirteenth row, so the dock costs no
  row there; the owner approved the size by eye on that panel. A larger value takes a row there. A theme written before the dock existed takes the value from
  `default-dark`, with no warning.

- **`colors.backgroundPalette`** is the palette the [background](backgrounds.md) shaders draw with: one to four colours
  (`#RRGGBB`; fewer than four repeat the last, transparency is ignored). See [Background palette](#background-palette).

A gauge draws its fill in `colors.accent`, or in `colors.warning` or `colors.critical` once its reading has reached
that level, and its track in the same colour at the alpha of `colors.accentDim`. A reading that is not current (stale,
pending, unavailable) uses `colors.textMuted` for both. The number keeps `colors.text`, except where it lies over the
fill of a vertical bar: there it is `colors.text` or `colors.background`, whichever contrasts more with the fill.
Thickness and line ends come from `stroke`.

## Background palette

The built-in background shaders (`plasma`, `aurora`, `gradient`, `julia` and the twelve others, see `docs/backgrounds.md`) draw with four colours that come from the theme.
`colors.backgroundPalette` sets them; the built-in themes have:

| Theme | `backgroundPalette` |
|---|---|
| `default-dark` | `#0f0f1a`, `#1a2350`, `#3a2a6e`, `#24507a` |
| `default-light` | `#e9ebf2`, `#b9ccf2`, `#d9b8ee`, `#a6dbee` |
| `glass` | `#131c4f`, `#1b2760`, `#3a3f9c`, `#6b3fa0` |

**It is the one value that is not taken from `default-dark`.** Every other value you leave out falls back to the default
theme's, but a light theme written before backgrounds existed would then get the dark palette. A theme without
`backgroundPalette` gets one derived from its own colours instead: `colors.background` mixed towards `colors.accent` by 0,
20, 40 and 60%, component by component. No warning is logged for that. A `backgroundPalette` that is not an array of one to
four valid colours is dropped with a warning, and the derived palette is used. The `colors` of the `background` setting
in `urdeck-config.json` win over both.

Keep the palette dark and low in contrast unless the theme's cards are opaque: a bright, busy background under
translucent cards makes their text hard to read.

## Format 2

Format 2 adds optional values to the theme file for cards, type, gauges, icons and a background of its own. A theme can use any of them without declaring anything; `"format": 2` says that the file does, and a number above 2 logs one warning and still loads. [`docs/schemas/v2/theme.json`](schemas/v2/theme.json) is the same list as a JSON Schema (point `$schema` at `https://urdeck.app/schemas/v2/theme.json` for completion in an editor).

- **Everything is optional, and absent means off.** A theme written before format 2 loads with the same values, the same warnings and the same drawing as before. Merging a theme over `default-dark` never adds a format 2 group.
- **A value is read, checked and kept; some are not drawn yet.** The tables say which change draws each group. Until then a value is accepted without a warning and has no effect on the screen. The gauge styles `meter`, `tach`, `marker` and `thickbar` are kept and a widget that asks for the theme's gauge gets a `ring`.
- **An invalid value is replaced by the default** (for an optional value, by "absent") and one warning in `urdeck.log` names the theme, the key and the value. A number outside its range, a colour that does not parse, a name that is not listed, a value of the wrong kind (text where a number goes) and a list with a bad item are all invalid; a bad item drops the whole list, except in `card.cutCorners` and `card.cornerMarks.corners`, where an unknown name is dropped and the others stay. Names are read in any case.
- **Some groups are never taken from `default-dark`:** `colors.ramp`, `colors.cardTints`, `colors.backgroundPalette`, `pattern`, `background`, `icons`, `meta`, every card chrome value, `typography.valueFill` and `valueGlow`, and every gauge value except `style`. The rest merge key by key, like the format 1 values.
- **Sizes** are fractions of one grid cell, **angles** are degrees (0 is left to right or 3 o'clock, clockwise), **times** are seconds (1 or more), **tracking** is in em, and **colours** are `#RRGGBB` or `#AARRGGBB`.

Rules between values, checked after each value on its own (each warns once and changes only what it names):

- `card.borderStyle`, `borderGradient` or `borderAnimate` with a border width of 0 has no effect: warned.
- `card.borderAnimate` with `mode` `spin` needs a `borderGradient` of kind `conic`; without one the animation is switched off.
- `pattern.spacing` less than three times `pattern.thickness` makes the marks run together: warned.
- `card.cornerMarks.inset` below 0 reaches outside the card; with `card.gap` smaller than that the marks would overlap the next card: warned.
- `card.shape` `chamfer` with `card.compact.shape` `circle` is allowed; the circle applies to compact cards only.
- More than one continuous animation is allowed and written to the log at information level.

### `meta`

Drawn by: nothing; it describes the theme to the settings window.

| Key | Value | Default | Meaning |
|---|---|---|---|
| `meta.name` | text |  | Display name for the editor. |
| `meta.description` | text |  |  |
| `meta.author` | text |  |  |
| `meta.license` | text |  |  |
| `meta.version` | text |  |  |
| `meta.mode` | `dark`, `light` |  | Lets the editor group themes and pick a matching default. |
| `meta.tags` | list of text |  |  |
| `meta.pairsWith` | group |  | Recommendations for the settings editor. They never decide what the deck shows. |
| `meta.pairsWith.backgrounds` | list of text |  | Background names that suit this theme, best first. Names of built-in or user backgrounds; a name that does not exist is skipped. A background's manifest can name themes too (`pairsWith.themes`); a pair is recommended if either side names the other. The theme's default background belongs in this list. |
| `meta.samples` | list of groups |  | Same shape as a background manifest's samples. |
| `meta.samples[].file` | text |  |  |
| `meta.samples[].caption` | text |  |  |

### `colors`

Drawn by: the changes that use them.

| Key | Value | Default | Meaning |
|---|---|---|---|
| `colors.accent2` | colour |  | A second accent for gradients, markers, redlines, tab highlights. Default: accent. |
| `colors.ramp` | list of 2 to 5 colours |  | Low-to-high load colours. Default: [good, warning, critical]. Used when gauge.colorBy is 'ramp'. |
| `colors.cardTints` | list of 1 to 8 colours |  | Fill colours that cards cycle through when card.tint is 'cycle' (Pop). |
| `colors.ink` | colour |  | Text colour used on top of an accent or tint fill. Default: whichever of text/background contrasts most with the fill. |

### `card`

Drawn today: the shape (`shape`, `cut`, `cutCorners`), the fill (`fillGradient`, `tint`) and the border (`borderStyle`,
`borderDash`, `borderGradient`). Kept and drawn by the later card chrome changes: `borderAnimate`, `cornerMarks`, `shadow`,
`glow`, `highlight`, `sheen`, `accentStrip` and `compact`.

- **Shape.** `round` is the rounded rectangle of `card.radius`, limited to half the card's smaller side. `chamfer` cuts the
  corners in `cutCorners` with a straight line `cut` along each side, limited to half the smaller side; the other corners are
  square and `radius` is not used. An empty `cutCorners` gives square cards. The widget's content is clipped to the shape.
- **Fill**, first that applies: `fillGradient` (linear across the card at its angle, the colours evenly spaced); `tint`
  `cycle` with `colors.cardTints`; `colors.cardFill`. Without `cardTints` a tinted theme uses the card fill. The text colour does
  not change with the tint.
- **Tint order.** A card takes the tint at its index, counting round the list. The index is its place among its page's placed,
  visible cards, row by row and then column by column of the grid, from the first tint on every page; a hidden widget, or one
  that does not fit, takes none. A dock slot takes its position in the dock, whether or not the slots before it show a widget.
- **Border**, always inside the card's edge, in `card.borderWidth`: `solid`, `none`, or `dashed` with `borderDash` (`on` and
  `off`; three and two border widths when absent). A dashed border has a dash centred on every corner: on each side the dash
  and the gap are scaled together so that a whole number of them fits. `borderGradient` colours the border, solid or dashed:
  `linear` at its angle, `conic` once round the card's centre, starting at the right. A border width of 0 draws nothing.
- **Hairlines.** In a theme that sets any card value of format 2, a border of 2 pixels or less is drawn at a whole number of
  pixels, never under 1; under one pixel it is written to `urdeck.log` once. A theme with no such value draws its cards
  exactly as before, unrounded.
- A fill gradient is drawn once per card size and reused, so a repaint costs about what it does with a plain fill
  ([`docs/perf/card-chrome.md`](perf/card-chrome.md)).

| Key | Value | Default | Meaning |
|---|---|---|---|
| `card.shape` | `round`, `chamfer` | `"round"` | 'chamfer' cuts corners with straight lines (Ember). Default round. |
| `card.cut` | 0 to 0.5 | `0.14` | Chamfer size, fraction of a cell, capped at half the smaller side. |
| `card.cutCorners` | list of names of `tl`, `tr`, `br`, `bl` | `["tr","bl"]` | Which corners are cut. Default [tr, bl]. |
| `card.fillGradient` | group |  | Replaces cardFill with a gradient. |
| `card.fillGradient.colors` | list of 2 to 6 colours |  | Stops, evenly spaced. |
| `card.fillGradient.angle` | -360 to 360 | `90` | Degrees, 0 = left to right, 90 = top to bottom. |
| `card.borderStyle` | `solid`, `dashed`, `none` | `"solid"` | Default solid. |
| `card.borderDash` | group |  | Dash pattern for dashed/dotted. |
| `card.borderDash.on` | 0.002 to 1 |  | Dash length. |
| `card.borderDash.off` | 0.002 to 1 |  | Gap length. |
| `card.borderGradient` | group |  | Gradient border (Rigline, Ember, Obsidian hairline). |
| `card.borderGradient.colors` | list of 2 to 8 colours |  | Stops. |
| `card.borderGradient.kind` | `linear`, `conic` | `"linear"` | conic walks round the card, so it can rotate. |
| `card.borderGradient.angle` | -360 to 360 | `135` | Linear only. |
| `card.borderAnimate` | group |  | Animated border. Bounded by motion.level. |
| `card.borderAnimate.mode` | `none`, `spin`, `pulse` | `"none"` | spin rotates a conic gradient; pulse breathes the alpha. |
| `card.borderAnimate.period` | 1 to 120 | `7` | Seconds per cycle. |
| `card.cornerMarks` | group |  | Registration marks (Carbon, Blueprint). Drawn outside the clip, so needs a bleed margin. |
| `card.cornerMarks.style` | `none`, `bracket`, `tick` | `"none"` | Shape of each mark. |
| `card.cornerMarks.corners` | list of names of `tl`, `tr`, `br`, `bl` |  | Default [tl, br]. |
| `card.cornerMarks.size` | 0.01 to 0.5 |  | Arm length. |
| `card.cornerMarks.thickness` | 0.002 to 0.1 |  | Line width. |
| `card.cornerMarks.inset` | -0.1 to 0.1 | `0` | Offset from the corner; negative sits outside the card. |
| `card.cornerMarks.color` | colour |  | #RRGGBB or #AARRGGBB (alpha first, as in v1). |
| `card.shadow` | group |  | Drop shadow. Bleeds outside the card. |
| `card.shadow.x` | -0.5 to 0.5 | `0` | Offset. |
| `card.shadow.y` | -0.5 to 0.5 | `0` | Offset. |
| `card.shadow.blur` | 0 to 1 |  | 0 = hard offset (Pop). |
| `card.shadow.spread` | -0.5 to 0.5 | `0` |  |
| `card.shadow.color` | colour |  | #RRGGBB or #AARRGGBB (alpha first, as in v1). |
| `card.glow` | group |  | Outer glow. Bleeds outside the card. |
| `card.glow.blur` | 0 to 1 |  | Blur radius, fraction of a cell. |
| `card.glow.color` | colour |  | #RRGGBB or #AARRGGBB (alpha first, as in v1). |
| `card.glow.alpha` | 0 to 1 | `0.6` | Strength. |
| `card.highlight` | group |  | Inset edge light (Prism). |
| `card.highlight.top` | colour |  | Specular line along the top edge. |
| `card.highlight.bottom` | colour |  | Rim light along the bottom, inside. |
| `card.highlight.thickness` | 0.001 to 0.1 | `0.01` |  |
| `card.sheen` | group |  | A radial light patch inside the card (Prism). |
| `card.sheen.color` | colour |  | #RRGGBB or #AARRGGBB (alpha first, as in v1). |
| `card.sheen.x` | -1 to 2 | `0.1` | Centre, fraction of card width. |
| `card.sheen.y` | -1 to 2 | `0` | Centre, fraction of card height. |
| `card.sheen.size` | 0.1 to 3 | `0.7` | Radius, fraction of card width. |
| `card.accentStrip` | group |  | A lit line along one edge of every card, inside the clip (Carbon scan line, Rigline tile strips). |
| `card.accentStrip.side` | `top`, `bottom`, `left`, `right` | `"bottom"` |  |
| `card.accentStrip.thickness` | 0.005 to 0.2 | `0.03` |  |
| `card.accentStrip.length` | 0.1 to 1 | `0.72` | Fraction of the side. |
| `card.accentStrip.colors` | list of 1 to 8 colours |  | Cycled by slot index (compact cards) or by card index. Default [accent]. |
| `card.accentStrip.fade` | true or false |  | Fade to transparent along the length (Carbon's scan line). |
| `card.accentStrip.glow` | group |  | A soft glow. color omitted = the thing's own colour. |
| `card.accentStrip.glow.blur` | 0 to 1 |  | Blur radius, fraction of a cell. |
| `card.accentStrip.glow.color` | colour |  | #RRGGBB or #AARRGGBB (alpha first, as in v1). |
| `card.accentStrip.glow.alpha` | 0 to 1 | `0.6` | Strength. |
| `card.tint` | `none`, `cycle` | `"none"` | 'cycle' fills cards from colors.cardTints in page order. |
| `card.compact` | group |  | A partial override applied to cards of 1x1 cell or less and to dock slots. |
| `card.compact.radius` | 0 to 1 |  |  |
| `card.compact.borderWidth` | 0 to 1 |  |  |
| `card.compact.shape` | `round`, `chamfer`, `circle` |  | 'circle' makes square cards round (Obsidian dock). |
| `card.compact.accentStrip` | group |  | A lit strip on compact cards (Rigline tiles). Overrides card.accentStrip for them. |
| `card.compact.accentStrip.side` | `top`, `bottom`, `left`, `right` | `"bottom"` |  |
| `card.compact.accentStrip.thickness` | 0.005 to 0.2 | `0.03` |  |
| `card.compact.accentStrip.length` | 0.1 to 1 | `0.72` | Fraction of the side. |
| `card.compact.accentStrip.colors` | list of 1 to 8 colours |  | Cycled by slot index (compact cards) or by card index. Default [accent]. |
| `card.compact.accentStrip.glow` | group |  | A soft glow. color omitted = the thing's own colour. |
| `card.compact.accentStrip.glow.blur` | 0 to 1 |  | Blur radius, fraction of a cell. |
| `card.compact.accentStrip.glow.color` | colour |  | #RRGGBB or #AARRGGBB (alpha first, as in v1). |
| `card.compact.accentStrip.glow.alpha` | 0 to 1 | `0.6` | Strength. |
| `card.compact.cornerMarks` | group |  | Registration marks on compact cards. Overrides card.cornerMarks for them. |
| `card.compact.cornerMarks.style` | `none`, `bracket`, `tick` | `"none"` | Shape of each mark. |
| `card.compact.cornerMarks.corners` | list of names of `tl`, `tr`, `br`, `bl` |  | Default [tl, br]. |
| `card.compact.cornerMarks.size` | 0.01 to 0.5 |  | Arm length. |
| `card.compact.cornerMarks.thickness` | 0.002 to 0.1 |  | Line width. |
| `card.compact.cornerMarks.inset` | -0.1 to 0.1 | `0` | Offset from the corner; negative sits outside the card. |
| `card.compact.cornerMarks.color` | colour |  | #RRGGBB or #AARRGGBB (alpha first, as in v1). |

### `pattern`

Drawn by: the card chrome change.

| Key | Value | Default | Meaning |
|---|---|---|---|
| `pattern.kind` | `none`, `dots`, `grid`, `hatch`, `scanlines` | `"none"` | Default none. |
| `pattern.spacing` | 0.02 to 2 | `0.13` | Distance between marks, fraction of a cell. |
| `pattern.majorEvery` | whole number, 2 to 20 |  | Every Nth line of a grid is drawn heavier (Blueprint: 5). |
| `pattern.thickness` | 0.001 to 0.1 | `0.01` | Line width / dot radius. |
| `pattern.color` | colour |  | #RRGGBB or #AARRGGBB (alpha first, as in v1). |
| `pattern.major` | group |  | Style of the heavier lines. |
| `pattern.major.color` | colour |  | #RRGGBB or #AARRGGBB (alpha first, as in v1). |
| `pattern.major.thickness` | 0.001 to 0.1 |  |  |
| `pattern.angle` | -180 to 180 | `45` | hatch/lines direction, degrees. |

### `divider`

Drawn by: the widget parts change.

| Key | Value | Default | Meaning |
|---|---|---|---|
| `divider.style` | `none`, `line`, `gradient`, `dimension` | `"line"` | dimension = a line with end ticks and an optional centred label (Blueprint). |
| `divider.color` | colour |  | #RRGGBB or #AARRGGBB (alpha first, as in v1). |
| `divider.thickness` | 0.001 to 0.05 | `0.01` |  |
| `divider.gradientTo` | colour |  | #RRGGBB or #AARRGGBB (alpha first, as in v1). |

### `well`

Drawn by: the widget parts change.

| Key | Value | Default | Meaning |
|---|---|---|---|
| `well.fill` | colour |  | #RRGGBB or #AARRGGBB (alpha first, as in v1). |
| `well.radius` | 0 to 0.5 | `0.09` |  |
| `well.border` | colour |  | #RRGGBB or #AARRGGBB (alpha first, as in v1). |

### `chip`

Drawn by: the widget parts change.

| Key | Value | Default | Meaning |
|---|---|---|---|
| `chip.shape` | `none`, `pill` | `"none"` |  |
| `chip.fill` | colour |  | #RRGGBB or #AARRGGBB (alpha first, as in v1). |
| `chip.border` | colour |  | #RRGGBB or #AARRGGBB (alpha first, as in v1). |
| `chip.textColor` | colour |  | #RRGGBB or #AARRGGBB (alpha first, as in v1). |
| `chip.colorBy` | `none`, `status` | `"status"` | status tints the chip by good/warning/critical. |

### `typography`

Drawn by: the `Readout` and `TextLine` components of the SDK, so every widget that uses them, and the resolver (fonts, optical
size). The split-flap clock's digits, a gauge's own centre text and an image tile's letter take the role's font and optical
size, but not the case, spacing, slant, fill or glow.

- **Fonts per role.** `fonts.value`, `fonts.unit` and `fonts.label` name a family or a file in the theme folder for that role;
  a role without one, and the body and title, use `typography.font`. A role whose font cannot be loaded falls back one step at a
  time: the role's font, `typography.font`, the default theme's font (Inter), the system font. Each missing font is one warning
  per loaded theme, naming the theme, the role and the font, however often the theme is resolved (the page and the dock each
  resolve it); editing and saving the theme loads it again and warns again. A file path that leaves the theme folder is refused
  for each role. Roles that end up with the same font and weight share one typeface.
- **Weights the font cannot make.** A weight outside a variable font's weight axis is drawn at the nearest end of the axis, and
  a weight on a single-weight file is drawn at the file's own weight; each is one warning per role per loaded theme (for example
  `Theme 'ember': the body weight 500 has no effect: font 'BarlowCondensed-Bold.ttf' has one weight (700).`). An installed family
  is not checked. The built-in themes log nothing.
- **`opticalSize`.** `off` (the default, and every theme written before it) leaves a font's optical size axis (`opsz`) where the
  font puts it. `auto` sets it for each role whose font has the axis: the top of the axis (the display cut) for values and units,
  which are drawn large, and the role's size in pixels, limited to the axis, for labels, body and titles. A Didone such as Bodoni
  Moda needs it for its hairlines at large sizes; a font without the axis is unaffected.
- **Case** applies to a readout's label and to a text line at the label step (`case.label`) and to a text line at the title step
  (`case.title`), with invariant casing, before the text is measured. Body text and values are never recased.
- **Tracking** is added after each character but the last, in em of the text's size: `value` to a readout's value (the digits
  keep their equal widths and the spacing is part of the fit, so the size does not change with the value), `label` to a readout's
  label and label lines, `title` to title lines. Body text and units are not spaced.
- **`slant`** leans a readout's value to the right about its baseline. The readout keeps the rectangle and size of the upright
  value, so the top of a slanted value reaches past the rectangle's right edge by the slant's tangent times the cap height.
- **`valueScale`** multiplies the size a widget asks of a readout, and the result is never larger than the size at which the
  value, unit and label fill the rectangle. Above 1 it enlarges only a readout a widget draws smaller than it could (a gauge's
  readout, a clock set below full size); a full-size readout stays as it is. Below 1 it shrinks every readout.
- **`valueFill`** fills a readout's value with the gradient across the value's box, at `angle`; the unit and label keep their
  colours, and a stale reading (muted) or a colour the widget gives the value replaces the gradient. It does not move.
- **`valueGlow`** draws a blurred copy behind a readout's value, in `color` (absent: the value's colour or gradient), at `alpha`.
  The copy is kept while the value, size and colour stay the same, so a repaint does not blur again.

| Key | Value | Default | Meaning |
|---|---|---|---|
| `typography.fonts` | group |  | Per-role overrides; each is a family or a file in the theme folder. Missing role falls back to typography.font. |
| `typography.fonts.value` | text |  |  |
| `typography.fonts.unit` | text |  |  |
| `typography.fonts.label` | text |  |  |
| `typography.valueScale` | 0.5 to 2 | `1` | Multiplies the readout's size (Pop and Obsidian run big). |
| `typography.case` | group |  | Letter case applied at draw time. |
| `typography.case.label` | `none`, `upper`, `lower` | `"none"` |  |
| `typography.case.title` | `none`, `upper`, `lower` | `"none"` |  |
| `typography.tracking` | group |  | Letter spacing in em. Applied per glyph at draw time; the Readout's equal-width digits are kept. |
| `typography.tracking.value` | -0.1 to 0.6 |  | em |
| `typography.tracking.label` | -0.1 to 0.6 |  | em |
| `typography.tracking.title` | -0.1 to 0.6 |  | em |
| `typography.slant` | 0 to 20 | `0` | Synthetic skew of the value, degrees. Prefer a real italic file when the family has one. |
| `typography.valueFill` | group |  | Fills the value text with a gradient (Rigline clock). |
| `typography.valueFill.colors` | list of 2 to 6 colours |  | Gradient stops. |
| `typography.valueFill.angle` | -360 to 360 | `0` |  |
| `typography.valueGlow` | group |  | Glow behind value text. |
| `typography.valueGlow.blur` | 0 to 1 |  | Blur radius, fraction of a cell. |
| `typography.valueGlow.color` | colour |  | #RRGGBB or #AARRGGBB (alpha first, as in v1). |
| `typography.valueGlow.alpha` | 0 to 1 | `0.6` | Strength. |
| `typography.opticalSize` | `off`, `auto` | `"off"` | off leaves a font's optical size axis (opsz) at its default; auto sets it per role: the top of the axis for value and unit, the role's pixel size for label, body and title. A font without the axis is unaffected. |

### `gauge`

Drawn by: the gauge change.

| Key | Value | Default | Meaning |
|---|---|---|---|
| `gauge.labelPosition` | `auto`, `inside`, `below`, `above` | `"auto"` | Where a gauge's label sits. auto keeps the v1 placement (Prism: below). |
| `gauge.sweep` | group |  | Ring/tach/marker geometry. |
| `gauge.sweep.start` | -360 to 360 | `135` | Degrees, 0 = 3 o'clock, clockwise. v1 ring: 135. |
| `gauge.sweep.span` | 30 to 360 | `270` | v1 ring: 270. |
| `gauge.segments` | group |  | Chops the fill into LED blocks. |
| `gauge.segments.length` | 0.005 to 1 |  | Segment length (bars) or arc length in cells (rings). |
| `gauge.segments.gap` | 0 to 1 |  | Space between segments. |
| `gauge.outline` | group |  | Outline round the track (thickbar). |
| `gauge.outline.width` | 0.002 to 0.2 |  |  |
| `gauge.outline.color` | colour |  | #RRGGBB or #AARRGGBB (alpha first, as in v1). |
| `gauge.ticks` | group |  | Tick marks on rings/tach/meter. |
| `gauge.ticks.count` | whole number, 2 to 60 |  |  |
| `gauge.ticks.majorEvery` | whole number, 1 to 20 |  |  |
| `gauge.ticks.length` | 0.005 to 0.5 |  |  |
| `gauge.ticks.thickness` | 0.001 to 0.05 |  |  |
| `gauge.ticks.color` | colour |  | #RRGGBB or #AARRGGBB (alpha first, as in v1). |
| `gauge.ticks.labels` | true or false |  | Print numbers at major ticks. |
| `gauge.scale` | group |  | Scale labels under a bar (Paper). |
| `gauge.scale.labels` | list of 2 to 11 text |  | Printed under a bar at even spacing, e.g. ["0","50","100"]. |
| `gauge.scale.size` | 0.005 to 0.5 |  |  |
| `gauge.redline` | group |  |  |
| `gauge.redline.from` | 0 to 1 | `0.85` | Fraction where the zone starts. |
| `gauge.redline.color` | colour |  | #RRGGBB or #AARRGGBB (alpha first, as in v1). |
| `gauge.redline.glow` | group |  | A soft glow. color omitted = the thing's own colour. |
| `gauge.redline.glow.blur` | 0 to 1 |  | Blur radius, fraction of a cell. |
| `gauge.redline.glow.color` | colour |  | #RRGGBB or #AARRGGBB (alpha first, as in v1). |
| `gauge.redline.glow.alpha` | 0 to 1 | `0.6` | Strength. |
| `gauge.redline.lightsAbove` | true or false |  | Zone only lights while the value is inside it (Ember). |
| `gauge.gradient` | `"ramp"` or a gradient |  | Fill gradient along the value: 'ramp' uses colors.ramp, or give stops. |
| `gauge.glow` | group |  | Glow round the fill. color omitted = fill colour. |
| `gauge.glow.blur` | 0 to 1 |  | Blur radius, fraction of a cell. |
| `gauge.glow.color` | colour |  | #RRGGBB or #AARRGGBB (alpha first, as in v1). |
| `gauge.glow.alpha` | 0 to 1 | `0.6` | Strength. |
| `gauge.marker` | group |  | Value marker (Obsidian jewel, Blueprint needle). |
| `gauge.marker.shape` | `diamond`, `needle` | `"diamond"` |  |
| `gauge.marker.size` | 0.005 to 0.5 |  |  |
| `gauge.marker.color` | colour |  | #RRGGBB or #AARRGGBB (alpha first, as in v1). |
| `gauge.marker.halo` | true or false |  | Dark ring round the marker. |
| `gauge.crosshair` | true or false |  | Centre cross on dials (Blueprint). |
| `gauge.track` | group |  | Overrides the default accentDim track. |
| `gauge.track.color` | colour |  | #RRGGBB or #AARRGGBB (alpha first, as in v1). |
| `gauge.track.widthRatio` | 0.1 to 2 | `1` | Track width relative to stroke.thickness. |
| `gauge.colorBy` | `accent`, `ramp` | `"accent"` | 'ramp' colours the fill by value along colors.ramp instead of the accent/warning/critical steps. |
| `gauge.valueTint` | true or false | `false` | Colours the readout with the same colour as the fill. |
| `gauge.sparkline` | group |  | A history line under the readout; needs Stats to keep a ring buffer. |
| `gauge.sparkline.enabled` | true or false |  |  |
| `gauge.sparkline.height` | 0.05 to 1 | `0.3` |  |
| `gauge.sparkline.historySeconds` | 10 to 600 | `60` |  |
| `gauge.sparkline.strokeWidth` | 0.002 to 0.1 |  |  |
| `gauge.status` | group |  | A status chip beside the label. |
| `gauge.status.enabled` | true or false |  |  |
| `gauge.status.thresholds` | list of 2 numbers |  | [warm, hot] as 0..1. Default [0.7,0.9]. |
| `gauge.status.labels` | list of 3 text |  | [ok, warm, hot]. |

### `icons`

Drawn by: the icons change.

| Key | Value | Default | Meaning |
|---|---|---|---|
| `icons.set` | text |  | Folder name inside the theme folder (icons/<set>/<key>.svg), or a built-in set name. Keys are in 04-widgets-and-icons.md. A missing key falls back to the built-in 'outline' set, then to the widget's own icon. |
| `icons.stroke` | 0.005 to 0.2 |  | Override stroke width for set icons, fraction of the icon size. |
| `icons.tint` | text |  | 'accent', 'accent2', 'text', 'ramp', 'none' (keep the SVG's colours) or a #colour. |
| `icons.glow` | group |  | Glow behind set icons (Rigline). Colour defaults to the icon's tint. |
| `icons.glow.blur` | 0 to 1 |  | Blur radius, fraction of a cell. |
| `icons.glow.color` | colour |  | #RRGGBB or #AARRGGBB (alpha first, as in v1). |
| `icons.glow.alpha` | 0 to 1 | `0.6` | Strength. |
| `icons.animated` | true or false | `true` | false = static icons even where the widget would use Lottie (weather). Default true. |

### `background`

What the deck shows behind the cards when the configuration has no `background`; see [Theme backgrounds](backgrounds.md#theme-backgrounds)
for the order (the user's choice, `"type": "none"` included, then `default`, then `static`, then `colors.background`) and
for what happens when one fails. The built-in themes define none.

| Key | Value | Default | Meaning |
|---|---|---|---|
| `background.default` | group |  | The animated background this theme selects when the user has not picked one. |
| `background.default.name` | text |  | A shader background's name, built in or a folder in `backgrounds/`. It uses the theme's background palette. |
| `background.static` | group |  | Still background used when there is no default, or when the default cannot be shown. Image, gradient or colour (the first that is given, in that order). It is drawn once. `motion.level` does not change which one shows. |
| `background.static.image` | text |  | File in the theme folder, scaled to cover the deck. A path that leaves the folder is ignored with a warning. User themes only. |
| `background.static.color` | colour |  | #RRGGBB or #AARRGGBB (alpha first, as in v1). |
| `background.static.gradient` | group |  | Linear or radial gradient. |
| `background.static.gradient.colors` | list of 2 to 6 colours |  | Stops. |
| `background.static.gradient.kind` | `linear`, `radial` | `"linear"` |  |
| `background.static.gradient.angle` | -360 to 360 | `90` | Linear only. |
| `background.static.gradient.cx` | -1 to 2 | `0.5` | Radial centre x, fraction of width. |
| `background.static.gradient.cy` | -1 to 2 | `0` | Radial centre y, fraction of height. |
| `background.static.gradient.radius` | 0.1 to 3 | `0.7` | Radial radius, fraction of width. |
| `background.scrim` | group |  | A veil between the background and the cards, to hold text contrast. Drawn over a shader, a picture and a still background, not over the plain colour and not with `"type": "none"`. |
| `background.scrim.color` | colour |  | #RRGGBB or #AARRGGBB (alpha first, as in v1). |
| `background.scrim.alpha` | 0 to 1 |  | Multiplied by the colour's own alpha. Without both values there is no scrim. |

### `motion`

Drawn by: the motion change.

| Key | Value | Default | Meaning |
|---|---|---|---|
| `motion.level` | `full`, `reduced`, `off` | `"full"` | The ceiling for theme-driven animation (borderAnimate, and what a later change adds). The effective level is the lower of this and the user/OS setting. Does not touch the shader background, which has its own control. |

## Bundled font

The built-in themes use Inter (variable), SIL Open Font License 1.1; its licence text is in
`src/UrDeck.Engine/Themes/Builtin/Inter-OFL.txt`.
