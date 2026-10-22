## Why

The theme engine now draws everything Ember and Blueprint ask for (card chrome, chamfers, dashed borders, corner marks, tach
and marker gauges, the pattern layer, outline icons, motion levels), but both exist only as test fixtures, so no user can pick
them. Carbon and Paper shipped the same way (`themes-carbon-paper`); this is the next pair in the order of
`docs/design/look-and-feel/STATUS.md` (PR 14). Ember is also the first built-in with a glow on its numerals and gauges, so
shipping it measures that cost on the panel.

## What Changes

- Ember (dark) and Blueprint (dark) become built-in themes: `src/UrDeck.Engine/Themes/Builtin/ember.json` and
  `blueprint.json` (flat layout), listed in `ThemeStore.BuiltInNames`, selectable with `"theme": "ember"` or
  `"theme": "blueprint"`.
- Two font files are bundled: Barlow Condensed Bold (Ember) and IBM Plex Mono Regular (Blueprint), with their OFL
  texts and entries in `THIRD-PARTY-NOTICES.md`. Blueprint's title is weight 400: `typography.fonts` has no title role, so a Medium file could not be named.
- The drafts are corrected where they are known to be wrong: Ember's tick colour `#ffffff1c` (read as AARRGGBB, so opaque
  yellow) becomes `#1cffffff`; `valueScale` above 1 is dropped (no effect); Ember's body weight 500 (a Bold-only file, one
  warning on load) is set to the weight the file has; extra readout room below the middle for an arc with ticks and an
  inside label; contrast fixes (`THEME060` to `THEME062`) found by `urdeck-cli theme validate`.
- Tuned on the 1100x3840 panel at 100 % and 150 %; the final values and reasons are recorded. Ember's glow cost is measured
  and accepted or reduced.
- Goldens for both built-ins (page and dock), redrawn in the real fonts and with the pattern layer, outline icons and
  motion levels. The v1 goldens and the other drafts' goldens stay unchanged. The fixture copies of Ember and Blueprint are
  removed and their tests load the built-ins.
- Website: a sample deck and picture per theme, `themes.json` entries, `docs/themes.md`, `site/src/content/docs/themes.md`,
  `site/MEDIA-CHECKLIST.md`, and `docs/design/look-and-feel/STATUS.md` (PR 14 row).
- Not in this change: engine changes. A slanted value leaning past its rectangle (Ember, about 50 px on a 4x2 clock) is
  checked on the panel and recorded as a follow-up if it needs the fit to count the lean.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `theme`: "Built-in Themes" lists Ember and Blueprint among the shipped themes (with their fonts, no warning on load,
  motion level); "Built-in Theme Metadata" gives their names, modes, pairings and default backgrounds.

## Impact

- `src/UrDeck.Engine/Themes/Builtin/` (two theme files, three font files, two licence texts), `ThemeStore.BuiltInNames`.
- Engine size grows by about 0.5 MB of fonts (to be measured); fonts are opened only for a theme that names them.
- `tests/UrDeck.Engine.Tests`: goldens `ember-page`, `ember-dock` (new), `blueprint-page`, `blueprint-dock` redrawn;
  `Fixtures/themes/ember` and `blueprint` removed and their users repointed.
- `THIRD-PARTY-NOTICES.md`, `docs/themes.md`, `docs/design/look-and-feel/STATUS.md`, `docs/perf/theme-look.md`.
- `site/`: `decks/ember`, `decks/blueprint`, `src/data/themes.json`, `src/assets/decks/`, `src/content/docs/themes.md`.
- Runs beside three sibling theme changes; shared files (`ThemeStore.BuiltInNames`, `THIRD-PARTY-NOTICES.md`,
  `GoldenTests.cs`, `docs/themes.md`, `site/src/data/themes.json`) are rebased on whichever lands first.
