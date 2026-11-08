## Why

Choosing a theme and a background is the most visible change a user makes, and today it means typing a theme name and a
`background` object into the file. The engine already has the read-only services for an editor (`ThemeCatalog`,
`BackgroundCatalog`, `PairingIndex`; kept for exactly this, see the product-direction note of 2026-10-19). This change
adds the editor's Theme and background section (`docs/handoff/2026-11-06-editor-design.md`, artboard 2;
`docs/handoff/editor-design/ThemeBackground.dc.html`) and the theme quick-switch in the Pages header.

Found while designing it: the mock-up's "Dim behind cards" slider has no setting behind it. The scrim is a theme value
(`background.scrim` in the theme file); the configuration cannot change it. And the deck-wide motion level (`motion`)
has no home in the editor yet.

**Order:** after `editor-forms` (shader parameters are a generated form). Independent of `editor-reading-picker`,
`editor-arrange` and `editor-states`.
**Model:** Sonnet.

## What Changes

- **Theme and background section** (navigation, after Pages):
  - **Theme**: a grid of pictures of the user's own current page drawn in each installed theme (built-in and the user's
    theme folders), rendered in the app and cached, with the theme's swatch shown until the picture is ready; the
    current theme marked. "Your own theme folder…" opens the themes folder in Explorer with a line on how to add one.
  - **Background**: three choices first: **Theme's choice** (no `background` key: what the theme suggests, named),
    **None**, **Picture…**; then "Suggested for <theme>" (the theme's default first, then the backgrounds whose manifest
    pairs with the theme, from `PairingIndex`) and "All backgrounds", as tiles with each background's sample picture.
  - **Options pane** beside them: a live preview (the user's page, drawn by a preview session), **Motion** (Moving or
    Still; Still is `speed` 0), the shader's parameters as a generated form from its manifest with Reset, and **Dim
    behind cards**. A picture shows a drop area, then its file with Change….
- **Dim behind cards** becomes a configuration setting: `background.dim`, 0 to 0.7, replacing the alpha of the theme's
  scrim (its colour stays; black when the theme has none). Absent = the theme's own scrim.
- **Motion** (the deck-wide motion level: follow Windows, full, reduced, off) in this section.
- **Theme quick-switch** in the Pages header: a drop-down of theme names with swatches and "All themes and
  backgrounds…".
- Changing the theme while the background is "Theme's choice" changes the background with it, as the deck already does.

## Capabilities

### New Capabilities

- `editor-theme-background`: the Theme and background section, its preview and options, the theme quick-switch.

### Modified Capabilities

- `backgrounds`: "Background Scrim" can be overridden by the configuration's `background.dim`.

## Impact

- `src/UrDeck.Engine/Backgrounds/BackgroundSettings.cs` (`dim` as an own key, 0–0.7), `DeckBackground.PaintAtTime`
  (scrim alpha), `RequestKey` (includes dim); tests; the site's backgrounds page and the published config reference.
- `src/UrDeck.Host/Editor/ThemeBackgroundSection.cs`, `ThemeThumbnails` (rendering and cache in `cache/theme-previews/`),
  the header quick-switch in `PagesSection`.
- `editor-forms`' `FormBuilder` fed with descriptors made from `ManifestParameter`s (an adapter in the engine).
