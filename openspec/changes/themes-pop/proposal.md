## Why

Pop is the loud theme: flat saturated colour per card, thick black outlines, hard offset shadows and huge heavy
numerals. It exists only as a test fixture (`tests/UrDeck.Engine.Tests/Fixtures/themes/pop/theme.json`), so no user can
pick it. Every knob it uses is built (card tint cycling, hard shadow via the underlay, thick bar gauge, pill chip, icon
sets, motion level). It also photographs well, so the website gallery gains most from it.

## What Changes

- Pop (light) becomes a built-in theme: `src/UrDeck.Engine/Themes/Builtin/pop.json` (flat layout), listed in
  `ThemeStore.BuiltInNames`, selectable with `"theme": "pop"`.
- One font family is bundled: Archivo (variable file, OFL 1.1) with its licence text and an entry in
  `THIRD-PARTY-NOTICES.md`. Numerals use Archivo at weight 900; Archivo Black is not bundled (spike S3: same advance,
  ink within 1 %).
- `typography.valueScale: 1.35` is dropped from the draft: a value is limited to its fitted size, so it has no effect.
  Large numerals are checked on the panel and tuned with padding and typography values instead.
- Contrast is the main risk (flat saturated tints with ink text, tinted cards keep `Theme.Text`). The draft is run
  through `urdeck-cli theme validate`; the built-in must validate clean (`THEME060` to `THEME062`).
- Goldens for the built-in (page and dock) are redrawn in the real font; the fixture copy of Pop is removed and its
  tests load the built-in. The v1 goldens stay unchanged.
- Website: a sample deck (`site/decks/pop`) with its picture, a `themes.json` entry, `site/src/content/docs/themes.md`,
  `docs/themes.md`, the media checklist and `docs/design/look-and-feel/STATUS.md` (PR 15 row).
- Not in this change: any engine, SDK or host change; the other themes; a theme picker.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `theme`: "Built-in Themes" lists Pop among the shipped themes and requires it to set every colour of its own (it is
  light); "Built-in Theme Metadata" gives its name, mode, pairings and default background.

## Impact

- `src/UrDeck.Engine/Themes/Builtin/` (`pop.json`, `Archivo-Variable.ttf`, `Archivo-OFL.txt`), `ThemeStore.BuiltInNames`.
- `tests/UrDeck.Engine.Tests`: `Fixtures/themes/pop` removed and users repointed; goldens `pop-page.png`, `pop-dock.png`
  redrawn; new built-in, light-colour, catalog and pairing tests.
- `THIRD-PARTY-NOTICES.md`, `docs/themes.md`, `docs/design/look-and-feel/STATUS.md`, `site/` (deck, picture, themes data
  and page, media checklist).
- Runs beside the sibling theme changes (`themes-prism-obsidian`, `themes-ember-blueprint`, `themes-rigline`); they touch
  the same lines of `ThemeStore.BuiltInNames`, `THIRD-PARTY-NOTICES.md`, `GoldenTests.cs`, `docs/themes.md`,
  `site/src/data/themes.json` and the two "Built-in" requirements of `theme`. Rebase on whichever lands first.
