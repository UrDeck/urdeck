## Context

The drafts are in `tests/UrDeck.Engine.Tests/Fixtures/themes/{prism,obsidian}/theme.json` and drive engine tests and
goldens (`prism-page/dock`, `obsidian-page/dock`, drawn in the fallback font because the fixture folders hold no fonts).
This change mirrors `themes-carbon-paper` (archived 2026-10-19): flat built-in layout, fonts beside the JSON, embedded by
the existing `Themes\Builtin` globs, `ThemeStore.LoadBuiltIn` merges over `default-dark`.

`urdeck-cli theme validate` on the drafts today reports: Prism `THEME020` (font not in folder, expected for a fixture; the
built-in has it) and `THEME054` (translucent cards, the shadow reaches 25 px of its 193 px); Obsidian four `THEME020`
(same reason). The contrast rules (THEME060 to THEME062) are re-run once the fonts are in place. Every background the
drafts name exists in `src/UrDeck.Engine/Backgrounds/Builtin` (nebula, vortex, horizon, spectrum, ripple, warpgate).

## Goals / Non-Goals

**Goals:** both themes load from the engine alone in their own fonts, validate clean, and ship as looked at on the panel,
with every changed number recorded. **Non-Goals:** host UI; engine changes (a finding only the engine can fix becomes a
follow-up); glyph-exact goldens on Linux.

## Decisions

### 1. Flat files and fonts

`prism.json` and `obsidian.json` next to `default-dark.json`; `BricolageGrotesque-Variable.ttf`,
`BodoniModa-Variable.ttf`, `Jost-Variable.ttf` and their `-OFL.txt` files next to `InterVariable.ttf`. Sources:
`spikes/theme-look-s3/fonts/` (untracked). Before committing, compare each file with its upstream release (Google Fonts),
confirm the licence text is OFL 1.1, and record version and copyright line in `THIRD-PARTY-NOTICES.md`. Not subset.
A folder per theme was rejected in carbon-paper for the same reason: it needs a loader change and nothing else gains.

### 2. Built-ins replace the fixtures

Delete `Fixtures/themes/prism` and `obsidian`; tests that used them load the built-ins; assertions moved by tuning are
updated with the reason. The other four drafts stay fixtures until their changes land.

### 3. Goldens at the harness size

`prism-page/dock` and `obsidian-page/dock` are redrawn from the built-ins at 1100x1920 (real fonts change them); the
3840 picture is the site deck. v1, Carbon and Paper goldens must not change.

### 4. `valueScale` above 1 is dropped

Prism 1.15 and Obsidian 1.25 have no effect (a value is limited to its fitted size). If the panel shows Obsidian's Didone
numerals too small for the look, that is an engine fit question, recorded as a follow-up, not a lever here.

### 5. Prism's icons

Keep `icons.set: "outline"` with tint `text` (`animated` no longer exists). Measured: outline about 0 % CPU and GPU with
`motion: full`, Meteocons 2 to 3 % and 16 to 20 MB more (`docs/perf/theme-look.md`, `docs/perf/weather.md`). Dropping
`set` (Meteocons) is an option for the owner at the panel look; if chosen, it is recorded with a fresh measurement.
Obsidian keeps `outline` with accent tint.

### 6. Prism's paint cost and shadow

Prism paints at 1.83x `default-dark` (budget 1.5x), accepted by the owner (`docs/perf/card-chrome.md`), and its limited
shadow was approved on the panel. Real fonts or tuning may widen the gap, so cost is re-measured with the same harness and
recorded; a regression beyond the accepted figure goes back to the owner. `THEME054` warns that the translucent cards
limit the shadow. Because built-ins must validate clean, shrink blur, offset or spread to what the gap allows (the
approved look is the 25 px limit, so matching values should not change what is drawn). If that does change the look, or
the validator offers no way to acknowledge the limit, record it and raise it with the owner; a lint exemption belongs to
`theme-lint`, not to this change.

### 7. Lint and contrast

After the fonts are in place, run `theme validate` on both. Likely findings: THEME060 to THEME062 on Prism's `textMuted`
(`#ccffffff`) and status colours over translucent cards, and on Obsidian's `critical` (`#d9715b`); Obsidian's
`textMuted` `#b79a5e` on `#15120e` is expected to pass. Fixes are made in the theme values and recorded in the contrast
table in the PR. Built-ins must validate clean.

### 8. Tuning on the panel

The owner looks at each theme on the 1100x3840 panel over its default background and its static gradient, at 100 % and
150 % scaling. To look at: Prism's thick frost and sheen over a bright `spectrum` frame, the specular top edge, light wide
numerals (weight 300) over moving backgrounds; Obsidian's champagne gradient border and hairline divider, the diamond
marker and halo, tracking 0.28 on upper-case labels, circular dock buttons, scrim alpha 0.2 over `nebula`. Each changed
value goes into a table (key, draft, final, reason) in the PR.

### 9. Site and docs

Decks follow `06-themes.md`; pictures come from `scripts/Render-SiteDecks.ps1`. `themes.json` gains two entries; both
themes pages list the built-ins with look, fonts, default background and the config snippet.

## Risks / Trade-offs

- [Real fonts change fit: Bodoni Moda numerals are high-contrast and wide] -> the panel look judges; tracking first.
- [Prism cost grows beyond 1.83x] -> re-measure; the owner decides.
- [A lint finding conflicts with an approved look (THEME054)] -> see decision 6.
- [Textual conflicts with the sibling theme changes] -> rebase on whichever lands first; keep edits to the shared lines
  (`ThemeStore.BuiltInNames`, `THIRD-PARTY-NOTICES.md`, `GoldenTests.cs`, `docs/themes.md`, `themes.json`) additive, and
  resolve by keeping every theme's entries.
- [Large font files grow the engine] -> accepted; installer size is not a concern.

## Migration Plan

Additive. Rollback: remove the two names from `BuiltInNames`; a config naming them falls back to the default theme with
the usual warning.

## Open Questions

- Prism: outline or Meteocons (panel look).
- Whether pairings or default backgrounds change after the panel look.
