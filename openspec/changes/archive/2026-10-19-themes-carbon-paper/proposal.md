## Why

The theme engine can now draw everything Carbon and Paper ask for (card chrome, compact cards, typography, gauges,
widget parts), but the eight new looks exist only as test fixtures, so no user can pick one. Carbon and Paper are the
cheapest pair: neither animates, both use the same two font families, and Paper needs nothing that is not built. Shipping
them now also tunes the engine against real themes on the panel, where earlier changes kept finding surprises.

## What Changes

- Carbon (dark) and Paper (light) become built-in themes: `src/UrDeck.Engine/Themes/Builtin/carbon.json` and
  `paper.json` (the flat layout), listed in `ThemeStore.BuiltInNames`, selectable with `"theme": "carbon"` or
  `"theme": "paper"` in `urdeck-config.json`.
- Two font families are bundled with the built-ins: Instrument Sans and JetBrains Mono (variable files), with their OFL
  texts and entries in `THIRD-PARTY-NOTICES.md`. The fixture copies of the two themes have no font files, so their
  existing goldens were drawn in the fallback font; the built-ins are the first renders in the real typefaces.
- The drafts are tuned on the 1100x3840 panel; final values and the reason for each change are recorded. Three open
  points are settled: Paper's `valueScale: 1.1` (no effect, since a value is limited to its fitted size), whether
  Carbon's bar turns amber under load or only its status word does, and whether Paper's vermilion accent is ever drawn as
  text (about 3.6:1 on its card).
- Goldens for both built-ins (page and dock); the v1 goldens (`default-dark`, `default-light`, `glass`) stay unchanged.
  The fixture copies of Carbon and Paper are removed and the tests that used them load the built-ins.
- Carbon keeps its `pattern` value, which draws nothing until the `page-pattern` change; its `icons` values and Paper's
  draw nothing until `theme-icons` (Weather keeps its own icons).
- Website: a sample deck for each theme (`site/decks/carbon`, `site/decks/paper`) with its picture, the themes page lists
  both with the config snippet, and `docs/themes.md` and `docs/design/look-and-feel/STATUS.md` are updated.
- Not in this change: the pattern layer, themed icons, any theme picker (the configuration editor's approach is not
  decided; nothing here adds host UI), and `urdeck-cli theme validate` with its contrast rule (built in parallel as
  `theme-cli`). The contrast of both themes is checked by hand and recorded in the pull request.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `components`: "Gauge Readout Gap" (new `gauge.readoutGap` theme key, SDK 0.12.0), added during panel tuning because Carbon's values sat tight on the bar.
- `theme`: "Built-in Themes" lists Carbon and Paper among the shipped themes, and the built-ins may bundle font files;
  "Built-in Theme Metadata" gives their names, modes, pairings and default background.

## Impact

- `src/UrDeck.Engine/Themes/Builtin/` (two theme files, two font files, two licence texts; picked up by the existing
  `EmbeddedResource` globs), `ThemeStore.BuiltInNames`. No change to the theme resolver, the card painters or the SDK.
- Engine size grows by about 380 KB of fonts; memory grows only when a theme using them is loaded.
- `tests/UrDeck.Engine.Tests`: goldens for `carbon` and `paper` move from the drafts to the built-ins and are redrawn in
  the real fonts; `Fixtures/themes/carbon` and `paper` are removed and their users repointed.
- `THIRD-PARTY-NOTICES.md`, `docs/themes.md`, `docs/design/look-and-feel/STATUS.md`.
- `site/`: `decks/carbon`, `decks/paper`, `src/data/themes.json`, `src/assets/decks/`, `src/content/docs/themes.md`.
- Runs beside `theme-cli` (another worktree): this change does not edit `ThemeDefinition.Rules.cs` or the sample page.
