## Why

Rigline is the last of the eight looks, and the one the engine could not draw until the motion change: a conic spectrum
spinning round every card edge, glowing slanted numerals, scanlines, gradient arc gauges coloured by load, and tile strips
lit underneath like case fans. Every knob it needs is built (`card-chrome-*`, `card-compact`, `theme-typography`,
`theme-gauges`, `page-pattern`, `theme-icons`, `motion`), so it exists only as a test fixture. Shipping it closes the
theme work and is the first built-in theme that moves, so it is also the first measurement of a whole animated theme
(border, glow, ramp arcs, scanlines) on the panel.

## What Changes

- Rigline (dark) becomes a built-in theme: `src/UrDeck.Engine/Themes/Builtin/rigline.json` (flat layout), listed in
  `ThemeStore.BuiltInNames`, selectable with `"theme": "rigline"`.
- Chakra Petch Bold (static, OFL 1.1) is bundled with its licence text and an entry in `THIRD-PARTY-NOTICES.md`; the
  italic look is the theme's `slant: 10`, not a second font file.
- The draft is tuned to load clean: body weight 500 becomes the weight the Bold-only file has (700), so no warning is
  logged; `valueScale` stays at 1 (nothing above 1); every `THEME060` to `THEME062` finding is fixed; AARRGGBB colours are
  checked one by one.
- Tuned on the 1100x3840 panel, over `horizon`; final values and reasons are recorded in the design's tuning record (and
  go in the pull request). The panel pass changed the card fill (translucent), the background palette (an orange-red sun)
  and the card glow (a tight halo that fits the gap). The whole theme is measured (CPU, private memory) at motion `full`, `reduced`
  and `off`, with the display off and the deck hidden, and the result is accepted by the owner or the theme is lightened.
- Goldens for Rigline are redrawn from the built-in (page and dock); the v1 goldens and the other themes' stay unchanged.
  The fixture copy is removed and its users repointed.
- Website: a sample deck (`site/decks/rigline`) and its picture, `themes.json`, the themes page, `docs/themes.md`,
  `MEDIA-CHECKLIST.md`, and `docs/design/look-and-feel/STATUS.md` (PR 16 done, theme work complete).
- A fix found on the panel: the Clock's seconds made the time jump (the unit's width changed with its digits, and the value
  is fitted to the width left). `Readout` gains `ReadoutOptions.WidestUnit` and draws and measures the unit's digits with
  equal advances, as it already does the value's; the Clock passes its widest suffix and drops its own `KeepSize`
  workaround. This is the one SDK and widget change here; it applies to every theme.
- Not in this change: `card.compact.borderAnimate` (compact cards take the page card's animation; recorded as a follow-up
  only if the tile strips look wrong), `typography.valueFill.animate` and `pattern.scroll` (deferred), SDK `Theme.Motion`,
  any theme picker. Optional: spinning-border loops for the site through `npm run media`, and a GPU check of a moving
  shader past the 4-hour wrap.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `theme`: "Rigline Built-in Theme" (added, so that it does not collide with the sibling theme changes that edit "Built-in
  Themes" and "Built-in Theme Metadata"): name, mode, font, pairings, default background, loads clean.
- `motion`: "A Built-in Theme May Animate" (added): Rigline's border turns at `full`, stands still at `reduced` and `off`,
  and pauses with the display and the hidden deck.
- `components`: "Readout Size Is Stable" (modified): the unit's digits have equal widths, and a widget may state the widest unit.
- `widget-clock`: "Clock Seconds Do Not Move The Time" (added).

## Impact

- `src/UrDeck.Engine/Themes/Builtin/` (`rigline.json`, `ChakraPetch-Bold.ttf`, `ChakraPetch-OFL.txt`; picked up by the
  existing `EmbeddedResource` globs), `ThemeStore.BuiltInNames`. No change to the resolver, painters or host.
- `sdk/UrDeck.Sdk/Components/Readout.cs` (MIT: `WidestUnit`, equal unit digits) and `widgets/UrDeck.Widgets.Clock`
  (passes it; `KeepSize` removed). Other themes' goldens are unchanged by it.
- Engine grows by the font (about 100 KB); memory grows only while the theme is loaded (border sprites measured in
  `docs/perf/motion.md`).
- `tests/UrDeck.Engine.Tests`: `Fixtures/themes/rigline` removed, users repointed (`GoldenTests`, `CardChromeTests`,
  `CardUnderlayTests`, and others found by search), goldens `rigline-page.png` and `rigline-dock.png` redrawn, catalog and
  pairing tests extended.
- `THIRD-PARTY-NOTICES.md`, `docs/themes.md`, `docs/perf/` (a Rigline measurement section), `STATUS.md`, `site/`.
