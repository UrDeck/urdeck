## Why

User documentation lives in two places that disagree in depth: `docs/themes.md` (704 lines) and `docs/backgrounds.md` (609
lines) hold the full reference, while the site's pages (`site/src/content/docs/`, 572 lines in all) are shorter
summaries. The reference tables are typed by hand from the schema and the engine, so they drift whenever a key, a
setting or a finding code is added. The design package asked for generated reference tables, a theme authoring cookbook
and a check that every built-in is documented (`docs/design/look-and-feel/08-cli-validation-docs-site.md` §3 and §5).
With the catalog and the media pipeline in place, the site can be the one home of user documentation, built from the
product.

## What Changes

- **The site is the user documentation.** The user-facing content of `docs/themes.md` and `docs/backgrounds.md` moves to
  the site and is rewritten there (quick start, reference, fonts and licences, icon sets, backgrounds and pairing, motion,
  checking a theme, troubleshooting). `docs/` keeps contributor and design notes; `docs/themes.md` and
  `docs/backgrounds.md` become short contributor pages (how built-ins are made and tested) that link to the site.
  **BREAKING** (docs): links to the moved sections change; the README, `CONTRIBUTING.md`, `AGENTS.md` and the code comments
  that point at them are updated.
- **Generated reference.** The theme format and background manifest tables are built from the published schemas; the
  finding codes tables, the built-in lists and each widget's settings table are built from the product's catalog. The
  site's build fails when a schema key, a setting or a finding code has no description, or when a built-in theme,
  background or widget is missing from the docs.
- **Pictures in the docs follow the picker.** Widget pages show the widget at each size, themes and backgrounds show their
  pictures, all from the media manifest and in the chosen theme.
- **A theme authoring cookbook.** Recipes (chamfered cards, gradient border, hard shadow, LED bars, tach with redline,
  dotted page, circular dock, custom icons), each a small theme folder in the repository whose JSON the page shows as it
  is and whose picture the media pipeline renders. CI validates the recipes.

Not in this change: a docs search, translated docs, an API reference for the SDK.

## Capabilities

### New Capabilities

<!-- none -->

### Modified Capabilities

- `website`: `Configuration Pages` becomes the one home of user documentation with generated reference and completeness
  checks; a new requirement for the cookbook.
- `site-media`: the media matrix includes a picture per cookbook recipe.

## Impact

- `site/src/content/docs/` rewritten and extended (themes reference, backgrounds reference, cookbook pages, troubleshooting);
  `site/src/components/` (reference table components fed by the schemas and the catalog); `site/cookbook/<recipe>/`
  (theme folders and a `recipe.json`); `site/scripts/check-output.mjs` (completeness); the media generator (cookbook rows).
- `docs/themes.md`, `docs/backgrounds.md` shortened to contributor pages; links in `README.md`, `CONTRIBUTING.md`,
  `AGENTS.md`, `docs/BACKLOG.md` and code comments.
- `docs/schemas/v2/theme.json` and `v1/shader-manifest.json`: descriptions for every key that has none (about 50 in the
  theme schema).
- `ci.yml`: the recipes validated with `urdeck-cli theme validate` in `test-linux`.
- Depends on `preview-renderer` (the catalog's widget settings and finding codes), `site-media-pipeline` and `site-gallery`
  (slots and the picker).
