## Why

Carbon and Paper ship; the other six themes exist only as test fixtures. Prism (dark glass) and Obsidian (warm black,
Didone numerals) are next in the order of `docs/design/look-and-feel/STATUS.md` (PR 13): everything they use is built
(card chrome, highlight, sheen, limited shadow, compact circle cards, marker gauge, gradient divider, icon sets, motion
level), and each needs only fonts that are already downloaded. Shipping them also gives the glass look a proper successor
to `glass` and tunes the engine against two more real themes on the panel.

## What Changes

- Prism and Obsidian become built-in themes: `src/UrDeck.Engine/Themes/Builtin/prism.json` and `obsidian.json` (flat
  layout), listed in `ThemeStore.BuiltInNames`, selectable with `"theme": "prism"` or `"theme": "obsidian"`.
- Three font families are bundled: Bricolage Grotesque (Prism), Bodoni Moda and Jost (Obsidian), variable files with
  their OFL texts and entries in `THIRD-PARTY-NOTICES.md`. The fixture copies have no font files, so their goldens were
  drawn in the fallback font; the built-ins are the first renders in the real typefaces.
- `typography.valueScale` above 1 is dropped from both drafts (Prism 1.15, Obsidian 1.25): it has no effect, a value is
  limited to its fitted size.
- Prism keeps `icons.set: "outline"` (static glyphs, about 0 % CPU); Meteocons (drop `set`) stays a panel-look option.
- Drafts are tuned on the 1100x3840 panel at 100 % and 150 %; each changed value is recorded with its reason. Contrast
  and lint findings on the drafts (THEME060 to THEME062, THEME054) are fixed so both built-ins validate clean.
- Goldens for both built-ins (page and dock); v1 goldens and the other drafts' goldens stay unchanged. The two fixture
  copies are removed and their users repointed.
- Website: sample decks `site/decks/prism` and `site/decks/obsidian` with pictures, `themes.json` entries, both themes
  pages, `MEDIA-CHECKLIST.md`, and `STATUS.md` (PR 13 row).
- Not in this change: any host UI for picking a theme, the pattern layer, the other four themes (sibling changes).

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `theme`: "Built-in Themes" lists Prism and Obsidian among the shipped themes; "Built-in Theme Metadata" gives their
  names, modes, pairings and default background.

## Impact

- `src/UrDeck.Engine/Themes/Builtin/` (two theme files, three font files, three licence texts, picked up by the existing
  `EmbeddedResource` globs), `ThemeStore.BuiltInNames`. No change to the resolver, painters or SDK expected.
- Engine size grows by the fonts; memory grows only when a theme using them loads.
- Prism paints at 1.83x `default-dark` (already accepted by the owner, `docs/perf/card-chrome.md`); real fonts and any
  tuning may widen the gap, so it is re-measured.
- `tests/UrDeck.Engine.Tests`: goldens `prism-*` and `obsidian-*` move from the drafts to the built-ins;
  `Fixtures/themes/prism` and `obsidian` are removed and their users repointed.
- `THIRD-PARTY-NOTICES.md`, `docs/themes.md`, `docs/design/look-and-feel/STATUS.md`, `site/` (`decks/`, `src/data/themes.json`,
  `src/assets/decks/`, `src/content/docs/themes.md`, `MEDIA-CHECKLIST.md`).
- Sibling changes (the other four themes) are written in parallel and touch the same lines in `ThemeStore.BuiltInNames`,
  `THIRD-PARTY-NOTICES.md`, `GoldenTests.cs`, `docs/themes.md` and `site/src/data/themes.json`: expect textual
  conflicts and rebase on whichever lands first.
