# 06 Themes

Eight new built-in themes. The draft files are `assets/themes/<name>/theme.json` and validate against
`assets/schema/theme.schema.json`. Values were converted from the mocks (mock px / 110 = cell fraction). They are **first guesses**:
tune each on the real 1100x3840 panel by eye, and record the final numbers (with the reason) in the theme's PR. Mock images are in
`assets/design-board/`.

Every theme also carries `meta` (name, description, mode, tags, `pairsWith`), `background.default` (what the theme selects when the user has not chosen one) and
`background.static` (a still fallback). The pairing lists on the theme and on the background manifests were generated from one table and are consistent (`07`).

## Acceptance for any theme PR

1. Validates against the schema; `urdeck-cli theme validate` is clean (PR 11).
2. Golden snapshot added for the standard test page at 1100x1920 and 1100x3840.
3. Looks like the mock at the same crop (side-by-side picture in the PR). Differences are listed and justified.
4. Contrast: text on card >= 4.5:1; muted text >= 3:1 (the validator reports it).
5. Works with `motion.level` `off` (animations stop; still looks complete) and with its `background.static`.
6. The site deck `site/decks/<name>/urdeck-config.json` and its pictures exist (`08`).

## Carbon (`carbon`, dark)

**For:** IT, NOC walls, business desks.

**Look:** Near-black ground with a faint 14 px dot grid; hairline cards (3 px radius) with phosphor registration corners at top-left and bottom-right; mono numerals; segmented LED bars; a sparkline under each reading; status words (OK / WARM / HOT) that change colour; one phosphor accent (#4ff0c8) that turns amber then red under load.

**Fonts:** JetBrains Mono (values, units, labels), Instrument Sans (body).

**If a host only honours v1 keys:** With only v1 keys honoured: dark square-ish cards, hairline border, accent mint, JetBrains Mono values, plain bar gauges. Still coherent.

**Acceptance picture:** Match `design-board/Main` Carbon panel: corner marks visible at card corners, segmented bar with a partial last block, sparkline polyline above the bar, CPU shows "OK" in accent and GPU "WARM" in amber at 68 % / 82 %.

**Site deck widgets:** Clock (Format 24h, ShowSeconds, BlinkSeparator, Header label "Local time"), Stats 2x1 (CPU, GPU, sparkline), Weather 4x2, Shortcut x4 with labels.

**Pairs with backgrounds (best first):** contour, streams, plotter, nebula, warpgate. Default: `contour`.

**Format-2 knobs this theme exercises:** `background.default`, `background.static`, `card.cornerMarks`, `chip.colorBy`, `chip.shape`, `colors.ink`, `divider.color`, `divider.style`, `divider.thickness`, `gauge.colorBy`, `gauge.segments`, `gauge.sparkline`, `gauge.status`, `icons.animated`, `icons.set`, `icons.stroke`, `icons.tint`, `motion.level`, `pattern.color`, `pattern.kind`, `pattern.spacing`, `pattern.thickness`, `typography.case`, `typography.fonts`, `typography.tracking`, `typography.valueScale`

## Rigline (`rigline`, dark)

**For:** Gamers with RGB; case displays.

**Look:** Every card has a 1.5 px conic spectrum border that rotates once per 7 s plus a soft violet glow; the clock digits are filled with a sliding spectrum gradient; gauges are 270° arcs with a gradient stroke and glow whose hue follows load; tiles light a coloured strip underneath like case fans; faint scanlines crawl down the page.

**Fonts:** Chakra Petch Bold (all roles; italic look via `slant` 10).

**If a host only honours v1 keys:** With only v1 keys honoured: near-black violet cards, cyan accent, Chakra Petch bold, plain ring gauges. Reads as a tidy dark theme; loses the "RGB" energy.

**Acceptance picture:** Match `design-board/Main` Rigline: the border visibly travels; ring gradient cyan→violet→magenta; clock digits show a moving gradient; tiles have 4 different strip colours.

**Site deck widgets:** Clock (Subtitle "Rig online"), Stats 2x1 rings, Weather 4x2, Shortcut x4 without labels.

**Cost / risk:** **Heaviest theme**: three continuous animations (border, value fill, scanline scroll). It is the test case for budget S2.

**Pairs with backgrounds (best first):** horizon, spectrum, equalizer, streams, warpgate, vortex, embers. Default: `horizon`.

**Format-2 knobs this theme exercises:** `background.default`, `background.scrim`, `background.static`, `card.borderAnimate`, `card.borderGradient`, `card.compact`, `card.fillGradient`, `card.glow`, `chip.shape`, `colors.accent2`, `colors.ramp`, `gauge.colorBy`, `gauge.glow`, `gauge.gradient`, `gauge.sweep`, `gauge.track`, `gauge.valueTint`, `icons.animated`, `icons.set`, `icons.stroke`, `icons.tint`, `motion.level`, `pattern.color`, `pattern.kind`, `pattern.scroll`, `pattern.spacing`, `pattern.thickness`, `typography.case`, `typography.slant`, `typography.tracking`, `typography.valueFill`, `typography.valueGlow`, `typography.valueScale`

## Prism (`prism`, dark)

**For:** Everyone; the safe showpiece over any moving background.

**Look:** Thick frosted glass: 27 % radius, a vertical gradient fill, a bright specular line along the top edge and a faint rim at the bottom, a radial sheen patch in the top-left, a soft drop shadow, light (300) wide numerals, full-circle ring gauges with a white gradient.

**Fonts:** Bricolage Grotesque variable (opsz+wght).

**If a host only honours v1 keys:** With only v1 keys honoured: translucent white cards (like glass today) with a large radius and Bricolage light numerals.

**Acceptance picture:** Match `design-board/Main` Prism: specular top edge visible, sheen in the corner, full ring (not 270°) starting at 12 o'clock.

**Site deck widgets:** Clock, Stats 2x1 (rings), Weather 4x2, Shortcut x4 no labels. Uses `card.blur` later (reserved).

**Cost / risk:** `card.blur` is **reserved**: without it Prism is "glass without frost". The mocks use `backdrop-filter`. See decision D1.

**Pairs with backgrounds (best first):** nebula, vortex, horizon, spectrum, ripple. Default: `nebula`.

**Format-2 knobs this theme exercises:** `background.default`, `background.static`, `card.blur`, `card.compact`, `card.fillGradient`, `card.highlight`, `card.shadow`, `card.sheen`, `colors.accent2`, `gauge.glow`, `gauge.gradient`, `gauge.sweep`, `gauge.track`, `icons.animated`, `icons.set`, `icons.stroke`, `icons.tint`, `motion.level`, `typography.tracking`, `typography.valueScale`

## Paper (`paper`, light)

**For:** Offices, bright rooms, anyone who wants light mode that looks designed.

**Look:** Warm off-white ground, cards with no border and a soft shadow (plus a 1 px top highlight), one vermilion accent (#e5502b), tabular figures, thin meter bars with a 0 / 50 / 100 scale beneath, hairline dividers.

**Fonts:** Instrument Sans (values, body), JetBrains Mono (labels, units).

**If a host only honours v1 keys:** With only v1 keys honoured: light cards, vermilion accent, Instrument Sans, bar gauges without scale. Fine.

**Acceptance picture:** Match `design-board/Main` Paper: shadow only (no border), meter scale labels under bars, "Wk 42" in the clock header.

**Site deck widgets:** Clock (ShowWeek), Stats 2x1 meters, Weather 4x2, Shortcut x4 with labels.

**Pairs with backgrounds (best first):** contour, ripple, plotter, shapes. Default: `contour`.

**Format-2 knobs this theme exercises:** `background.default`, `background.static`, `card.highlight`, `card.shadow`, `divider.color`, `divider.style`, `divider.thickness`, `gauge.colorBy`, `gauge.scale`, `gauge.track`, `icons.animated`, `icons.set`, `icons.stroke`, `icons.tint`, `motion.level`, `typography.case`, `typography.fonts`, `typography.tracking`, `typography.valueScale`

## Ember (`ember`, dark)

**For:** Sim racers and anyone who wants speed on a screen.

**Look:** Chamfered cards (top-right and bottom-left cut 16 px) with an amber-to-brown gradient border and a diagonal hatch on the page; tach gauges with 28 tick marks, a red-line zone from 80 % that lights when load is above it, a needle and an amber glow; condensed italic numerals glowing orange.

**Fonts:** Barlow Condensed Bold (all roles; `slant` 10).

**If a host only honours v1 keys:** With only v1 keys honoured: square dark brown cards with an orange border, Barlow Condensed numerals, plain ring gauges.

**Acceptance picture:** Match `design-board/Themes2` Ember: chamfered corners; tach with tick ring and a lit red zone at 82 %.

**Site deck widgets:** Clock (Subtitle "Session 04:12" as a string), Stats 2x1 tachs, Weather, Shortcut x4.

**Pairs with backgrounds (best first):** embers, warpgate, equalizer. Default: `embers`.

**Format-2 knobs this theme exercises:** `background.default`, `background.static`, `card.borderGradient`, `card.cut`, `card.cutCorners`, `card.fillGradient`, `card.shape`, `colors.accent2`, `gauge.glow`, `gauge.gradient`, `gauge.marker`, `gauge.redline`, `gauge.sweep`, `gauge.ticks`, `gauge.track`, `icons.animated`, `icons.set`, `icons.stroke`, `icons.tint`, `motion.level`, `pattern.angle`, `pattern.color`, `pattern.kind`, `pattern.spacing`, `pattern.thickness`, `typography.case`, `typography.slant`, `typography.tracking`, `typography.valueGlow`, `typography.valueScale`

## Blueprint (`blueprint`, dark)

**For:** Engineers; "a technical drawing come alive".

**Look:** Cyanotype ground with a drafting grid (a major line every 5 minor lines), dashed card outlines, yellow bracket corner marks that sit just outside the card, protractor-style dials with a single needle and a centre crosshair, dimension-line dividers with a label, IBM Plex Mono.

**Fonts:** IBM Plex Mono.

**If a host only honours v1 keys:** With only v1 keys honoured: dark-blue square cards with a thin pale-blue border and mono type. Plain, but on theme.

**Acceptance picture:** Match `design-board/Themes2` Blueprint: dashed borders, yellow corner marks outside the card, a 180° dial with ticks and numerals.

**Site deck widgets:** Clock, Stats 2x1 dials, Weather (5-DAY dimension divider), Shortcut x4 with labels.

**Cost / risk:** Outside corner marks need the gap (`|inset| <= gap/2`): the draft uses inset -0.045 with gap 0.09. Needs the underlay (S1).

**Pairs with backgrounds (best first):** plotter, contour, streams. Default: `plotter`.

**Format-2 knobs this theme exercises:** `background.default`, `background.static`, `card.borderDash`, `card.borderStyle`, `card.compact`, `card.cornerMarks`, `colors.accent2`, `divider.color`, `divider.style`, `divider.thickness`, `gauge.crosshair`, `gauge.marker`, `gauge.sweep`, `gauge.ticks`, `gauge.track`, `icons.animated`, `icons.set`, `icons.stroke`, `icons.tint`, `motion.level`, `pattern.color`, `pattern.kind`, `pattern.major`, `pattern.majorEvery`, `pattern.spacing`, `pattern.thickness`, `typography.case`, `typography.tracking`, `typography.valueScale`

## Pop (`pop`, light)

**For:** Anyone who wants fun; also photographs well for the site.

**Look:** Flat saturated colour per card (yellow, sky, pink, lime, blue cycling), 3 px black outlines, hard offset shadows (6 px, no blur), huge heavy numerals, thick outlined bars with a black fill, black ink text on tints.

**Fonts:** Archivo Black (values), Archivo (everything else).

**If a host only honours v1 keys:** With only v1 keys honoured: cream background, yellow cards with a thick black border, Archivo type. No hard shadow, no colour cycling.

**Acceptance picture:** Match `design-board/Themes2` Pop: hard shadows outside every card; five tint colours in order; bars with a black outline.

**Site deck widgets:** Clock, Stats 2x1 thick bars, Weather, Shortcut x4.

**Cost / risk:** Hard shadow is bleed: needs the underlay (S1).

**Pairs with backgrounds (best first):** shapes, ripple. Default: `shapes`.

**Format-2 knobs this theme exercises:** `background.default`, `background.static`, `card.compact`, `card.shadow`, `card.tint`, `colors.accent2`, `colors.cardTints`, `colors.ink`, `gauge.colorBy`, `gauge.outline`, `gauge.track`, `icons.animated`, `icons.set`, `icons.stroke`, `icons.tint`, `motion.level`, `typography.case`, `typography.fonts`, `typography.tracking`, `typography.valueScale`

## Obsidian (`obsidian`, dark)

**For:** The person with a very tidy desk; executive calm.

**Look:** Warm black with a champagne gradient hairline border (bright at the top fading down) and a top highlight, Didone numerals (Bodoni Moda) at a large scale, small caps-style labels with wide tracking (0.28 em), gauges reduced to a thin ring with one diamond jewel marker, circular dock/tile buttons, a gradient divider.

**Fonts:** Bodoni Moda variable (values), Jost variable (labels, body). Bodoni replaces the earlier font Pat disliked.

**If a host only honours v1 keys:** With only v1 keys honoured: warm black cards, gold-ish border and text, Jost body. Numerals revert to Jost.

**Acceptance picture:** Match `design-board/Themes2` Obsidian: gradient hairline border, jewel marker on a thin ring, circular tiles.

**Site deck widgets:** Clock, Stats 2x1 rings, Weather, Shortcut x4 (circular).

**Pairs with backgrounds (best first):** nebula, vortex, warpgate. Default: `nebula`.

**Format-2 knobs this theme exercises:** `background.default`, `background.scrim`, `background.static`, `card.borderGradient`, `card.compact`, `card.fillGradient`, `card.highlight`, `colors.accent2`, `divider.color`, `divider.gradientTo`, `divider.style`, `divider.thickness`, `gauge.marker`, `gauge.sweep`, `gauge.track`, `icons.animated`, `icons.set`, `icons.stroke`, `icons.tint`, `motion.level`, `typography.case`, `typography.fonts`, `typography.tracking`, `typography.valueScale`

## Existing themes

Keep `default-dark` (baseline for the merge; the default), `default-light` and `glass`. Optionally give them `meta` and `background.default`
(`aurora`) so the editor can list them like the new ones. That is a tiny, safe PR (PR 5) and must not change their pixels.

## Palettes at a glance

| Theme | background | cardFill | accent | accent2 | text |
|---|---|---|---|---|---|
| carbon | `#090b0e` | `#0f1318` | `#4ff0c8` | `` | `#e6e9ee` |
| rigline | `#07060d` | `#141026` | `#19e6ff` | `#ff3ea5` | `#f4f2ff` |
| prism | `#10163a` | `#26ffffff` | `#ffffff` | `#bfe6ff` | `#ffffff` |
| paper | `#e9e7e0` | `#f8f7f3` | `#e5502b` | `` | `#17181c` |
| ember | `#0b0806` | `#140d09` | `#ffb347` | `#ff3b2a` | `#fbeede` |
| blueprint | `#0c2d52` | `#cc0c2d52` | `#ffd34d` | `#9fd0ff` | `#dcecff` |
| pop | `#f6f0e0` | `#ffd84a` | `#111111` | `#3b5bff` | `#111111` |
| obsidian | `#0a0908` | `#15120e` | `#d9b86c` | `#f3e3b5` | `#efe6d3` |
