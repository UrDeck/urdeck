## Context

The draft is `tests/UrDeck.Engine.Tests/Fixtures/themes/pop/theme.json`; it drives goldens (`pop-page.png`,
`pop-dock.png`, redrawn when the pill chip landed in `theme-widget-parts`) and tests for the hard shadow
(`CardUnderlayTests`, archived `card-underlay`). The fixture folder holds no font file, so those renders used the
fallback font. Built-ins are embedded resources `UrDeck.Themes.<file>` (the `Themes\Builtin\*.json;*.ttf;*.txt` glob);
`ThemeStore.LoadBuiltIn` merges the file over `default-dark`. The `shapes` and `ripple` backgrounds exist. The template
is the archived change `2026-10-19-themes-carbon-paper`.

## Goals / Non-Goals

**Goals:**
- Pop loads from the engine alone, in Archivo, with no warning and no contrast finding, on Windows and Linux.
- What ships is what the owner approved on the panel; each changed value is recorded with its reason.
- No engine change; anything only the engine can fix becomes a follow-up.

**Non-Goals:**
- Host UI, a theme picker, glyph-exact goldens on Linux.

## Decisions

### 1. Flat file, one font

`pop.json` sits beside the other built-ins; `Archivo-Variable.ttf` and `Archivo-OFL.txt` beside `InterVariable.ttf`.
Source is `spikes/theme-look-s3/fonts/` (untracked). Before committing, compare with the upstream Google Fonts release,
confirm OFL 1.1, and record version and copyright in `THIRD-PARTY-NOTICES.md`. `ArchivoBlack-Regular.ttf` is not
bundled: values use Archivo at `weights.value: 900` (S3: same advance, ink within 1 %). `typography.fonts` still names
the file for value and unit; the variable file serves every role. Not subset.

### 2. valueScale dropped, big numerals checked

A value is limited to its fitted size, so `valueScale: 1.35` draws the same as 1.0 (the same finding as Paper's). It is
removed. "Huge" numerals must come from the fitted size, so the panel look checks them; the levers are `card.padding`,
`unitRatio`, `tracking.value` and weight 900. If the numerals read small, that is a fit question for the engine and is
recorded as a follow-up, not fixed here.

### 3. Contrast

Cards are flat opaque tints from `colors.cardTints` (yellow, sky, pink, lime, blue) with `Theme.Text` (`#111111`) on
them. Ink on blue `#3b5bff` is the weak pair (about 3.4:1 estimated); the other four are well above 4.5:1. The
validator checks text and muted text on the card fill and on each tint, and status colours. Expected fixes, listed as
tasks and decided against the validator's output: lighten the blue tint (for example `#6f8bff`); darken `textMuted`
only if `THEME061` fires; `warning` equals the yellow card fill, so a status word in `warning` on a yellow card would
vanish (`THEME062`), so change `warning` to an orange and check it on every tint. The built-in must validate clean
with `--fail-on-warnings`; the final table goes in the pull request.

### 4. Hard shadow reach

A shadow is limited to the gap between cards when the fill is translucent (card-underlay). Pop's fills are opaque, so
the shadow should be drawn at full reach (`x`, `y` of 0.055 of a unit, blur 0). The tasks check the reach logging in
`urdeck.log` on the panel and that the shadow is not clipped at the page edge or under the dock.

### 5. Light mode and the rest of the draft

Pop merges over `default-dark`, so every v1 colour must be set in `pop.json`; a test compares them (as for Paper).
Background `shapes` (pairs: `shapes`, `ripple`), static colour `#f6f0e0`, `icons.set: "outline"` with tint `text`,
`motion.level: full`. `ThemeStore.BuiltInNames` gains `pop`; `ThemeCatalog` and `PairingIndex` tests cover it. The
chip is the pill from `theme-widget-parts`. The fixture is deleted and tests load the built-in; other drafts stay.

### 6. Goldens, tuning, site

Goldens stay at the harness size (1100x1920), redrawn after tuning; v1 goldens must not change. The owner looks at
Pop on the 1100x3840 panel over `shapes` and over the static colour, at 100 % and 150 % scaling. Every changed value
goes in a tuning table (key, draft, final, reason) in the pull request. Pop photographs well, so the site deck
(Clock, Stats 2x1 thick bars, Weather, Shortcut x4) is rendered with `Render-SiteDecks.ps1` and checked closely.

## Risks / Trade-offs

- [Blue tint fails contrast] -> change the tint or text; the validator decides.
- [Numerals not huge enough without valueScale] -> panel look; follow-up if only the engine can fix it.
- [Sibling changes edit the same files] -> rebase on whichever lands first; resolve list and table entries by union.
- [Variable Archivo weight 900 not honoured by the loader] -> the test for "no font or weight warning" catches it.
- [Hard shadow reach differs from the fixture's golden] -> redraw and look.

## Migration Plan

Additive. Rollback removes `pop` from `BuiltInNames`; a config naming it falls back to the default theme with the usual warning.

## Open Questions

- Which exact tint and text colours pass contrast and still look loud (decided on the panel).
