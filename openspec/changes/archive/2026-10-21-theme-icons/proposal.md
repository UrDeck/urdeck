## Why

A theme can already say `icons.set`, `icons.stroke`, `icons.tint` and `icons.glow`, but nothing reads them and no icon set exists
(`THEME044` tells authors to remove the key). Carbon and Paper were built without their `"icons": { "set": "outline" }`
for that reason, so their weather card still shows the full-colour animated Meteocons that the design board draws as
outline glyphs. Shortcuts have no way to show a glyph either, and a shortcut whose real icon cannot be found falls back to a
bare letter. This is PR 10 of the look-and-feel package, and every remaining theme needs it.

## What Changes

- **Icon sets.** A set is a folder with a mandatory `manifest.json` (version 1: `name`, `source`, `license`, an optional
  upstream `version`, and a `keys` table of canonical key to relative SVG path) beside the SVGs. A folder without a manifest is
  not loaded and the log names the file to make. The `<set>/<group>/<key>.svg` naming convention is not a lookup rule; the
  manifest is the only source of truth, so files keep their upstream names and the manifest records where each came from.
- **Built-in `outline` set**, embedded in the engine: Lucide (ISC, with the MIT-licensed Feather parts) at a pinned release,
  copied once, with the licence text in the set. It covers the 13 weather keys and all 24 `app/` keys of
  `docs/design/look-and-feel/04-widgets-and-icons.md`; `ui/` is not shipped. Keys keep the design's own names (`home`,
  `bolt`, `thunderstorms-day`), so any other set can fill them. The seven seed SVGs from the design handoff are replaced,
  because their source is unknown.
- **Theme-folder sets.** `icons.set` also names a folder `icons/<set>/` inside a theme folder, loaded the same way. The
  resolution chain is the theme's set, then `outline`, then the widget's own icon.
- **Vector drawing.** The engine parses only `path`, `circle`, `ellipse`, `line`, `polyline`, `polygon` and `rect` (no SVG
  library) into paths once per set; stroke width, tint, `fill="currentColor"` shapes and glow are applied when drawing.
- **SDK** (0.13.0): `IIconSet`, `IconGlyph`, `IconTheme` on `Theme.Icons` (the shared resolved set, `Active` when the theme
  names a set, stroke, tint, glow), and `ThemedIcon.Draw(canvas, theme, rect, key, tint?, rampPosition)`.
  `ImageTile.Draw` gains the glyph to show in place of its placeholder. Breaking SDK changes are allowed; the product has not shipped.
- **Weather.** A theme with `Icons.Active` draws static vector icons for the current condition and each forecast day, tinted
  by `icons.tint` (`ramp` maps the temperature from -10 to 40 °C along `colors.ramp`), with the optional glow. The widget
  then loads no Lottie animation, caches no poster bitmaps and asks for no animation repaints. A theme with no set
  draws the Meteocons exactly as before.
- **Shortcut.** `Icon: "theme:<key>"` draws a theme glyph (tinted, never the real icon); a shortcut whose icon cannot be
  found, once the icon service has given up, draws a glyph chosen by target kind (`link`, `folder`, `file`) instead of the
  letter placeholder. A real app, file or site icon is unchanged and never tinted.
- **Lint and CLI.** `THEME044` checks `icons.set` against the built-in and theme-folder sets. New findings: manifest missing or
  malformed, entry points to a missing file or outside the folder, SVG breaks the format rules (4 KB, `viewBox`, element
  whitelist, no `transform`), manifest key outside the canonical lists, missing licence, and a warning for an `.svg` in the set
  folder that no manifest entry uses.
- **BREAKING (theme format 2): `icons.animated` is removed.** Set icons are static, so the key can no longer change
  anything. It leaves the definition, the JSON schema, `docs/themes.md` and every fixture. Prism's draft sets it to `true` with
  `set: "outline"`, which already contradicted itself; its theme change decides between outline icons and Meteocons.
- Carbon and Paper get `"icons": { "set": "outline" }` back and their goldens are redrawn. The other themes and the three v1
  themes stay byte-identical.

## Capabilities

### New Capabilities
- `icon-sets`: the manifest format, how sets are found, parsed and chained, the SVG format rules and the built-in `outline` set.

### Modified Capabilities
- `theme`: `icons.*` values are read and resolved into `Theme.Icons`; `icons.animated` is removed.
- `theme-lint`: `THEME044` becomes a real check, and the manifest and SVG findings are added.
- `widget-sdk`: `IconTheme`, `IIconSet`, `IconGlyph` and `ThemedIcon`.
- `components`: `ImageTile` takes a glyph for its placeholder.
- `widget-weather`: set mode draws static themed icons; Meteocons mode is unchanged.
- `widget-shortcut`: `theme:<key>` icons and the glyph fallback.

## Impact

- Code: `sdk/UrDeck.Sdk` (Theme, new icon types, ImageTile), `src/UrDeck.Engine` (new `Icons` set loader and parser, `ThemeResolver`,
  `ThemeFormat2.Icons`, `ThemeLint`), `widgets/UrDeck.Widgets.Weather`, `widgets/UrDeck.Widgets.Shortcut`,
  `carbon.json` and `paper.json`, the theme fixtures.
- Docs: `docs/themes.md`, `docs/schemas/v2/theme.json`, the site's theme and widget pages, `docs/design/look-and-feel/STATUS.md`,
  a perf note for static versus Lottie weather (`docs/perf/weather.md`).
- Dependencies: none added. Lucide is copied, not referenced.
- Goldens: Carbon and Paper redrawn (weather and shortcut cards on the sample page).
- Not in this change: per-theme art (PR 10b), the `ui/` keys, a card-level switch back to animated weather, and `Theme.Motion`.
