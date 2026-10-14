# 02 Theme schema, format 2

This is the authoritative spec for `theme.json`. The machine-readable twin is `assets/schema/theme.schema.json`
(JSON Schema 2020-12). If the two disagree, fix the schema; if the schema and the engine disagree, the engine has a bug.
Appendix A lists every key (generated from the schema, so it cannot drift).

## 1. Compatibility and versioning

- **Additive.** Every v1 key keeps its name, range and meaning. Nothing is removed or renamed.
- `"format": 2` is optional. Absent = 1. A host that supports up to N logs *one* warning when it loads a theme with
  `format > N` ("theme X declares format 3; this host supports 2; unknown keys are ignored") and carries on.
- **Old host, new theme:** unknown keys are ignored silently, so a format-2 theme shows its colours, radius, fonts and
  gauge style (`meter/tach/marker/thickbar` unknown to a v1 host are rejected by v1's `Sanitize` and replaced by `ring`
  with a warning, which is acceptable). Draft themes are written so this degradation looks reasonable: v1 keys alone
  give a coherent (plainer) theme.
- **New host, old theme:** identical to today. **Test:** snapshot of default-dark/default-light/glass must be
  byte-identical (or within a documented epsilon) before and after PR 1-4 in `09`.
- Format 2 is not a fork. There is one `ThemeDefinition` class; new members are nullable like the old ones.

## 2. Units and conventions (apply to every key)

| Kind | Rule |
|---|---|
| Colour | `#RRGGBB` or `#AARRGGBB`, alpha **first** (v1 rule). Regex `^#([0-9a-fA-F]{6}|[0-9a-fA-F]{8})$`. |
| Size | Fraction of one grid cell, unless stated. Resolved to pixels by `ThemeResolver` per surface (so a 1100 px and a 2200 px panel look the same). |
| Ratio | Unitless multiplier (`valueScale`, `widthRatio`, `unitRatio`). |
| Angle | Degrees. 0 = +x (3 o'clock), clockwise positive, as SkiaSharp. Gradient `angle`: 0 = left→right, 90 = top→bottom. |
| Time | Seconds. Animation periods must be ≥ 1 s. |
| Tracking | em (fraction of the font size). |
| Enum | Case-insensitive on read (`ThemeGaugeDefinition.Parse` does this today), canonical lowercase on write. |
| Arrays | Length limits are validated; an out-of-range array is dropped (whole key) with one warning. |
| Colour arrays | Every element must parse or the whole array is dropped (matches `backgroundPalette` behaviour). |

## 3. Merge and validation rules

- **Merge:** per member, `value ?? baseline`. Nested objects merge recursively (`card.shadow.blur` inherits
  independently of `card.shadow.color`). **Exceptions, deliberately not inherited from default-dark:**
  - `colors.backgroundPalette` (v1 rule, see `01`).
  - `colors.ramp`, `colors.cardTints` (a theme that does not define them must not get default-dark's).
  - `pattern`, `card.shadow`, `card.glow`, `card.highlight`, `card.sheen`, `card.cornerMarks`, `card.borderGradient`,
    `card.fillGradient`, `typography.valueFill`, `typography.valueGlow`, `gauge.sparkline`, `gauge.status`,
    `gauge.redline`, `gauge.ticks`, `gauge.segments`, `gauge.marker`, `icons.*`, `background.*`, `meta.*`,
    `card.compact`: default-dark has none of these, so "inherit" and "absent" coincide; the rule is stated so nobody
    adds them to default-dark and thereby changes every theme.
  - Keys whose absence means "feature off" **must stay absent in the merged result** (no silent default objects).
- **Sanitize:** each invalid value is cleared (so the merge takes the default) and `UrDeckLog.Warn($"Theme '{name}': {path} {value} is outside {min}..{max}; using the default.")`
  exactly as v1 does. Add a `ValueCheck.Enum`, `ValueCheck.Colors(path, list, min, max)`, `ValueCheck.Angle` helper rather
  than open-coding. **Cross-field rules** are validated after per-field ones (see each section).
- **Unknown keys:** a new `ThemeLint` pass (see `08`) reports unknown keys with a "did you mean" at *validate* time
  (CLI) and at debug log level at load time. Never an error.
- **Reserved keys** (`card.blur`) are accepted, validated for range, logged once at info ("not supported yet; ignored")
  and never drawn.

## 4. Key groups

Each subsection says what the knob does, how it interacts, the failure cases and which draft themes use it. Defaults
are the value used when the key is absent; for "off by default" features the default is *absent*.

### 4.1 `meta` (never affects rendering)

`name, description, author, license, version, mode ("dark"|"light"), tags[], pairsWith.backgrounds[], samples[]`.
Why: the config editor needs a display name, a light/dark grouping and recommendations. `samples` mirrors the background
manifest's `samples` (image file inside the theme folder, caption). `mode` is also used by the editor to pick a default
palette hint; it does **not** change colours. Not merged from default-dark (a theme without `meta.name` shows its folder
name). Validation: strings only; `pairsWith.backgrounds` entries are not checked at load (a missing background is simply
skipped by the editor; `theme validate` warns).

### 4.2 `colors`

New: `accent2`, `ramp[2..5]`, `cardTints[1..8]`, `ink`.

- `accent2`: second accent for gradients, markers, redlines, tab highlights. Default: `accent`. (Rigline magenta, Ember red,
  Blueprint pale blue, Prism ice, Obsidian pale champagne.)
- `ramp`: low→high load colours. Default derived: `[good, warning, critical]` when `gauge.colorBy == "ramp"` and the key is absent.
  `colorBy: "ramp"` samples the ramp **continuously** by value (linear interpolation in sRGB; document that, it is
  good enough and avoids a Lab dependency). `accent` mode keeps v1 behaviour (accent below warning, warning, critical).
- `cardTints`: with `card.tint == "cycle"`, card *i* of a page (page order, then row-major, skipping dock slots) uses
  `cardTints[i % n]` as its fill instead of `cardFill`. Compact cards and dock slots use the index of their slot. Text colour is `colors.ink` if set, else the more
  contrasting of `text`/`background` against the fill (same rule v1 uses for the vertical-bar readout).
- `ink`: text on tinted/accent fills.
- Draft values are in `assets/themes`. **Contrast rule:** the editor/validator warns when `text` on `cardFill` is under
  4.5:1 (7:1 not required). Transparent fills are composited over `background` for the check.

### 4.3 `card` (chrome)

Drawing order **inside the card's surface** (new `CardChrome` in the engine; see `03`):

```
1  shadow            (outside, bleed; behind everything)
2  glow              (outside, bleed)
3  fill              (cardFill | fillGradient | tint)  clipped to the card shape
4  sheen             clipped
4b accentStrip       clipped, a lit line along one edge
5  [widget content]  clipped, drawn by the widget AFTER the chrome
6  highlight         inside rim, clipped; drawn after the content so nothing covers it
7  border            solid|dashed|dotted|gradient|conic, drawn inside the edge as v1
8  cornerMarks       (may bleed outside)
```

Note on 6: v1 draws the border *before* the widget (`DrawCard` then `Render`). Keep that for 1-4 and 7-8: **chrome is
drawn in two calls, `DrawBack` (1-4, 7) before the widget and `DrawFront` (6, 8) after**, so content can never cover a
corner mark or a rim light. Border (7) stays in `DrawBack` to preserve v1 pixels.

Keys:

| Key | Semantics | Validation / cross-field |
|---|---|---|
| `shape` `round`\|`chamfer` | `chamfer` replaces the rounded rect with an `SKPath` polygon whose cut corners are those in `cutCorners` | With `chamfer`, `radius` is ignored: uncut corners are square, as in the mock |
| `cut` (0..0.5, default .14) | Chamfer size; capped at half the smaller side | |
| `cutCorners` | subset of `tl,tr,br,bl`; default `[tr, bl]`; duplicates removed; empty list = square card | unknown names dropped with a warning |
| `fillGradient{colors[2..6],angle}` | replaces `cardFill` (and `tint`) | |
| `borderStyle` `solid\|dashed\|dotted\|none` | | `dotted` = round-capped zero-length dashes spaced `borderDash.off+borderWidth` |
| `borderDash{on,off}` | dash lengths in cells; default `on=3*borderWidth, off=2*borderWidth` | ignored for `solid` |
| `borderGradient{colors[2..8],kind,angle}` | replaces `cardBorder` colour; `linear` uses `angle`; `conic` sweeps 360° around the card centre | `borderWidth` must be > 0 for it to show (warn if 0) |
| `borderAnimate{mode,period}` | `spin` rotates a conic start angle once per `period`; `pulse` modulates alpha 0.55..1 sinusoidally; only valid with a gradient border | `motion.level` caps it (see 4.13) |
| `cornerMarks{style,corners,size,thickness,inset,color}` | `bracket`: L-shaped, `tick`: short diagonal-free tick, `cross`: small plus; `inset` negative = outside the card; default `corners [tl,br]`, `color` = accent | outside marks need bleed ≥ `|inset|+thickness` |
| `shadow{x,y,blur,spread,color}` | `blur 0` = hard offset (Pop); else gaussian-ish via `SKMaskFilter.CreateBlur` | bleed = `max(|x|,|y|)+blur*1.5+spread` |
| `glow{blur,color,alpha}` | outer glow, colour defaults to the **border** colour (or accent) | bleed = `blur*1.5` |
| `highlight{top,bottom,thickness}` | 1-pixel-class lines just inside the top / bottom edge, following the corner radius (an arc segment), fading to transparent over ~12 % of the width at each end | |
| `sheen{color,x,y,size}` | one radial gradient (center at x,y of the card), alpha→0 at `size*width` | |
| `accentStrip{side,thickness,length,colors,fade,glow}` | A lit line along one edge, inside the clip, `length` of the side centred. `fade` fades it to transparent (Carbon's scan line). `colors` cycle by card index (or slot index on compact cards, Rigline). `card.compact.accentStrip` overrides it for compact cards | |
| `tint` `none\|cycle` | see `colors.cardTints` | |
| `blur` | **reserved**; ignored (decision D1) | |
| `compact{…}` | partial override: `radius, borderWidth, padding, shape (round\|chamfer\|circle), accentStrip{…}, cornerMarks{…}` | applies when the card is ≤ 1×1 cell **or** a dock slot; `circle` requires a square card (otherwise falls back to `round` radius = min/2 with an info log) |

**Why `compact` exists:** Obsidian wants circular dock buttons, Rigline wants a lit strip under 1×1 tiles, Pop wants
tighter radii on tiles. Doing it as a partial of the same object (instead of `card1x1`, `dock` variants, etc.) keeps one
mental model: "the same keys, for small cards".

**Card geometry invariants.** The content rectangle (`ContentRect`) must still be computed from `padding` only; chrome
never shrinks it. `Gap` between cards is unchanged by chrome (bleed may overlap neighbours' margins, drawn under them
by z-order; shadows therefore never cover a neighbour's *content*).

### 4.4 `pattern` (page level)

A texture drawn once per page over the background and under all cards, at the page's resolution, **not** inside each
card (so a Carbon dot grid is continuous across card gaps). Kinds: `dots` (radius = thickness), `grid` (horizontal +
vertical lines, `majorEvery` heavier lines using `pattern.major`), `lines` (parallel lines at `angle`), `hatch` (one set of thin, dense parallel lines at `angle`; `lines` is the same routine with a thicker default, so implement it once), `scanlines` (horizontal 1-pixel lines;
`scroll.dy` moves it). `scroll` is cells/second. Cost: patterns are rendered into a **tile bitmap** once and drawn as a
shader (`SKShader.CreateBitmap`, repeat) with a matrix for scroll, so per-frame cost is one fill. Scrolling forces the
page layer to tick at the host clock (counts against `motion`). The pattern layer lives in the *background layer's*
compositor stack (see `03` for where), not in each widget surface.

### 4.5 `divider`, `chip`, `well`

Rules and labels that widgets draw. Widgets call `Components.Divider.Draw(canvas, theme, rect)` /
`Components.Chip.Draw(...)` (new SDK components, `04`). `divider.style`: `line`, `gradient` (color → `gradientTo`, default
transparent), `dimension` (a line with end ticks and an optional label centred: Blueprint). `well{fill,radius,border}`: a recessed rounded rectangle widgets draw behind grouped content (Weather's forecast days in Rigline); absent means none. `chip.shape`: `none` (plain
text, v1 look), `pill`, `rect`, `bracket`; `colorBy: status` tints from good/warning/critical.

### 4.6 `typography`

New: `fonts{value,unit,label,body,title}`, `valueScale`, `case{label,title,unit}`, `tracking{value,label,title,body}`,
`slant`, `valueFill`, `valueGlow`.

- `fonts`: each is a family name or a file in the theme folder (v1 rule: path escapes rejected). A role without an
  entry uses `typography.font`. `ThemeFont` becomes a map role→spec (see `03`). Fallback chain per role: spec → `font` →
  bundled Inter → `SKTypeface.Default`, with one warning per missing file.
- `valueScale` multiplies the **fitted** readout size (the Readout already fits text to its rect; scale applies to the
  *target* cap height, then the fit still shrinks it if it overflows).
- `case`: applied by `TextLine`/`Readout` at draw time via `ToUpperInvariant()`/`ToLowerInvariant()` (not culture) so
  it is stable.
- `tracking` (em): implemented by drawing glyph runs with per-glyph advances (`SKFont.GetGlyphWidths` + manual
  positioning) when ≠ 0. **Constraint:** Readout's equal-width digits must stay equal; apply tracking *after*
  digit-width equalisation. Cost: a text-shaping detour only when tracking ≠ 0; cache the shaped result per string/size
  in the component.
- `slant` (deg 0..20): `canvas.Skew(-tan(slant), 0)` about the text baseline, value role only. Prefer a real italic file
  when the family has one (Chakra Petch does; Barlow Condensed does), but a single file per role keeps themes simple, and
  `ThemeFont` roles can point at an italic file instead of using `slant`. Keep `slant` because variable fonts without
  an italic axis (Bricolage) and user themes need it.
- `valueFill{colors,angle,animate.period}`: replaces the value's paint with `SKShader.CreateLinearGradient` spanning
  the text bounds, with the gradient translated by `phase = time/period` (wrapping, tile mode `Repeat` with the first
  colour repeated last by the *theme author*, as in the draft Rigline). Animation needs the widget to be `IsAnimating`
  (see 03). Not applied when the reading is stale/unavailable (uses `TextMuted`).
- `valueGlow{blur,color,alpha}`: draw the value twice, first with `SKMaskFilter.CreateBlur(Normal, sigma)` in the glow
  colour. Cost note in `03`.

### 4.7 `stroke`: unchanged

### 4.8 `gauge`

`style` gains `meter`, `tach`, `marker`, `thickbar` (v1: `ring`, `bar`, `verticalBar`; `plain` still rejected for the
*theme* value). A **widget's explicit `Style` still wins** (v1 rule) and a widget may also name the new styles.
`GaugeStyle` enum gains `Meter, Tach, Marker, ThickBar`; `Plain` semantics unchanged. All **modifiers** below apply to
every style where they make sense and are ignored (no warning) where they do not:

| Style | Geometry | Honours |
|---|---|---|
| `ring` (v1) | arc, default start 135 span 270 | sweep, segments, gradient, glow, track, colorBy, valueTint, ticks (small), marker |
| `bar` (v1) | horizontal rounded bar | segments, gradient, glow, track, scale, colorBy, valueTint |
| `verticalBar` (v1) | | same as bar |
| `meter` | thin bar with tick row and numeric `scale.labels` beneath (Paper) | ticks, scale, track, colorBy |
| `tach` | arc with tick ring, optional numerals, redline wedge, needle (Ember) | sweep, ticks, redline, marker (default needle), gradient, glow |
| `marker` | hairline arc/bar plus a jewel/needle at the value (Obsidian, Blueprint) | sweep, marker, ticks, crosshair, track |
| `thickbar` | heavy bar with outline (Pop) | outline, track, colorBy |

Modifier semantics:

- `sweep{start,span}`: ring/tach/marker geometry. v1 constants 135/270 become the defaults (so v1 pixels do not change).
  Prism uses `{-90, 360}` (full ring starting at 12 o'clock). Validation: `span` 30..360; when `span == 360` no track gap.
- `segments{length,gap}`: the fill is chopped into blocks (bars: block length in cells; rings: **arc length** in
  cells at the ring's mid radius). Partial last block drawn partially (Carbon shows a partial block, matches the mock).
- `outline{width,color}`: outline round the track (thickbar).
- `ticks{count,majorEvery,length,thickness,color,labels}`: tick marks. For arcs they are radial lines outside the track;
  for meters, under the bar. `labels: true` prints the numbers (0..100 or the slot's Min..Max) at major ticks using the
  `label` role.
- `scale{labels[],size}`: explicit labels at even spacing under a bar.
- `redline{from,color,glow,lightsAbove}`: the zone from `from` to 1 is drawn on the track in `color` at low alpha; with `lightsAbove` it renders at full strength only while the fraction is ≥ `from` (Ember).
- `gradient`: `"ramp"` (uses `colors.ramp`) or explicit `{colors,angle}`. For an arc the gradient runs **along the sweep**
  (sweep shader), not by angle; for bars along the length. `angle` is ignored for arcs; document it.
- `glow{blur,color,alpha}`: a blurred copy of the fill under the fill. Colour defaults to the fill's current colour.
- `marker{shape,size,color,halo}`: `dot`, `diamond`, `needle`, `line`; `halo` draws a dark ring.
- `crosshair`: small centre cross on dials (Blueprint).
- `track{color,widthRatio}`: replaces the `accentDim`-alpha default; `widthRatio` scales track width relative to stroke thickness.
- `labelPosition` `auto|inside|below|above`: where the gauge's label sits. `auto` is today's placement; Prism sets `below` so the ring's centre holds only the number.
- `colorBy` `accent|ramp` and `valueTint`: see 4.2. `valueTint` colours the readout with the same colour as the fill
  (**except** where v1 picks the readout colour for contrast over a vertical bar fill; contrast wins).
- `sparkline{enabled,height,historySeconds,strokeWidth,fillAlpha}`: Stats-level feature (needs history; `04`). Drawn
  under the readout, same colour as the fill. Height is in cells.
- `status{enabled,thresholds[2],labels[3]}`: draws a `chip` beside the label: `labels[0]` while fraction < t0, `labels[1]`
  while < t1, else `labels[2]`; colours good/warning/critical. If the slot has its own `Warning`/`Critical` those win over
  `thresholds` (expressed as fractions of Min..Max).

### 4.9 `indicator`, `press`, `dock`: v1 unchanged

One addition by convention only: draft themes set `indicator.active/inactive/backdrop` to match their palette.

### 4.10 `icons`

`set` (folder under the theme folder `icons/<set>/` **or** a built-in set name), `stroke`, `tint`, `animated`. See `04`
for the resolution order, canonical keys, tinting rules and Lottie interplay.

### 4.11 `background`

- `background.default{name,parameters,colors}`: what the host selects when the user config does not set `background`.
- `background.static{image|gradient|color,fit,tileSize}`: still background used (a) when no animated background is selected **and**
  `default` is absent, (b) when motion is `off` (user/OS setting), (c) as the fallback while a shader compiles or when it fails.
- `background.scrim{color,alpha}`: a veil between background and cards (for text contrast over busy shaders).
- **Precedence (single source of truth, restated in `07`):**
  1. user `urdeck-config.json` `background` (including `{"type":"none"}`),
  2. theme `background.default`,
  3. theme `background.static`,
  4. `colors.background` solid.
  A user choice of a specific background never loses its `colors`/params to the theme; a theme default's `parameters`
  and `colors` are applied only when the *theme default* is the one selected.
- `static.gradient` is `linear` (angle) or `radial` (`cx`, `cy`, `radius`: Obsidian's warm glow at the top of the page).
- Images: file inside the theme folder (path-escape rejected), PNG/JPEG/WebP, ≤ 8 MB (log + ignore if larger),
  decoded once at the background layer's scale, `cover` fit by default.

### 4.12 `motion`

`motion.level`: `full | reduced | off`. The **ceiling for theme-driven animation only** (`borderAnimate`,
`valueFill.animate`, `pattern.scroll`, clock separator blink, `redline` pulsing). The effective level is
`min(theme.motion.level, user setting, OS "reduce motion")`. `reduced` keeps single-shot transitions (value eases) but stops
loops; `off` also stops eases. **Does not control the shader background** (it has speed/enable controls already, and a
`static` background is what the host shows if the user turns the background off). New *user* setting `reduceMotion`
(`auto|on|off`, default `auto` = follow Windows `UISettings.AnimationsEnabled`) lives in `urdeck-config.json` (add in PR 9; see `09`).

## 5. Cross-field and cost rules (enforced in `Sanitize` after per-field checks)

1. `borderGradient` or `borderStyle != none` with `borderWidth == 0` -> warn "border style has no effect with borderWidth 0".
2. `borderAnimate.mode == spin` with `borderGradient.kind != conic` -> downgrade to `none` with a warning.
3. `card.shape == chamfer` and `compact.shape == circle` -> circle wins for compact cards only.
4. `gauge.redline` / `ticks` on `bar`/`verticalBar` -> ignored silently (documented).
5. `pattern.spacing / thickness < 3` (marks closer than ~3 px at 1100 px width: spacing ≤ 0.027 cells with thickness ≥ 0.009) -> warn "pattern is too dense".
6. A theme enabling **more than one** continuous animation (`borderAnimate`, `valueFill.animate`, `pattern.scroll`) is
   allowed but logged at info with the estimated cost class (see perf budgets in `03`).
7. `card.cornerMarks.inset < 0` with `card.gap < |inset|` -> warn (marks would overlap neighbours).

## 6. Worked example: what each draft theme exercises

See `06-themes.md` and the coverage matrix in `11-gap-audit.md`.

# Appendix A: key reference (generated from `theme.schema.json`)

| Key | Type / range | Default | Description |
|---|---|---|---|
| `format` | 1\|2 |  | Optional. 2 declares that the file uses format-2 keys; the host warns if it is newer than it supports. Absent = 1. |
| `meta` | object |  | Descriptive data. Never affects rendering. |
| `meta.name` | string |  | Display name for the editor. |
| `meta.description` | string |  |  |
| `meta.author` | string |  |  |
| `meta.license` | string |  |  |
| `meta.version` | string |  |  |
| `meta.mode` | dark\|light |  | Lets the editor group themes and pick a matching default. |
| `meta.tags` | array of string |  |  |
| `meta.pairsWith` | object |  | Recommendations for the config editor. |
| `meta.pairsWith.backgrounds` | array of string |  | Background names that suit this theme, best first. Names of built-in or user backgrounds. |
| `meta.samples` | array of object |  | Same shape as a background manifest's samples. |
| `colors` | object |  | Colours. |
| `colors.background` | color |  |  |
| `colors.cardFill` | color |  |  |
| `colors.cardBorder` | color |  |  |
| `colors.text` | color |  |  |
| `colors.textMuted` | color |  |  |
| `colors.accent` | color |  |  |
| `colors.accentDim` | color |  |  |
| `colors.good` | color |  |  |
| `colors.warning` | color |  |  |
| `colors.critical` | color |  |  |
| `colors.backgroundPalette` | array of color [1..4] |  | v1: one to four shader colours. |
| `colors.accent2` | color |  | NEW. A second accent for gradients, markers, redlines, tab highlights. Default: accent. |
| `colors.ramp` | array of color [2..5] |  | NEW. Low-to-high load colours. Default: [good, warning, critical]. Used when gauge.colorBy is 'ramp'. |
| `colors.cardTints` | array of color [1..8] |  | NEW. Fill colours that cards cycle through when card.tint is 'cycle' (Pop). |
| `colors.ink` | color |  | NEW. Text colour used on top of an accent or tint fill. Default: whichever of text/background contrasts most with the fill. |
| `card` | object |  | Card chrome. |
| `card.radius` | number 0..1 |  | v1. Capped at half the smaller side. |
| `card.borderWidth` | number 0..1 |  | v1. |
| `card.gap` | number 0..1 |  | v1. |
| `card.padding` | number 0..1 |  | v1. |
| `card.shape` | round\|chamfer | round | NEW. 'chamfer' cuts corners with straight lines (Ember). Default round. |
| `card.cut` | number 0..0.5 | 0.14 | NEW. Chamfer size, fraction of a cell, capped at half the smaller side. |
| `card.cutCorners` | array of tl\|tr\|br\|bl | ['tr', 'bl'] | NEW. Which corners are cut. Default [tr, bl]. |
| `card.fillGradient` | gradient |  | NEW. Replaces cardFill with a gradient. |
| `card.borderStyle` | solid\|dashed\|dotted\|none | solid | NEW. Default solid. |
| `card.borderDash` | object |  | NEW. Dash pattern for dashed/dotted. |
| `card.borderDash.on` | number 0.002..1 |  | Dash length. |
| `card.borderDash.off` | number 0.002..1 |  | Gap length. |
| `card.borderGradient` | object |  | NEW. Gradient border (Rigline, Ember, Obsidian hairline). |
| `card.borderGradient.colors` | array of color [2..8] |  | Stops. |
| `card.borderGradient.kind` | linear\|conic | linear | conic walks round the card, so it can rotate. |
| `card.borderGradient.angle` | number -360..360 | 135 | Linear only. |
| `card.borderAnimate` | object |  | NEW. Animated border. Bounded by motion.level. |
| `card.borderAnimate.mode` | none\|spin\|pulse | none | spin rotates a conic gradient; pulse breathes the alpha. |
| `card.borderAnimate.period` | number 1..120 | 7 | Seconds per cycle. |
| `card.cornerMarks` | object |  | NEW. Registration marks (Carbon, Blueprint). Drawn outside the clip, so needs a bleed margin. |
| `card.cornerMarks.style` | none\|bracket\|tick\|cross | none | Shape of each mark. |
| `card.cornerMarks.corners` | array of tl\|tr\|br\|bl |  | Default [tl, br]. |
| `card.cornerMarks.size` | number 0.01..0.5 |  | Arm length. |
| `card.cornerMarks.thickness` | number 0.002..0.1 |  | Line width. |
| `card.cornerMarks.inset` | number -0.1..0.1 | 0 | Offset from the corner; negative sits outside the card. |
| `card.cornerMarks.color` | color |  |  |
| `card.shadow` | object |  | NEW. Drop shadow. Bleeds outside the card. |
| `card.shadow.x` | number -0.5..0.5 | 0 | Offset. |
| `card.shadow.y` | number -0.5..0.5 | 0 | Offset. |
| `card.shadow.blur` | number 0..1 |  | 0 = hard offset (Pop). |
| `card.shadow.spread` | number -0.5..0.5 | 0 |  |
| `card.shadow.color` | color |  |  |
| `card.glow` | glow |  | NEW. Outer glow. Bleeds outside the card. |
| `card.highlight` | object |  | NEW. Inset edge light (Prism). |
| `card.highlight.top` | color |  | Specular line along the top edge. |
| `card.highlight.bottom` | color |  | Rim light along the bottom, inside. |
| `card.highlight.thickness` | number 0.001..0.1 | 0.01 |  |
| `card.sheen` | object |  | NEW. A radial light patch inside the card (Prism). |
| `card.sheen.color` | color |  |  |
| `card.sheen.x` | number -1..2 | 0.1 | Centre, fraction of card width. |
| `card.sheen.y` | number -1..2 | 0 | Centre, fraction of card height. |
| `card.sheen.size` | number 0.1..3 | 0.7 | Radius, fraction of card width. |
| `card.accentStrip` | object |  | NEW. A lit line along one edge of every card, inside the clip (Carbon scan line, Rigline tile strips). |
| `card.accentStrip.side` | top\|bottom\|left\|right | bottom |  |
| `card.accentStrip.thickness` | number 0.005..0.2 | 0.03 |  |
| `card.accentStrip.length` | number 0.1..1 | 0.72 | Fraction of the side. |
| `card.accentStrip.colors` | array of color [1..8] |  | Cycled by slot index (compact cards) or by card index. Default [accent]. |
| `card.accentStrip.fade` | boolean |  | Fade to transparent along the length (Carbon's scan line). |
| `card.accentStrip.glow` | glow |  |  |
| `card.tint` | none\|cycle | none | NEW. 'cycle' fills cards from colors.cardTints in page order. |
| `card.blur` | number 0..1 |  | RESERVED. Backdrop blur, fraction of a cell. Accepted and ignored in v1 of this work, with a one-time info log. See decision D1. |
| `card.compact` | object |  | NEW. A partial override applied to cards of 1x1 cell or less and to dock slots. |
| `card.compact.radius` | number 0..1 |  |  |
| `card.compact.borderWidth` | number 0..1 |  |  |
| `card.compact.padding` | number 0..1 |  |  |
| `card.compact.shape` | round\|chamfer\|circle |  | 'circle' makes square cards round (Obsidian dock). |
| `card.compact.accentStrip` | object |  | A lit strip on compact cards (Rigline tiles). Overrides card.accentStrip for them. |
| `card.compact.accentStrip.side` | top\|bottom\|left\|right | bottom |  |
| `card.compact.accentStrip.thickness` | number 0.005..0.2 | 0.03 |  |
| `card.compact.accentStrip.length` | number 0.1..1 | 0.72 | Fraction of the side. |
| `card.compact.accentStrip.colors` | array of color [1..8] |  | Cycled by slot index (compact cards) or by card index. Default [accent]. |
| `card.compact.accentStrip.fade` | boolean |  | Fade to transparent along the length (Carbon's scan line). |
| `card.compact.accentStrip.glow` | glow |  |  |
| `pattern` | object |  | NEW. A page-level texture drawn over the background and under the cards (not per card). |
| `pattern.kind` | none\|dots\|grid\|lines\|hatch\|scanlines | none | Default none. |
| `pattern.spacing` | number 0.02..2 | 0.13 | Distance between marks, fraction of a cell. |
| `pattern.majorEvery` | integer 2..20 |  | Every Nth line of a grid is drawn heavier (Blueprint: 5). |
| `pattern.thickness` | number 0.001..0.1 | 0.01 | Line width / dot radius. |
| `pattern.color` | color |  |  |
| `pattern.major` | object |  | Style of the heavier lines. |
| `pattern.major.color` | color |  |  |
| `pattern.major.thickness` | number 0.001..0.1 |  |  |
| `pattern.angle` | number -180..180 | 45 | hatch/lines direction, degrees. |
| `pattern.scroll` | object |  | Moves the pattern. Bounded by motion.level. |
| `pattern.scroll.dx` | number -5..5 | 0 | Cells per second. |
| `pattern.scroll.dy` | number -5..5 | 0 | Cells per second. |
| `divider` | object |  | NEW. The rule widgets draw between their sections. |
| `divider.style` | none\|line\|gradient\|dimension | line | dimension = a line with end ticks and an optional centred label (Blueprint). |
| `divider.color` | color |  |  |
| `divider.thickness` | number 0.001..0.05 | 0.01 |  |
| `divider.inset` | number 0..0.5 | 0 |  |
| `divider.gradientTo` | color |  |  |
| `well` | object |  | NEW. A recessed rounded rectangle widgets draw behind grouped content such as the weather forecast days (Rigline). Absent = no well. |
| `well.fill` | color |  |  |
| `well.radius` | number 0..0.5 | 0.09 |  |
| `well.border` | color |  |  |
| `chip` | object |  | NEW. Small status labels (OK, WARM, rig online). |
| `chip.shape` | none\|pill\|rect\|bracket | none |  |
| `chip.fill` | color |  |  |
| `chip.border` | color |  |  |
| `chip.textColor` | color |  |  |
| `chip.colorBy` | none\|status | status | status tints the chip by good/warning/critical. |
| `typography` | object |  | Type. |
| `typography.font` | string |  | v1. Default family for every role. |
| `typography.fonts` | object |  | NEW. Per-role overrides; each is a family or a file in the theme folder. Missing role falls back to typography.font. |
| `typography.fonts.value` | string |  |  |
| `typography.fonts.unit` | string |  |  |
| `typography.fonts.label` | string |  |  |
| `typography.fonts.body` | string |  |  |
| `typography.fonts.title` | string |  |  |
| `typography.weights` | object |  | v1. |
| `typography.weights.value` | number 1..1000 |  |  |
| `typography.weights.unit` | number 1..1000 |  |  |
| `typography.weights.label` | number 1..1000 |  |  |
| `typography.weights.body` | number 1..1000 |  |  |
| `typography.weights.title` | number 1..1000 |  |  |
| `typography.labelSize` | number 0.005..1 |  | v1. |
| `typography.bodySize` | number 0.005..1 |  | v1. |
| `typography.titleSize` | number 0.005..1 |  | v1. |
| `typography.unitRatio` | number 0.05..1 |  | v1. |
| `typography.valueScale` | number 0.5..2 | 1 | NEW. Multiplies the readout's size (Pop and Obsidian run big). |
| `typography.case` | object |  | NEW. Letter case applied at draw time. |
| `typography.case.label` | none\|upper\|lower | none |  |
| `typography.case.title` | none\|upper\|lower | none |  |
| `typography.case.unit` | none\|upper\|lower | none |  |
| `typography.tracking` | object |  | NEW. Letter spacing in em. Applied per glyph at draw time; the Readout's equal-width digits are kept. |
| `typography.tracking.value` | number -0.1..0.6 |  | em |
| `typography.tracking.label` | number -0.1..0.6 |  | em |
| `typography.tracking.title` | number -0.1..0.6 |  | em |
| `typography.tracking.body` | number -0.1..0.6 |  | em |
| `typography.slant` | number 0..20 | 0 | NEW. Synthetic skew of the value, degrees. Prefer a real italic file when the family has one. |
| `typography.valueFill` | object |  | NEW. Fills the value text with a gradient (Rigline clock). |
| `typography.valueFill.colors` | array of color [2..6] |  | Gradient stops. |
| `typography.valueFill.angle` | number -360..360 | 0 |  |
| `typography.valueFill.animate` | object |  | Slides the gradient. Bounded by motion.level. |
| `typography.valueFill.animate.period` | number 1..120 | 6 | Seconds per sweep. |
| `typography.valueGlow` | glow |  | NEW. Glow behind value text. |
| `stroke` | object |  | v1. |
| `stroke.thickness` | number 0.001..1 |  | v1. |
| `stroke.cap` | round\|square |  | v1. |
| `gauge` | object |  | Gauges. |
| `gauge.style` | ring\|bar\|verticalBar\|meter\|tach\|marker\|thickbar | ring | v1 had ring\|bar\|verticalBar. NEW: meter (thin bar with ticks and scale), tach (arc with ticks, redline and a needle), marker (hairline ring/bar with a jewel), thickbar (heavy outlined bar). A widget's explicit style still wins. |
| `gauge.labelPosition` | auto\|inside\|below\|above | auto | NEW. Where a gauge's label sits. auto keeps the v1 placement (Prism: below). |
| `gauge.sweep` | object |  | NEW. Ring/tach/marker geometry. |
| `gauge.sweep.start` | number -360..360 | 135 | Degrees, 0 = 3 o'clock, clockwise. v1 ring: 135. |
| `gauge.sweep.span` | number 30..360 | 270 | v1 ring: 270. |
| `gauge.segments` | object |  | NEW. Chops the fill into LED blocks. |
| `gauge.segments.length` | number 0.005..1 |  | Segment length (bars) or arc length in cells (rings). |
| `gauge.segments.gap` | number 0..1 |  | Space between segments. |
| `gauge.outline` | object |  | NEW. Outline round the track (thickbar). |
| `gauge.outline.width` | number 0.002..0.2 |  |  |
| `gauge.outline.color` | color |  |  |
| `gauge.ticks` | object |  | NEW. Tick marks on rings/tach/meter. |
| `gauge.ticks.count` | integer 2..60 |  |  |
| `gauge.ticks.majorEvery` | integer 1..20 |  |  |
| `gauge.ticks.length` | number 0.005..0.5 |  |  |
| `gauge.ticks.thickness` | number 0.001..0.05 |  |  |
| `gauge.ticks.color` | color |  |  |
| `gauge.ticks.labels` | boolean |  | Print numbers at major ticks. |
| `gauge.scale` | object |  | NEW. Scale labels under a bar (Paper). |
| `gauge.scale.labels` | array of string [2..11] |  | Printed under a bar at even spacing, e.g. ["0","50","100"]. |
| `gauge.scale.size` | number 0.005..0.5 |  |  |
| `gauge.redline` | object |  | NEW. |
| `gauge.redline.from` | number 0..1 | 0.85 | Fraction where the zone starts. |
| `gauge.redline.color` | color |  |  |
| `gauge.redline.glow` | glow |  |  |
| `gauge.redline.lightsAbove` | boolean |  | Zone only lights while the value is inside it (Ember). |
| `gauge.gradient` | ramp \| gradient |  | NEW. Fill gradient along the value: 'ramp' uses colors.ramp, or give stops. |
| `gauge.glow` | glow |  | NEW. Glow round the fill. color omitted = fill colour. |
| `gauge.marker` | object |  | NEW. Value marker (Obsidian jewel, Blueprint needle). |
| `gauge.marker.shape` | dot\|diamond\|needle\|line | dot |  |
| `gauge.marker.size` | number 0.005..0.5 |  |  |
| `gauge.marker.color` | color |  |  |
| `gauge.marker.halo` | boolean |  | Dark ring round the marker. |
| `gauge.crosshair` | boolean |  | NEW. Centre cross on dials (Blueprint). |
| `gauge.track` | object |  | NEW. Overrides the default accentDim track. |
| `gauge.track.color` | color |  |  |
| `gauge.track.widthRatio` | number 0.1..2 | 1 | Track width relative to stroke.thickness. |
| `gauge.colorBy` | accent\|ramp | accent | NEW. 'ramp' colours the fill by value along colors.ramp instead of the accent/warning/critical steps. |
| `gauge.valueTint` | boolean | False | NEW. Colours the readout with the same colour as the fill. |
| `gauge.sparkline` | object |  | NEW. A history line under the readout; needs Stats to keep a ring buffer. |
| `gauge.sparkline.enabled` | boolean |  |  |
| `gauge.sparkline.height` | number 0.05..1 | 0.3 |  |
| `gauge.sparkline.historySeconds` | number 10..600 | 60 |  |
| `gauge.sparkline.strokeWidth` | number 0.002..0.1 |  |  |
| `gauge.sparkline.fillAlpha` | number 0..1 | 0 |  |
| `gauge.status` | object |  | NEW. A status chip beside the label. |
| `gauge.status.enabled` | boolean |  |  |
| `gauge.status.thresholds` | array of number [2..2] |  | [warm, hot] as 0..1. Default [0.7,0.9]. |
| `gauge.status.labels` | array of string [3..3] |  | [ok, warm, hot]. |
| `indicator` | object |  | v1, unchanged. |
| `press` | object |  | v1, unchanged. |
| `dock` | object |  | v1, unchanged. |
| `icons` | object |  | NEW. Theme-specific icon set. |
| `icons.set` | string |  | Folder name inside the theme folder (icons/<set>/<key>.svg), or a built-in set name. Keys are in 04-widgets-and-icons.md. A missing key falls back to the built-in 'outline' set, then to the widget's own icon. |
| `icons.stroke` | number 0.005..0.2 |  | Override stroke width for set icons, fraction of the icon size. |
| `icons.tint` | string |  | 'accent', 'accent2', 'text', 'ramp', 'none' (keep the SVG's colours) or a #colour. |
| `icons.glow` | glow |  | Glow behind set icons (Rigline). Colour defaults to the icon's tint. |
| `icons.animated` | boolean | True | false = static icons even where the widget would use Lottie (weather). Default true. |
| `background` | object |  | NEW. Background pairing. |
| `background.default` | object |  | NEW. The animated background this theme selects when the user has not picked one. |
| `background.default.name` | string |  |  |
| `background.default.parameters` | object |  |  |
| `background.default.colors` | array of color [1..4] |  | Overrides backgroundPalette for this pairing. |
| `background.static` | object |  | NEW. Still background used when no animated background is selected, or when motion is off. Image, gradient or colour. |
| `background.static.image` | string |  | File in the theme folder. |
| `background.static.color` | color |  |  |
| `background.static.gradient` | object |  | Linear or radial gradient. |
| `background.static.gradient.colors` | array of color [2..6] |  | Stops. |
| `background.static.gradient.kind` | linear\|radial | linear |  |
| `background.static.gradient.angle` | number -360..360 | 90 | Linear only. |
| `background.static.gradient.cx` | number -1..2 | 0.5 | Radial centre x, fraction of width. |
| `background.static.gradient.cy` | number -1..2 | 0 | Radial centre y, fraction of height. |
| `background.static.gradient.radius` | number 0.1..3 | 0.7 | Radial radius, fraction of width. |
| `background.static.fit` | cover\|tile\|center | cover |  |
| `background.static.tileSize` | number 0.1..20 |  | Cells, when fit is tile. |
| `background.scrim` | object |  | NEW. A veil between the background and the cards, to hold text contrast. |
| `background.scrim.color` | color |  |  |
| `background.scrim.alpha` | number 0..1 |  |  |
| `motion` | object |  | NEW. |
| `motion.level` | full\|reduced\|off | full | The ceiling for theme-driven animation (borderAnimate, valueFill.animate, pattern.scroll, blinking separators). The effective level is the lower of this and the user/OS setting. Does not touch the shader background, which has its own control. |
