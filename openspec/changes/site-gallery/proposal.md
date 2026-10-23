## Why

The landing page shows eleven themes as eleven pictures of different pages, four backgrounds out of sixteen, and every
widget in one theme. Its lists (`themes.json`, `backgrounds.json`, `widgets.json`) copy what the product already says and
have gone stale. After `site-media-pipeline` every theme has a picture of the same page and of every widget size, so the
site can let a visitor choose a theme and see the whole page in it, and can list exactly what the product ships.

## What Changes

- **A floating theme picker.** A small control pinned to the corner of every page lists the built-in themes as swatches.
  Choosing one swaps every deck and widget picture and loop on the page to that theme, and tints the site's accent
  colours with the theme's accent (the page stays dark; the site keeps its fonts). The choice is remembered in the
  browser and can be linked with `?theme=<name>`. Without JavaScript the page shows glass, as today, and the picker is not
  shown.
- **Data from the product.** The pages are built from `site/src/generated/catalog.json` (written by the media pipeline)
  and the media manifest. **BREAKING** (site): `themes.json`, `backgrounds.json` and `widgets.json` are removed; the
  hand-written words the catalog does not have (alt text, the widgets' notes) move to one overrides file.
- **The theme gallery.** One card per theme: its deck, name, description, tags, swatch and the backgrounds it pairs with.
  Choosing a card sets the picker.
- **All sixteen backgrounds,** each with its loop, its description, and "pairs with" chips for the themes it goes with.
  With a theme chosen, the backgrounds that pair with it are marked and its default background comes first.
- **Widgets in the chosen theme,** at every size the media pipeline made.
- **A new theme, background or widget appears** on the site after a media refresh with no site edit, and one without media
  is left out, as today.

Not in this change: the documentation pages (`docs-rewrite`), audience filters on the gallery, the theme's fonts or light
mode on the site, a social preview picture.

## Capabilities

### New Capabilities

- `site-theme-picker`: the floating picker: what it lists, what a choice changes, how it is remembered and linked, and how
  it behaves without script, with reduced motion and with a keyboard.

### Modified Capabilities

- `website`: `Landing Page` (theme gallery, all backgrounds with pairing, widgets in the chosen theme, contents from the
  product's catalog); `Visitor Privacy` (a remembered theme choice is allowed and identifies nobody).

## Impact

- `site/src/pages/index.astro`, `site/src/components/` (a `ThemePicker`, a `ThemeGallery`, `WidgetShowcase` and
  `BackgroundShowcase` reworked), `site/src/layouts/Base.astro` (the picker on every page), `site/src/styles/`,
  `site/src/lib/` (catalog and media lookups, accent contrast), `site/src/data/` (three files removed, one overrides file),
  `site/scripts/check-output.mjs` (catalog coverage).
- No product code changes. Depends on `site-media-pipeline`.
- Docs: `site/README.md`.
