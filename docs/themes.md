# Themes

A theme is a folder holding one settings file and, optionally, font files. The theme decides how every card on a page
looks; widgets only draw content. Ten themes are built in (`default-dark`, `default-light`, `glass` (frosted translucent cards on a blue background), `carbon` (near-black, hairline cards with corner marks, mono numerals, a phosphor accent) and `paper` (warm off-white, soft shadows, one vermilion accent)). `carbon` and `paper` use Instrument Sans and JetBrains Mono and default to the `contour` background, over their own still colour where shaders are unavailable; select one with `"theme": "carbon"` or `"theme": "paper"`. `prism` (thick frosted glass: a specular top edge, a sheen, light wide numerals, full-circle rings) uses Bricolage Grotesque, and `obsidian` (warm black, champagne hairlines, Didone numerals, a diamond-marker gauge, circular dock buttons) uses Bodoni Moda for values and Jost for the rest; both default to the `nebula` background over their own still gradient where shaders are unavailable and are selected with `"theme": "prism"` or `"theme": "obsidian"`. `ember` (chamfered amber cards over a hatch, glowing condensed italic numerals, tach gauges with a red-line zone) uses Barlow Condensed Bold and defaults to the `embers` background over its own still gradient; `blueprint` (cyanotype ground with a drafting grid, dashed outlines, yellow corner marks, dimension-line dividers, a needle gauge) uses IBM Plex Mono and defaults to the `plotter` background over its own still colour; select them with `"theme": "ember"` or `"theme": "blueprint"`. `pop` (flat saturated tinted cards, thick black outlines, hard offset shadows, huge weight 900 numerals, a pill chip; light) uses Archivo and defaults to the `shapes` background over its own still cream; select it with `"theme": "pop"`. Your own go in a `themes`
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
  receives it). `plain` is not allowed here (a gauge that draws no shape is no
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
| `carbon` | `#090b0e`, `#10161b`, `#1f6f61`, `#4ff0c8` |
| `paper` | `#e9e7e0`, `#dcd8cc`, `#e6c9b8`, `#e5502b` |
| `prism` | `#10163a`, `#7b5cff`, `#19c6c0`, `#ff6aa8` |
| `obsidian` | `#0a0908`, `#2a2114`, `#6b5528`, `#d9b86c` |
| `ember` | `#0b0604`, `#5a1a07`, `#ff7a1a`, `#ffd9a0` |
| `blueprint` | `#0c2d52`, `#123c6b`, `#9fd0ff`, `#ffd34d` |
| `pop` | `#f6f0e0`, `#5b7bff`, `#ff8fc2`, `#b9f24a` |

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
- **A value is read, checked and kept; some are not drawn yet.** The tables say which change draws each group. Until then a value is accepted without a warning and has no effect on the screen. The gauge styles and values are drawn.
- **An invalid value is replaced by the default** (for an optional value, by "absent") and one warning in `urdeck.log` names the theme, the key and the value. A number outside its range, a colour that does not parse, a name that is not listed, a value of the wrong kind (text where a number goes) and a list with a bad item are all invalid; a bad item drops the whole list, except in `card.cutCorners` and `card.cornerMarks.corners`, where an unknown name is dropped and the others stay. Names are read in any case.
- **Some groups are never taken from `default-dark`:** `colors.ramp`, `colors.cardTints`, `colors.backgroundPalette`, `pattern`, `background`, `icons`, `meta`, every card chrome value, `typography.valueFill` and `valueGlow`, and every gauge value except `style`. The rest merge key by key, like the format 1 values.
- **Sizes** are fractions of one grid cell, **angles** are degrees (0 is left to right or 3 o'clock, clockwise), **times** are seconds (1 or more), **tracking** is in em, and **colours** are `#RRGGBB` or `#AARRGGBB`.

Rules between values, checked after each value on its own (each warns once and changes only what it names):

- `card.borderStyle`, `borderGradient` or `borderAnimate` with a border width of 0 has no effect: warned.
- `card.borderAnimate` with `mode` `spin` needs a `borderGradient` of kind `conic`; without one the animation is switched off.
- `card.borderAnimate` on a `dashed` `card.borderStyle` is not drawn (`THEME057`): the deck moves the border as a ring stretched round the card, which a dash pattern fitted to each side does not survive. The border stays still.
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

Drawn today: the shape (`shape`, `cut`, `cutCorners`), the fill (`fillGradient`, `tint`), the border (`borderStyle`,
`borderDash`, `borderGradient`), the light inside the card (`highlight`, `sheen`), the `accentStrip` and the part of the
`cornerMarks` on and outside the card, the `shadow` and the `glow`, the `compact` override for small cards, and the
`borderAnimate` (below).

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
- **Animated border.** `borderAnimate` moves the border. `spin` turns the conic `borderGradient` once round the card every
  `period` seconds (7 when absent); `pulse` breathes the border's opacity between full and 0.55 and back every `period`, for a solid,
  linear or conic border. Both move in 15 steps per second, not smoothly: the compositor draws them over the card, which is
  never repainted for them, at about 3 % of one CPU core for eight cards. The border moves only at the effective motion level
  `full` (see [`motion`](#motion)), while the display is on and the deck is shown; at `reduced` or `off`, with the display off or
  with the deck hidden it is the still border, drawn exactly as a border without `borderAnimate`. A snapshot (`--snapshot`,
  `urdeck-cli theme render`) always draws it still, at its starting angle and full opacity. It does not apply to a `dashed`
  border (`THEME057`), to a border width of 0, or to `spin` without a conic gradient (`THEME051`).
- **Hairlines.** In a theme that sets any card value of format 2, a border of 2 pixels or less is drawn at a whole number of
  pixels, never under 1; under one pixel it is written to `urdeck.log` once. A theme with no such value draws its cards
  exactly as before, unrounded.
- **Highlight.** `highlight.top` and `highlight.bottom` draw a line `thickness` wide just inside the top or bottom edge, round
  the corners with the edge, in their colour, fading to transparent over the first and the last eighth of the card's width. It
  is drawn after the widget's content, so content never covers it.
- **Sheen.** A radial light centred at `x` times the card's width and `y` times its height, reaching `size` times the card's
  width, from `color` to transparent; over the fill, under the content. Without a `color` there is no sheen.
- **Accent strip.** A line `thickness` wide along `side`, inside the card, `length` times the side long and centred on it, over
  the fill and under the content. Its colour is the entry of `colors` at the card's index, counting round the list (the same
  index as the tint); the theme's accent without `colors`. `fade` fades it from its colour at the start (the left, or the top on
  the left and right sides) to transparent at the end. `glow` lays a blurred copy under it, inside the card, in the glow's
  `color` (the strip's own without one) at the glow's `alpha`; a glow without `blur` draws nothing.
- **Corner marks.** `bracket` draws an L at each corner in `corners`, one arm along each edge; `tick` draws one arm, along the
  top or bottom edge. Each arm is `size` long from the mark's outer corner and `thickness` wide, in `color` (the accent without
  one). `inset` is the distance from the card's edge to the mark's outer edge: positive moves the mark in, negative puts it
  partly or wholly outside the card (minus half the thickness puts the line across the edge). Marks are drawn after the
  content, so content never covers them. The part outside the card is drawn under the cards with the shadow, and continues
  the part on the card without a break. `style` `none` (the default) draws no marks.
- **Shadow.** `shadow` is the card's shape moved by `x` and `y`, grown by `spread` on every side (shrunk when negative),
  blurred by `blur`, in `color`. `blur` 0, or no `blur`, is a hard edge (Pop); without a `color` it is black at 40 %. All four
  lengths are fractions of a cell.
- **Glow.** `glow` blurs the card's shape outwards by `blur`, in `color` (the card border colour without one) at `alpha`. A glow
  without `blur` draws nothing.
- **Under every card.** The shadow, the glow and the outside part of the marks are drawn under all the cards of the page, so
  they never cover a card's content, and each card's own shape is cut out of its own shadow and glow, so a translucent card
  does not show them through its fill. They take no input, and a pressed card's shadow shrinks and dims with the card. In the
  dock they are drawn at the dock's size and kept inside the dock's band.
- **Translucent cards.** When any card fill colour (`colors.cardFill`, a `fillGradient` colour, a `cardTints` entry in a tinted
  theme) has some transparency, a neighbour's shadow would show through the card. The shadow's offset and blur, and the glow's
  blur, are then scaled down together until they reach no further than `card.gap` past the card; `urdeck.log` says so once,
  with the reach before and after. A theme with opaque cards keeps its full reach.
- **Compact cards.** A dock slot and a page card of one grid cell (1x1) are compact; every other card is not. A compact card
  takes `card.compact` in place of the page card's values, each on its own: `radius`, `borderWidth` and `shape` (`round`,
  `chamfer` or `circle`) replace the page's, and a value `compact` does not set is the page card's. `compact.accentStrip` and
  `compact.cornerMarks` are merged key by key over `card.accentStrip` and `card.cornerMarks`: a key the compact group sets
  wins and the others come from the page's group (Blueprint's tick gives its style, corner, size and inset, and the
  thickness and colour come from the page's marks); with no page group the compact group stands alone, so a strip in
  `compact` alone is on tiles and not on larger cards. `circle` is a rounded card of half its smaller side as the radius: a
  circle on a square card, a capsule on one that is not square (one information line says so, and the card has room for one
  line of text). The fill, tint, border style, highlight, shadow, glow and marks of a compact card are drawn from its compact
  shape, and a dock slot's tint and strip colour count by its position. The padding is not overridden. A widget in the dock
  is given a theme with the compact corner radius and border width (a circle's radius is half the slot); a 1x1 widget on a
  page is given the page's theme, so only its card is compact.
- What is drawn outside the cards is made when the layout or the theme changes, never when a widget repaints.
- The highlight, accent strip and mark thicknesses are hairlines like the border: 2 pixels or less is drawn whole.
- A fill gradient, a sheen or an accent strip is drawn once per card size (and strip colour) and reused, so a repaint costs
  about what it does with a plain fill ([`docs/perf/card-chrome.md`](perf/card-chrome.md)).

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
| `card.cornerMarks.size` | 0.01 to 0.5 | `0.08` | Arm length. |
| `card.cornerMarks.thickness` | 0.002 to 0.1 | `0.01` | Line width. |
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

Drawn by: the `page-pattern` change. One texture over the whole window: above the background, its scrim and the theme's
background colour, under every card, the dock and the page indicator. It does not slide with the pages, takes no input and is
drawn whatever the configuration's `background` is (`"type": "none"` included), because it belongs to the theme.

- **Kinds.** `dots` (round dots of radius `thickness`), `grid` (horizontal and vertical lines; with `majorEvery`, every
  `majorEvery`-th line in each direction, starting with the first, takes `major`), `hatch` (parallel lines, `spacing` apart
  across the lines) and `scanlines` (horizontal lines). `spacing` and `thickness` are fractions of one grid cell, a quarter
  of the window's width. The first mark is half a repeat in from the top and left edges. `lines` and `scroll` are not
  available yet.
- **The angle** of a `hatch` is in degrees from horizontal, positive rising to the right: 45 (the default) runs from bottom
  left to top right, 0 or 180 is horizontal and 90 or -90 vertical. `angle` is used by `hatch` only, `majorEvery` and `major`
  by `grid` only; elsewhere they are ignored.
- **Whole pixels.** The distance between marks is the spacing in pixels rounded to a whole pixel (at least 2), a line is a
  whole number of pixels wide (at least 1) and a dot's radius is at least half a pixel. A hatch's angle is adjusted by under
  one degree so that its lines meet across every repeat; within one degree of an axis it is drawn as horizontal or vertical
  lines. On the 1100 pixel wide panel Carbon's dots are 35 pixels apart.
- **Colour.** `pattern.color`, alpha included; without one, the theme's muted text colour at a sixteenth of its opacity.
  `major.color` and `major.thickness` fall back to the pattern's own.
- **Cost.** The pattern is drawn once per theme and window size into one small block of whole repeats (at least 512 pixels
  along an axis, never more than the window) and laid out as tiles; nothing is repainted while the deck runs. A repeat wider
  or taller than 1024 pixels (a grid with a large `majorEvery` at a wide spacing) is still drawn correctly but costs more
  memory; `urdeck.log` has a `Pattern:` line with the block's size.

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

Drawn by: the `Divider` component of the SDK: Weather's rule between now and the forecast at 4x4. A theme with no `divider` gets the rule Weather always drew (the muted text colour at about a third of its opacity, at least one pixel thick).

| Key | Value | Default | Meaning |
|---|---|---|---|
| `divider.style` | `none`, `line`, `gradient`, `dimension` | `"line"` | dimension = a line with end ticks and an optional centred label (Blueprint). |
| `divider.color` | colour |  | #RRGGBB or #AARRGGBB (alpha first, as in v1). |
| `divider.thickness` | 0.001 to 0.05 | `0.01` |  |
| `divider.gradientTo` | colour |  | #RRGGBB or #AARRGGBB (alpha first, as in v1). |

### `well`

Drawn by: the `Well` component of the SDK: Weather puts one behind each forecast day at 4x4. With no `well` nothing is drawn.

| Key | Value | Default | Meaning |
|---|---|---|---|
| `well.fill` | colour |  | #RRGGBB or #AARRGGBB (alpha first, as in v1). |
| `well.radius` | 0 to 0.5 | `0.09` |  |
| `well.border` | colour |  | #RRGGBB or #AARRGGBB (alpha first, as in v1). |

### `chip`

Drawn by: the `Chip` component of the SDK: the status beside a gauge's label (Stats) and Weather's condition. A pill is drawn round the text and shrinks with it; with `colorBy` `status` a gauge's status text takes the colour of its level (a chip given no level keeps the theme's colours).

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

Drawn by: the gauge component, for the style each value applies to; a value that does not apply to the style is ignored without a warning.
A `meter` and a `thickbar` put the readout above the track, left-aligned; a `bar` does so with `layout` `stacked`. A `tach` has ticks
(28, every fourth longer) and a redline from 0.85 even when the theme sets none. A group the theme writes takes defaults for the keys
it leaves out: ticks 11 marks, every fifth longer, 0.04 long and 0.006 thick; redline from 0.85; marker a diamond 0.05 across.
Tick numbers run 0 to 100. `labelPosition` `below` and `above` apply to arcs. The track is cut into blocks like the fill when `segments` is set.

| Key | Value | Default | Meaning |
|---|---|---|---|
| `gauge.labelPosition` | `auto`, `inside`, `below`, `above` | `"auto"` | Where a gauge's label sits. auto keeps the v1 placement (Prism: below). |
| `gauge.layout` | `auto`, `stacked` | `"auto"` | `stacked` draws a bar with the readout above, left-aligned, and the track at the bottom (Carbon). A meter and a thick bar are stacked by definition. |
| `gauge.readoutGap` | number, 0 to 4 | `0.5` | The space between a bar's (or meter's, or thick bar's) track and its readout, in thicknesses of the track. Raise it to give the value more air above the bar (Carbon). |
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
| `gauge.crosshair` | true or false |  | Centre cross on dials. |
| `gauge.track` | group |  | Overrides the default accentDim track. |
| `gauge.track.color` | colour |  | #RRGGBB or #AARRGGBB (alpha first, as in v1). |
| `gauge.track.widthRatio` | 0.1 to 2 | `1` | Track width relative to stroke.thickness. |
| `gauge.colorBy` | `accent`, `ramp` | `"accent"` | 'ramp' colours the fill by value along colors.ramp instead of the accent/warning/critical steps. |
| `gauge.valueTint` | true or false | `false` | Colours the readout with the same colour as the fill. |
| `gauge.sparkline` | group |  | A history line under the readout, in the colour of the fill. Stats keeps the last `historySeconds` of each gauge (one value a second, only while this is enabled) and draws the line when the card is at least 0.9 of a cell tall. |
| `gauge.sparkline.enabled` | true or false |  |  |
| `gauge.sparkline.height` | 0.05 to 1 | `0.3` |  |
| `gauge.sparkline.historySeconds` | 10 to 600 | `60` |  |
| `gauge.sparkline.strokeWidth` | 0.002 to 0.1 |  |  |
| `gauge.status` | group |  | A status chip beside the label, on the label's row. Stats chooses the label from the fraction and the thresholds, or from the level a slot's own `warning` and `critical` give. |
| `gauge.status.enabled` | true or false |  |  |
| `gauge.status.thresholds` | list of 2 numbers |  | [warm, hot] as 0..1. Default [0.7,0.9]. |
| `gauge.status.labels` | list of 3 text |  | [ok, warm, hot]. |

### `icons`

Drawn by: the weather card (its condition icons) and the shortcut card (`theme:` glyphs and the fallback glyph for an icon that cannot be found).
A theme that names a set gets static outline icons, drawn in the theme's stroke and tint, in place of the animated colour icons.
There is no `animated` key: icons from a set are pictures.

| Key | Value | Default | Meaning |
|---|---|---|---|
| `icons.set` | text |  | Name of an icon set: a folder icons/<set>/ inside the theme folder (with a manifest.json), or a built-in set (outline). A key the set lacks is answered by the built-in 'outline' set. See Icon sets in the theme reference. |
| `icons.stroke` | 0.005 to 0.2 |  | Override stroke width for set icons, fraction of the icon size. |
| `icons.tint` | text |  | 'accent', 'accent2', 'text', 'ramp' (the colour of colors.ramp for the value shown, such as the temperature), 'none' (the text colour) or a #colour. |
| `icons.glow` | group |  | Glow behind set icons (Rigline). Colour defaults to the icon's tint. |
| `icons.glow.blur` | 0 to 1 |  | Blur radius, fraction of a cell. |
| `icons.glow.color` | colour |  | #RRGGBB or #AARRGGBB (alpha first, as in v1). |
| `icons.glow.alpha` | 0 to 1 | `0.6` | Strength. |

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

Drawn today: it caps `card.borderAnimate`.

| Key | Value | Default | Meaning |
|---|---|---|---|
| `motion.level` | `full`, `reduced`, `off` | `"full"` | The ceiling for theme-driven animation (borderAnimate, and what a later change adds). The effective level is the lower of this and the user's: the configuration's `motion`, or Windows' "Animation effects" setting when the configuration has none. Does not touch the shader background: the user's level does (a moving shader is drawn still below `full`), the theme's ceiling does not. |

At `full` the theme's continuous animations run; at `reduced` and `off` none does, and each is drawn as it is when still (the
two levels behave alike in this version). The page slide and the press feedback are not governed by the level. `urdeck.log`
says `Motion: <level> (<what decided it>)` at startup and whenever the level changes.

## Icon sets

An icon set is a folder of small SVG outline icons and a `manifest.json` that says which file shows which of the product's icon names.
`icons.set` names one: a folder `icons/<set>/` inside the theme's folder, or a built-in set. The deck looks a key up in the theme's set
first and then in the built-in `outline` set; a widget with an icon of its own for the case with no set (the weather card's animated
icons) uses it only when the theme names no set.

```json
{
  "version": 1,
  "name": "mine",
  "source": "https://example.com/where-the-icons-come-from",
  "license": "ISC",
  "upstreamVersion": "1.0.0",
  "keys": {
    "weather/rain": "cloud-rain.svg",
    "weather/thunderstorms-day": "bolt-cloud.svg",
    "weather/thunderstorms-night": "bolt-cloud.svg",
    "app/home": "house.svg"
  }
}
```

- The manifest is required and is the only way a file is found; no file is picked up by its name. Several keys may name one file, and
  a file may keep its upstream name. A folder without a manifest is not a set; the log names the file to make.
- A path in `keys` is relative to the set's folder, ends in `.svg` and stays inside it (a `..` segment, an absolute path or a
  backslash that leaves the folder gives that key no icon).
- Keys: `weather/` `clear-day`, `clear-night`, `partly-cloudy-day`, `partly-cloudy-night`, `overcast`, `fog`, `drizzle`, `rain`, `sleet`,
  `snow`, `thunderstorms-day`, `thunderstorms-night`, `not-available`; `app/` `terminal`, `folder`, `file`, `music`, `gamepad`,
  `browser`, `mail`, `chat`, `settings`, `code`, `camera`, `image`, `video`, `calendar`, `clock`, `home`, `power`, `lock`, `link`,
  `cloud`, `download`, `star`, `heart`, `bolt`, `chart`. A set may have fewer; the lists only grow.
- An icon file is at most 4 KB, with a root `svg` whose `viewBox` is `0 0 24 24`, made of `path`, `circle`, `ellipse`, `line`, `polyline`,
  `polygon` and `rect` (with `rx`/`ry`) inside `svg` and `g`. No `style`, `script`, `image`, `use`, `text`, `defs`, `transform` or links.
  The art is outlines: the deck strokes every shape with the theme's `icons.stroke` and its stroke cap, in the theme's `icons.tint`
  (or `glow`), and also fills a shape marked `fill="currentColor"`. Colours and widths in the file are ignored.
- The built-in `outline` set is [Lucide](https://lucide.dev) (ISC; MIT for the icons derived from Feather), copied once; its
  manifest names the release. The licence text is in the set; the running application draws no credit.
- The deck reads and parses a set once when it resolves the theme; drawing an icon reads nothing.

`urdeck-cli theme validate` checks a theme's sets with the findings THEME070 to THEME076 below.

## Checking a theme

`urdeck-cli` checks a theme folder, draws it and starts one, without a deck: no window, no GPU, no network, on Windows and on
Linux. It reads a theme by the same code the deck does, so the deck's log and the tool's output name the same findings with the
same words; the tool adds the checks that need a judgement (keys the deck does not read, font files, backgrounds that do not
exist, text contrast, metadata). It never reads or writes the deck's configuration or log. Build and run it from the
repository as [the background tool](backgrounds.md#the-command-line-tool) is, or install the package.

```
urdeck-cli theme validate <folder> [--background-dir <directory>]... [--format text|markdown|json|sarif] [--fail-on-warnings]
urdeck-cli theme validate --all <directory> [...]
urdeck-cli theme validate --builtin [...]
urdeck-cli theme render <folder> | --builtin <name> -o <file.png> [--page page|dock] [--size WIDTHxHEIGHT] [--fail-on-warnings]
urdeck-cli theme init <name> [--output <directory>] [--from <built-in theme>]
urdeck-cli theme schema [--output <file>]
```

- **`validate`** prints every finding of a folder, of every folder directly inside a directory (`--all`, in the order of their
  names; not recursive; no folders at all is a failure of the tool), or of the built-in themes as the product ships them
  (`--builtin`). The folder's name is the theme's name, and a folder named like a built-in is checked as the folder it is.
  `--background-dir` adds a directory of background folders that `background.default.name` and `meta.pairsWith` may name; the
  built-in backgrounds are always known. The formats are those of `shader validate`; a JSON diagnostic of a value also has `key`.
- **`render`** draws the sample page (a clock, two stats cards, a weather card and four shortcuts, with fixed readings and time)
  in the theme, at 1100x1920 unless `--size` says otherwise, with the page renderer the deck uses. `--page dock` adds the dock and
  the page indicator. The same input gives the same picture on one machine; pictures from different machines can differ in the
  edge pixels of text. The findings go to the error stream and do not stop the picture; a theme that cannot be read at all gets no
  picture.
- **`init`** makes `<name>` (letters, digits, `-` and `_`) with a `theme.json` holding the `$schema` line, `format` 2, a `meta` for
  you to fill in and the default dark theme's colours; with `--from` it makes a self-contained copy of a built-in theme with the
  fonts and licence files it names. It never overwrites a folder.
- **`schema`** prints the JSON Schema of the format, the file published as `docs/schemas/v2/theme.json`.

Exit codes are those of the other commands: `0` nothing that fails the run, `1` an error (or a warning with
`--fail-on-warnings`; an information finding never fails), `2` the tool could not do its job (bad arguments, a folder that does
not exist). The built-in themes check clean, and CI runs `theme validate --builtin --fail-on-warnings`.

### Findings

Each finding says what is wrong and how to fix it, names the key by its path (`card.shadow.blur`) and the file. The codes never
change meaning, and a retired code is not reused. **error** means the deck cannot use the theme or the file; **warning** means it
works and could be better; **info** is a remark. The deck logs the findings it can act on at load (the codes marked "also logged");
the rest only the tool reports.

| Code | Severity | Meaning and how to fix it |
|---|---|---|
| THEME001 | error | `theme.json` is missing, is not valid JSON or holds no object: add it, or fix the syntax at the position given. Nothing else is checked |
| THEME002 | warning | a key the deck does not read (a typo, or a key of a later format): fix the name, the message suggests the nearest key |
| THEME003 | warning, error | a value of the wrong JSON type (also logged). An error for a value of format 1 (`card.gap`, `colors`...), where the deck skips the whole theme |
| THEME004 | warning | a number outside its range, which the message gives (also logged): the deck uses the default |
| THEME005 | warning | a value that is not one of the names allowed, which the message lists (also logged; the case does not matter) |
| THEME006 | warning | a colour that is not `#RRGGBB` or `#AARRGGBB` (also logged) |
| THEME007 | warning | a list outside its length limits or with an invalid element (also logged): the deck drops the list whole |
| THEME008 | warning | a group that needs a member it does not have, such as a gradient without `colors` (also logged): the group is ignored |
| THEME010 | warning | `format` is newer than the deck reads (also logged): the deck ignores what it does not know |
| THEME020 | error | a font file is missing from the theme, is not a `.ttf`, `.otf` or `.ttc`, or its path leaves the theme's folder (also logged when it leaves the folder or cannot be loaded): add the file, fix the name, keep it inside the folder |
| THEME021 | warning | a font file is over 3 MB (subset it) or has no licence file beside it (`*-OFL.txt`, `LICENSE*` or `*-LICENSE*`): add the licence so the theme can be shared |
| THEME022 | warning | a font named by family, which is used only where it is installed: put a `.ttf` or `.otf` in the folder and name the file |
| THEME023 | warning | a weight outside a variable font's range, or one a static font does not have, for a role (also logged): choose a weight the file has |
| THEME040 | warning | `background.default.name` is not a background that exists: use a built-in one, or point `--background-dir` at yours |
| THEME041 | warning | `meta.pairsWith.backgrounds` names a background that does not exist: fix the name or remove it |
| THEME042 | warning | the default background is not in the theme's own `pairsWith` list: add it |
| THEME043 | warning | the still background picture is missing, leaves the folder or is over 8 MB |
| THEME044 | warning | `icons.set` names an icon set that exists neither in the theme's `icons/` folder nor among the built-in sets (also logged): fix the name |
| THEME050 | warning | a border style, gradient or animation with `card.borderWidth` 0 (also logged): give the border a width |
| THEME051 | warning | `card.borderAnimate` `spin` without a conic `card.borderGradient` (also logged): the deck switches the animation off |
| THEME052 | warning | a pattern whose marks are closer than three pixels at the panel width (also logged): raise `spacing` |
| THEME053 | warning | outside corner marks reaching past `card.gap`, thickness included (also logged): move them in or widen the gap |
| THEME054 | warning | a shadow or glow whose reach is more than the gap over a translucent fill (logged as information, the deck limits it): reduce the blur or offset |
| THEME055 | warning | a line (border, highlight, accent strip, corner mark) that resolves under one pixel, which the deck draws 1 px wide (also logged) |
| THEME056 | info | `card.compact.shape` `circle` on a card that is not square, which is a capsule (also logged) |
| THEME057 | warning | `card.borderAnimate` on a `dashed` `card.borderStyle` (also logged): the border is drawn still, because the moving ring is stretched round the card and a dash pattern fitted to each side does not survive that |
| THEME060 | warning | the text colour on the card fill, or on its gradient's darkest or lightest stop, is under 4.5:1: change the colours; a translucent fill is composited over `colors.background`, and the message says so because the page behind may be a moving background |
| THEME061 | warning | the text colour on a card tint is under 4.5:1 (a tinted card keeps `colors.text`): change the tint at the position the message names |
| THEME062 | info | the muted text colour on the card fill is under 3:1 |
| THEME070 | error | an icon set's `manifest.json` is missing, is not valid JSON, has a `version` other than 1 or has no `keys` (also logged): add or fix it |
| THEME071 | error | a manifest entry names a file that is missing, does not end in `.svg` or leaves the set's folder (also logged) |
| THEME072 | error | an icon file breaks the file rules: over 4 KB, no `viewBox` of `0 0 24 24`, an element or attribute that is not allowed, a `transform`, not valid XML (also logged) |
| THEME073 | warning | a manifest key is not one of the product's icon keys, so no widget asks for it |
| THEME074 | warning | the manifest's `license` or `source` is empty, or the set's folder has no licence file |
| THEME075 | warning | an `.svg` file in the set's folder that no manifest entry names (the tool alone reports it) |
| THEME076 | info | the set has no icon for some of the product's keys; the message names them and says `outline` answers them |
| THEME080 | warning | `meta` lacks a `name`, a `description` or a `mode`: add them so an editor can list the theme |
| THEME090 | info | more than one continuous animation is on (also logged): each costs CPU while it runs |

## Bundled font

`default-dark`, `default-light` and `glass` use Inter (variable); `carbon` and `paper` use Instrument Sans and JetBrains Mono, `prism` uses Bricolage Grotesque, `obsidian` uses Bodoni Moda and Jost (all variable), `ember` uses Barlow Condensed Bold and `blueprint` IBM Plex Mono Regular (static files) and `pop` Archivo (variable). All are under the SIL Open Font License 1.1; the licence texts are in
`src/UrDeck.Engine/Themes/Builtin/` (`Inter-OFL.txt`, `InstrumentSans-OFL.txt`, `JetBrainsMono-OFL.txt`, `BricolageGrotesque-OFL.txt`, `BodoniModa-OFL.txt`, `Jost-OFL.txt`, `BarlowCondensed-OFL.txt`, `IBMPlexMono-OFL.txt`, `Archivo-OFL.txt`) and `THIRD-PARTY-NOTICES.md` lists them.
