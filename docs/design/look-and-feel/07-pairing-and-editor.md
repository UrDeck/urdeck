# 07 Pairing, defaults and the config editor

## 1. The pairing construct (Pat's request)

> "Either the backgrounds or themes having a 'pairs well with' construct, so when we have our config editor UI users can get some recommended picks for backgrounds and stuff depending on the theme they choose."

### Data model (both directions, one rule)
- **Theme side:** `meta.pairsWith.backgrounds: string[]` in `theme.json`, best first.
- **Background side:** `pairsWith.themes: string[]` in the background's `manifest.json`, best first. (Unknown manifest key today: PR 5 makes it known.)
- **Closure rule (what the engine computes, in `PairingIndex`):** a pair (theme T, background B) is *listed* if **either** side names the other. It is *mutual* if both do.
  The shipped data is already symmetric (a test asserts it for built-ins), so mutual = listed for built-ins. User themes and user backgrounds will usually be one-sided; that is
  why the closure exists: a user's own theme can list built-in backgrounds, and a downloaded background can list built-in themes.
- **Theme default:** `background.default.name` is the single background the theme selects when the user has not chosen one; it must also be in `pairsWith` (validator warns if not).

### Recommendation ranking (`PairingIndex.Recommend(theme, backgrounds, limit)`)
Returns `(Background, Reason)` in this order; stop at `limit` (editor shows 3 to 5):
1. the theme's `background.default` (Reason: `ThemeDefault`)
2. mutual pairs, in the theme's own order (Reason: `Mutual`)
3. pairs listed by the theme only, in the theme's order (`ThemeListed`)
4. pairs listed by the background only, in the background's own tag-order (`BackgroundListed`)
5. tag affinity: background tags that share at least one tag with the theme's `meta.tags`, **only for backgrounds with no pairing data at all** (`TagMatch`)
6. nothing else: the editor shows "Browse all" instead of a fake recommendation.
Deterministic, no randomness, no scoring weights to tune. Unit-test every rung.

### Current pairing table (generated)

`D` = theme default, `●` = mutual pair.

| Theme \ Background | horizon | spectrum | contour | nebula | streams | ripple | embers | plotter | shapes | warpgate | equalizer | vortex |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| carbon |  |  | **D** | ● | ● |  |  | ● |  | ● |  |  |
| rigline | **D** | ● |  |  | ● |  | ● |  |  | ● | ● | ● |
| prism | ● | ● |  | **D** |  | ● |  |  |  |  |  | ● |
| paper |  |  | **D** |  |  | ● |  | ● | ● |  |  |  |
| ember |  |  |  |  |  |  | **D** |  |  | ● | ● |  |
| blueprint |  |  | ● |  | ● |  |  | **D** |  |  |  |  |
| pop |  |  |  |  |  | ● |  |  | **D** |  |  |  |
| obsidian |  |  |  | **D** |  |  |  |  |  | ● |  | ● |

Every theme lists two to seven backgrounds and every background pairs with at least two themes. Pop and Shapes are deliberately narrow; a recommendation list should be short and opinionated, not "everything".

## 2. Background precedence (restated; canonical text in `02 §4.11`)

1. `urdeck-config.json` `background` (a user choice, including an explicit `"none"`),
2. theme `background.default` (name + `parameters` + `colors`),
3. theme `background.static` (image, gradient or colour),
4. `colors.background` as a plain colour.

Details that bite:
- **Absence changes meaning (decision D7).** Today a config with no `background` shows the plain theme colour. After PR 5 it shows the theme's default
  background if the theme has one. Built-in `default-dark`, `default-light` and `glass` have none (until the optional `aurora` default), so **existing users see no change**; users who
  *pick a new theme* get its background. Add `"background": {"type": "none"}` (new, explicit opt-out) for people who want the plain colour.
- A user choosing a *different* shader keeps the theme palette (`colors.backgroundPalette`) unless they set `background.colors`; theme-default `colors`/`parameters` apply **only** to the theme default.
- `motion.level == off` (effective) swaps a shader for `static`; a `static` of kind `none` falls to the plain colour.
- A shader that fails to compile falls back through steps 3 and 4 (today: step 4) and logs once.

## 3. What the config editor will need from the engine (so you build the right seams now)

The editor is not part of this work, but it will ask for these; building them as small, testable engine services now avoids rework.

| Need | Seam (new unless noted) |
|---|---|
| List themes with name, `meta`, mode, colours for swatches, sample images | `ThemeCatalog.List()` returning `ThemeInfo` (built-in + user; cheap, no fonts loaded) |
| List backgrounds with manifest, parameters, samples | `BackgroundCatalog.List()` (exists in part for validation) |
| Recommend backgrounds for a theme | `PairingIndex.Recommend` (above) |
| Live preview of *theme + background + sample page* without touching the running deck | `PageRenderer` with a throwaway `LoadedTheme` and a `DeckBackground` at a chosen time; a `PreviewRenderer.Render(themeName, backgroundSpec, size, time)` returning `SKBitmap`/PNG. The same path the CLI `theme render` uses |
| Edit a background's parameters | manifest `parameters` (label, description, min, max, step, kind, unit, hidden) already exists; the editor builds sliders/colour pickers from it |
| Validate while typing | `ThemeLint` / `ThemeValidator` returning `Finding[]` with codes (PR 11) |
| Apply | write `urdeck-config.json` `theme` and optionally `background`; "Use theme's background" = **remove** the `background` key (so precedence step 2 applies) |
| Reset | remove keys; never write defaults back |
| Reduce motion | config `reduceMotion` and a read of the effective `MotionPolicy` for a status line |

**Sample page for previews:** a fixed, deterministic page used by golden tests, the CLI and the editor: Clock (top, 4x1), Stats (2x1 CPU/GPU), Weather (4x2),
four Shortcuts. Define it once as a JSON fixture `tests/UrDeck.Engine.Tests/Fixtures/sample-page.json` with fixed readings and time (13 Oct, 21:47:32; CPU 68 %, GPU 82 %;
18 °C partly cloudy; forecast 21/17/14/16/19) so every picture in the mocks is reproducible.

## 4. Recommended copy for the editor (optional)

"Recommended for Carbon" row with the reason chip: *Theme default*, *Pairs well*, *Matches style*. Never claim "best"; the shipped data is taste, not measurement.
