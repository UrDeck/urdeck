## Why

The eight new themes each come with a background that suits them. Today a background is something only the user's
configuration can choose, and a configuration without one shows a plain colour. So picking a theme gives the card
look and none of the atmosphere, and nothing in the product knows that Carbon wants contour lines and Rigline wants a
synthwave horizon. A settings editor will need exactly that knowledge to offer good picks.

This change lets a theme choose its own background, lets a background say which themes suit it, and gives the engine
the small services an editor needs to list both and recommend one for the other. It lands before the new shaders and
the new themes so that both can use it, and it changes nothing for anyone whose theme defines no background.

## What Changes

- **BREAKING (for configurations without `background`, in effect only for themes that define a background):** a
  configuration with no `background`, or with `null`, no longer means "no background". It means "the theme's choice":
  the theme's default background if it has one, else its still background, else its background colour. The three
  built-in themes define no background, so every existing deck looks the same. A user who picks a theme that has one
  gets it, and removes it with the new `"background": { "type": "none" }`.
- A theme can name a default shader (`background.default.name`), a still background (`background.static`: a gradient,
  linear or radial; a colour; or a picture file inside the theme folder, scaled to cover) and a veil between background
  and cards (`background.scrim`, a colour and an alpha, for text contrast over a busy shader).
- One precedence, in one place (`BackgroundPlan`): the user's `background`, including `none`; then the theme's default;
  then the theme's still background; then the plain background colour. A shader or picture that fails falls through the
  same list instead of straight to the colour.
- A background's manifest may name themes it suits (`pairsWith.themes`, best first); a theme may name backgrounds it
  suits (`meta.pairsWith.backgrounds`). A `PairingIndex` joins both sides: a pair is listed if either side names the
  other, and `Recommend(theme, backgrounds, limit)` returns a short, deterministic list with the reason for each pick.
- `ThemeCatalog` and `BackgroundCatalog` list what exists (names, descriptions, mode, swatch colours, samples, parameters)
  without loading fonts or compiling shaders, for the future editor and for the command line tool. There is no UI in
  this change.
- The log says which background was chosen and why (`shader 'horizon' (theme default)`), once per change.
- The three built-in themes gain `meta` (name, description, mode) and no background; the schema of the background
  manifest gains the optional `pairsWith`.

Not in this change: the twelve new shaders (`builtin-shaders`), the themes that use all this, motion policy (the
theme's `motion.level` does not touch the background; the shader keeps its own controls), and any settings UI.

## Capabilities

### New Capabilities

- `background-pairing`: how themes and backgrounds recommend each other, the index that joins the two sides, the
  recommendation order, and the catalogs the editor will read.

### Modified Capabilities

- `backgrounds`: "Background Setting" (absent means the theme's choice; `none`), "Background Failures" (the fallback
  list), "Background In Snapshots". New requirements "Theme Backgrounds" and "Background Scrim".
- `background-manifest`: "Manifest Content" (the optional `pairsWith`).
- `theme`: new requirement "Built-in Theme Metadata".

## Impact

- `src/UrDeck.Engine/Backgrounds`: new `BackgroundPlan`; `DeckBackground` takes a plan and draws a still gradient,
  colour or picture and the scrim; `BackgroundManifest` and its reader gain `PairsWith`; new `PairingIndex`,
  `BackgroundCatalog`; `ThemeCatalog` in `Themes`.
- `src/UrDeck.Host/BackgroundLayer.cs`: reads the plan; the layer's request key includes where the choice came from, so
  a theme change that changes the resolved background restarts it and an unrelated save does not.
- `src/UrDeck.Engine/Config`: the `background` object accepts `none`; absent and `null` read as "no choice".
- `src/UrDeck.Engine/Themes/Builtin/*.json`: `meta` for the three built-ins.
- `docs/backgrounds.md` and `docs/themes.md`, `docs/schemas/v1/shader-manifest.json`, `site/src/content/docs/` (the
  background and theme pages; AGENTS.md asks for both), `AGENTS.md` (the background paragraph).
- Tests in `tests/UrDeck.Engine.Tests`; a plan table, a pairing table, snapshots of a still gradient and a scrim. The
  engine coverage floor still applies.
- Depends on `theme-format-2` (the `background` and `meta` values it reads).
