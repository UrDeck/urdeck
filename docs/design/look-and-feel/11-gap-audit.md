# 11 Gap audit: did we miss a knob or a feature?

Pat asked: *"double check your work to make sure we have enough knobs in the proposed theme schema changes and that we are not missing any new features/functions."*
This is that check. Method: (1) walk every mock element in the eight theme panels and the twelve background tiles and ask "which knob produces this?";
(2) walk every widget (Clock, Stats, Weather, Shortcut) and ask "what new behaviour does a theme assume?"; (3) read each knob and ask "does any theme use it, and is anything in it already provided by v1?";
(4) cross-check against the earlier Knobs board on the design canvas and correct it.

## 1. Result in one paragraph

The first draft of the schema was missing **five knobs** and the widget layer was missing **eleven behaviours**; all are now in `02` and `04`. Four earlier claims on the Knobs board were **wrong** and were removed or renamed (section 4).
After the fixes every visible element of every mock maps to a knob or an explicit, justified deferral (backdrop blur). Eighteen knobs are exercised by no draft theme (section 5); they are kept on purpose or cut, with a reason each.

## 2. Mock feature to knob matrix

| Theme | Mock feature | Knob / feature | Status | PR |
|---|---|---|---|---|
| Carbon | Dot-grid page texture | `pattern.kind dots` | new | PR 8 |
| Carbon | Registration corners (outside the card edge) | `card.cornerMarks` | new | PR 2 |
| Carbon | 3 px radius hairline cards | `card.radius .03`, `borderWidth` | existing | n/a |
| Carbon | Mono numerals, uppercase tracked labels | `typography.fonts`, `case`, `tracking` | new | PR 3 |
| Carbon | Segmented LED bars with a partial last block | `gauge.segments` | new | PR 4 |
| Carbon | Sparkline under the reading | `gauge.sparkline` + Stats history buffer | new (widget feature) | PR 4 |
| Carbon | OK / WARM / HOT words | `gauge.status`, `chip` | new | PR 4 |
| Carbon | Blinking colon, seconds, "Local time" header | Clock `BlinkSeparator`, `ShowSeconds`, `Header` | new (widget options) | PR 4 |
| Carbon | Accent scan line along the clock card's bottom edge | `card.accentStrip` (fade) | **added in the gap audit** | PR 2 |
| Rigline | Spinning spectrum card edge | `card.borderGradient conic` + `borderAnimate spin` | new | PR 2 (static), PR 9 (animated) |
| Rigline | Outer glow on cards | `card.glow` | new | PR 2 |
| Rigline | Scanlines crawling down the page | `pattern.kind scanlines` + `scroll` | new | PR 8, PR 9 |
| Rigline | Gradient-filled digits that slide | `typography.valueFill` (+ `animate`) | new | PR 3, PR 9 |
| Rigline | Glowing italic numerals | `typography.valueGlow`, `slant` | new | PR 3 |
| Rigline | 270° gradient ring whose hue follows load, with glow | `gauge.gradient`, `colorBy ramp`, `glow`, `sweep` | new | PR 4 |
| Rigline | Lit strip under 1x1 tiles, one colour each | `card.compact.accentStrip` | new | PR 2 |
| Rigline | Forecast days on subtle rounded backing | `well` | **added in the gap audit** | PR 4 |
| Rigline | Glowing weather icons | `icons.glow` | **added in the gap audit** | PR 10 |
| Rigline | "Rig online" in the clock header | Clock `Subtitle` | new (widget option) | PR 4 |
| Prism | Frosted glass blur | `card.blur` (reserved) | deferred (D1) | n/a |
| Prism | Specular top edge, rim light | `card.highlight` | new | PR 2 |
| Prism | Corner sheen | `card.sheen` | new | PR 2 |
| Prism | Soft drop shadow | `card.shadow` (blur > 0) | new | PR 2 |
| Prism | Full 360° ring starting at 12 o'clock | `gauge.sweep {-90,360}` | new | PR 4 |
| Prism | Label below the ring | `gauge.labelPosition below` | **added in the gap audit** | PR 4 |
| Prism | Light, wide numerals | `typography.weights`, `valueScale` | existing + new | PR 3 |
| Paper | Soft shadow instead of border | `card.shadow` | new | PR 2 |
| Paper | Thin meter with 0/50/100 scale | `gauge.style meter`, `scale` | new | PR 4 |
| Paper | Hairline rules between sections | `divider` | new | PR 4 |
| Paper | Week number in the clock header | Clock `ShowWeek` | new (widget option) | PR 4 |
| Paper | Tabular figures | (Readout already equal-width) | existing | n/a |
| Ember | Chamfered cards | `card.shape chamfer`, `cut`, `cutCorners` | new | PR 2 |
| Ember | Gradient border, diagonal hatch page | `card.borderGradient`, `pattern hatch` | new | PR 2, PR 8 |
| Ember | Tach: ticks, redline that lights, needle | `gauge.style tach`, `ticks`, `redline`, `marker` | new | PR 4 |
| Ember | Condensed italic glowing numerals | `typography.fonts`, `slant`, `valueGlow` | new | PR 3 |
| Blueprint | Drafting grid with majors | `pattern grid`, `majorEvery`, `major` | new | PR 8 |
| Blueprint | Dashed outlines | `card.borderStyle dashed`, `borderDash` | new | PR 2 |
| Blueprint | Yellow marks outside the corners; tick on tiles | `card.cornerMarks`, `compact.cornerMarks` | new | PR 2 |
| Blueprint | Dimension-line divider with label | `divider.style dimension` | new | PR 4 |
| Blueprint | Protractor dial with needle and crosshair | `gauge.style marker`, `sweep`, `ticks labels`, `crosshair` | new | PR 4 |
| Pop | Flat colour per card | `colors.cardTints`, `card.tint cycle` | new | PR 2 |
| Pop | Hard offset shadow | `card.shadow blur 0` | new | PR 2 |
| Pop | Huge heavy numerals | `typography.fonts`, `valueScale` | new | PR 3 |
| Pop | Thick outlined bar | `gauge.style thickbar`, `outline` | new | PR 4 |
| Pop | Weather condition as an outlined yellow pill | `chip` shape pill, fill, border | new | PR 4 |
| Pop | Forecast days as outlined tiles | `well` (fill, radius, border) | new | PR 4 |
| Pop | Heavy-stroke icons, black ink on tints | `icons.stroke`, `colors.ink` | new | PR 10, PR 2 |
| Obsidian | Gradient hairline border + top highlight | `card.borderGradient linear`, `highlight` | new | PR 2 |
| Obsidian | Didone numerals, tracked caps labels | `typography.fonts`, `case`, `tracking` | new | PR 3 |
| Obsidian | Thin ring with a jewel marker | `gauge.style marker`, `marker diamond halo` | new | PR 4 |
| Obsidian | Circular tiles / dock buttons | `card.compact.shape circle` | new | PR 2 |
| Obsidian | Warm radial glow at the top of the page | `background.static.gradient radial` | **added in the gap audit** | PR 5 |
| All | Theme picks its background; fallback still image | `background.default`, `static`, `scrim` | new | PR 5 |
| All | Theme icon sets (weather, glyphs) | `icons.*` | new | PR 10 |
| All | Recommended backgrounds per theme and vice versa | `meta.pairsWith`, manifest `pairsWith` | new | PR 5 |
| All | Respect "reduce motion" | `motion.level`, config `reduceMotion`, `MotionPolicy` | new | PR 8, PR 9 |

### Knobs added by this audit (not on the earlier board)
1. `card.accentStrip` (Carbon scan line; generalises the compact-only strip)
2. `well` (Rigline forecast backing)
3. `gauge.labelPosition` (Prism label below ring)
4. `icons.glow` (Rigline icon glow)
5. `background.static.gradient.kind radial` (Obsidian's top glow)

## 3. Behaviour (not knobs) that themes assume

| # | New behaviour | Where | Why a theme needs it |
|---|---|---|---|
| 1 | Stats keeps a short history per slot | Stats widget | sparkline (Carbon) |
| 2 | Status text derived from level/threshold | Stats + `Chip` | OK/WARM/HOT |
| 3 | Clock `ShowSeconds`, `ShowWeek`, `Subtitle`, `BlinkSeparator`, `Header` | Clock widget | Carbon, Paper, Rigline, Ember |
| 4 | Shortcut `theme:<key>` glyph icons and `ShowLabel` | Shortcut widget | glyph tiles in decks |
| 5 | Weather themed icon resolution and static poster mode | Weather widget | icon sets |
| 6 | Card index and compact detection for tint and strips | engine/host | Pop, Rigline |
| 7 | Page underlay for bleed (shadow, glow, outside marks) and pattern | engine/host | Pop, Prism, Blueprint, Carbon |
| 8 | Theme-driven animation on the host frame clock, with a ceiling | host | Rigline |
| 9 | Per-role font loading with fallbacks | engine | Carbon, Obsidian, Pop, Paper |
| 10 | Effective-background resolution (user > theme default > static > colour) | engine | all |
| 11 | Pairing index and catalogs for the editor | engine | editor |

## 4. Corrections to the earlier Knobs board (apply to `assets/design-board/Knobs`)

| Earlier claim | Correction | Why |
|---|---|---|
| `typography.numerals` (tabular figures) | **Removed** | `Readout` already draws equal-width digits |
| Gauge style `arc` (a 270° arc) | **Removed** | `ring` already is a 270° arc starting at 135° |
| Nested `card.border.*` keys | **Flattened** to `card.borderStyle`, `borderDash`, `borderGradient`, `borderAnimate` | v1 already has flat `card.borderWidth`; nesting would conflict |
| Card pattern `card.pattern` | **Moved** to top-level `pattern` (page level) | a dot grid must be continuous across the gaps; per-card patterns would restart in each card |
| `widgets.<kind>.style` | Not adopted; widget-owned | widgets already have their own `Style` |
| Warp Gate `tint` parameter | **Removed** (never existed in the shader) | caption said `streaks, ringRate, coreSize, swirl` |
| Ripple `scale` parameter | **Renamed `ripples`** | `scale` is reserved by the product (SHADER032) |
| `pairsWith` as a theme-only thing | Both sides + closure rule | `07` |

## 5. Knobs that no draft theme uses (kept or cut)

Used by none of the eight drafts: `card.accentStrip.glow`, `card.compact.padding`, `card.compact.accentStrip.fade`, `divider.inset`, `typography.fonts.body`, `typography.fonts.title`, `typography.case.unit`, `typography.tracking.body`, `gauge.sparkline.fillAlpha`, `background.default.parameters`, `background.default.colors`, `background.static.image`, `background.static.fit`, `background.static.tileSize`.


Decisions:
- **Keep** (cheap, expected by users writing their own themes, and exercised by a unit test): `card.compact.padding`, `card.accentStrip.glow`, `card.compact.accentStrip.fade`, `well.border`, `divider.inset`, `typography.fonts.body/title`, `typography.case.unit`, `typography.tracking.body`, `gauge.sparkline.fillAlpha`, `background.default.parameters/colors`, `background.static.image/fit/tileSize` (images are how users will theme Blueprint-like static looks).
- **`chip`** is now used by Pop (pill, filled, outlined) and by Carbon/Rigline as plain text (`none`); `chip.shape` rect and bracket have no draft user but are the natural neighbours.
- Enum values no draft uses: `borderStyle dotted`, `pattern.kind lines`, `cornerMarks.style cross`, `marker.shape dot/line`, `divider.style line` (Carbon and Paper use it), `card.tint none`. Keep; they are the natural neighbours of used values.

Knobs used by exactly one theme (fine, they are signature features): `background.static.gradient.cx` (obsidian), `background.static.gradient.cy` (obsidian), `background.static.gradient.kind` (obsidian), `background.static.gradient.radius` (obsidian), `card.accentStrip.colors` (carbon), `card.accentStrip.fade` (carbon), `card.accentStrip.length` (carbon), `card.accentStrip.side` (carbon), `card.accentStrip.thickness` (carbon), `card.blur` (prism), `card.borderAnimate.mode` (rigline), `card.borderAnimate.period` (rigline), `card.borderDash.off` (blueprint), `card.borderDash.on` (blueprint), `card.borderStyle` (blueprint), `card.compact.accentStrip.colors` (rigline), `card.compact.accentStrip.glow` (rigline), `card.compact.accentStrip.length` (rigline), `card.compact.accentStrip.side` (rigline), `card.compact.accentStrip.thickness` (rigline), `card.compact.borderWidth` (obsidian), `card.compact.shape` (obsidian), `card.cut` (ember), `card.cutCorners` (ember), `card.glow` (rigline), `card.highlight.bottom` (prism), `card.shadow.spread` (paper), `card.shape` (ember), `card.sheen.color` (prism), `card.sheen.size` (prism), `card.sheen.x` (prism), `card.sheen.y` (prism), `card.tint` (pop), `chip.border` (pop), `chip.fill` (pop), `chip.textColor` (pop), `colors.cardTints` (pop), `colors.ramp` (rigline), `divider.gradientTo` (obsidian), `gauge.crosshair` (blueprint), `gauge.labelPosition` (prism), `gauge.marker.halo` (obsidian), `gauge.outline.color` (pop), `gauge.outline.width` (pop), `gauge.redline.color` (ember), `gauge.redline.from` (ember), `gauge.redline.glow` (ember), `gauge.redline.lightsAbove` (ember), `gauge.scale.labels` (paper), `gauge.scale.size` (paper), `gauge.segments.gap` (carbon), `gauge.segments.length` (carbon), `gauge.sparkline.enabled` (carbon), `gauge.sparkline.height` (carbon), `gauge.sparkline.historySeconds` (carbon), `gauge.sparkline.strokeWidth` (carbon), `gauge.status.enabled` (carbon), `gauge.status.labels` (carbon), `gauge.status.thresholds` (carbon), `gauge.valueTint` (rigline), `icons.glow` (rigline), `pattern.angle` (ember), `pattern.major.color` (blueprint), `pattern.major.thickness` (blueprint), `pattern.majorEvery` (blueprint), `pattern.scroll.dx` (rigline), `pattern.scroll.dy` (rigline), `typography.tracking.title` (obsidian), `typography.valueFill.angle` (rigline), `typography.valueFill.animate.period` (rigline), `typography.valueFill.colors` (rigline), `well.border` (pop).


## 6. What we deliberately did not add (and why)

| Idea | Not added because |
|---|---|
| `typography.numerals` | already provided |
| Per-widget theme overrides | breaks "the theme owns the card"; use a theme per deck |
| Arbitrary SkSL on cards | cost and support burden; backgrounds already have shaders |
| Per-card animated backgrounds | same |
| Real backdrop blur | D1 |
| Audio-reactive anything | D13 |
| CSS-like free-form effects (`filter`, `mix-blend-mode`) | unbounded; every effect is a named, ranged knob |
| Theme inheritance (`extends`) | merge over default-dark is enough; add later if users ask (the schema has room: `"extends": "carbon"`) |
| Per-theme sounds/haptics | not a visual concern |
| Blueprint's per-widget caption copy ("SHEET 01 · TIME", "FIG. A · CPU", "H 21 · M 47", "A1..A4") | it is text, not styling: the site deck sets widget `Label`/`Subtitle` strings; a `Divider` label comes from the widget's own label |
| Dock-specific chrome beyond `compact` | `card.compact` covers it; add `dock.*` only if a real need appears |
| Localised weather words | parked in the repo already |

## 7. Residual risks in the knob set itself

- **Over-wide surface.** About 160 new keys. Mitigation: every key optional with a default; docs generated from the schema; the "no drift" test.
- **Interactions.** Some combinations look bad (`borderGradient` conic on a chamfer; `glow` plus `shadow`). The validator warns on the known ones (`02 §5`); the rest are taste.
- **Mock fidelity.** The mocks use CSS features (conic gradients on `@property`, `backdrop-filter`) that Skia reproduces differently. Expect small differences; review against the picture, not the CSS.
- **Per-key behaviour at the extremes** (huge `tracking`, `span` 30°, `count` 60 ticks) is clamped by ranges, but ugly values are possible. That is acceptable for user themes; built-ins are reviewed by eye.
