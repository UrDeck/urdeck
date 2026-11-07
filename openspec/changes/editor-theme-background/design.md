## Context

- Configuration: `theme` (a name), `background` (absent/`null` = the theme's choice; `{type: none}`; `{type: shader,
  name, speed, scale, colors, <parameters>}`; `{type: image, path}`), `motion` (`full`/`reduced`/`off`, absent = follow
  Windows). `BackgroundSettings` (`src/UrDeck.Engine/Backgrounds/BackgroundSettings.cs`): own keys `type`, `name`,
  `path`, `speed` (0 = still), `scale`, `colors`; every other key is a shader parameter.
- `ThemeCatalog.List(ThemeStore)` → `ThemeCatalogEntry` with `ThemeSwatch` (background, card fill, accent, text) and
  samples; `BackgroundCatalog` → `BackgroundCatalogEntry` (name, title, description, tags, `PairsWithThemes`,
  `Parameters` as `ManifestParameter(Name, Label, Description, Kind, Min, Max, Step, Unit, Hidden)`, samples, built-in);
  `PairingIndex.Recommend(theme, backgrounds, limit)` and `BackgroundsFor(theme)`.
- The scrim is the theme's `background.scrim` (colour + alpha), drawn by `DeckBackground.PaintAtTime` over shader,
  picture and still backgrounds, not over the plain colour or `none` (`backgrounds` spec, "Background Scrim").
- `editor-shell`: preview sessions (`ISettingsHost.CreatePreviewSession`), `Document.Edit`. `editor-forms`:
  `FormBuilder`, `SettingDescriptor`.
- The goldens are test assets and are not shipped; the theme pictures in the mock-up came from them.

## Goals / Non-Goals

**Goals:** pick a theme by its look on the user's own page; make "still" an easy choice; shader options without
knowing their names; no new rendering path (everything through the deck session and the existing background code).

**Non-Goals:** editing a theme (the theme tools and the cookbook do that); per-page backgrounds; video backgrounds.

## Decisions

### D1. Section layout

Two columns: left (scrolling) Theme grid then Background choices and tiles; right (fixed, ~360 DIP) preview and
options, as `ThemeBackground.dc.html`. Below 1100 DIP the options move under the tiles.

### D2. Theme pictures

- For each theme: a deck session (preview kind, `editor-shell` D2) of the user's **current page** with that theme,
  Sample readings, still background (speed 0), at the deck's surface size, painted once into an offscreen
  `SKSurface` at 220 DIP wide; PNG cached in `cache/theme-previews/<theme>-<hash>.png`, where hash covers the page's
  JSON, the theme file's write time and the background setting. One render at a time on a background thread; the
  session is disposed after each render.
- Until ready, a tile shows the theme's swatch (`ThemeSwatch`: background, card rectangle in card fill, an accent bar,
  text colour line).
- Alternative considered: ship pictures (the goldens): they show the sample page, not the user's, and are not part of
  the build output.

### D3. Background choices and tiles

- Chips: "Theme's choice" (subtitle: the theme's default background's title and "still or moving", or "Its colour (this
  theme names no background)"), "None" ("The theme colour only"), "Picture…" ("A file of your own" or the file name).
- "Suggested for <theme>": `[theme default] + PairingIndex.BackgroundsFor(theme)` without duplicates; "All backgrounds":
  the rest by title. Tiles show the background's first sample picture (`ManifestSample` image); a user background
  without one gets a still frame rendered once with `DeckBackground` at tile size and cached like D2.
- Writing: Theme's choice → remove `background`; None → `{ "type": "none" }`; a tile → `{ "type": "shader", "name": n }`
  keeping `speed` 0 if the current one is still (so choosing another background keeps Still), and keeping `dim`;
  Picture → `{ "type": "image", "path": p }` (keeps `dim`).
- Shader tiles stay visible and choosable in every state (handoff).

### D4. Options pane

- Preview: a preview session (live readings, the deck's motion) of the current page, at a fixed width.
- Motion (shader only; disabled otherwise): `RadioButtons` Moving ("30 frames a second") / Still ("one frame, no GPU
  cost"); Still writes `speed: 0`, Moving removes `speed`. Under Moving, an informational line: "A moving background
  draws 30 frames a second while the display is on. Still keeps the look at no cost."
- Parameters: `ManifestParameter`s (not `Hidden`) mapped to `SettingDescriptor`s (kind `range`, min/max/step/unit from
  the manifest, label from `Label` or the name, help from `Description`, default from the uniform's default the shader
  validator reads; see `ShaderLint`/`BackgroundParameter` for where defaults live) and built with `FormBuilder` at the
  object path `/background` (the parameters are keys of the background object). Header: "<Title> options" with Reset
  (removes every parameter key in one transaction). An engine adapter `ManifestSettings.From(entry)` with tests.
- Dim behind cards: `Slider` 0–70 %, writes `background.dim` (0–0.7); a "Theme's" tick shows the theme's own scrim
  value; double-click or a small Reset returns to absent. Disabled for None.
- Picture: drop target (`AllowDrop`; PNG, JPEG, WebP) and "Choose a picture…" (`FileOpenPicker`); once chosen: the
  thumbnail, the file name, its folder, "Cropped to fill the screen. If the file moves, the deck falls back to the
  theme's background and says so here.", Change…. The fallback is shown from the deck's background status
  (`ISettingsHost.BackgroundStatus`: which candidate is shown and why the configured one failed, from `BackgroundPlan`'s
  existing failure logging, exposed as data).
- Motion level (deck-wide `motion`): `ComboBox` Follow Windows / Full / Reduced / Off, under a "Motion" heading at the end
  of the section, with help "Reduced keeps the clock and values moving but stills the background and card effects."
  (check the `motion` spec for the exact meaning and adjust the words).

### D5. `background.dim`

- `BackgroundSettings`: `dim` becomes an own key (no longer a parameter), a number clamped to 0–0.7; invalid → warning,
  ignored.
- `DeckBackground.PaintAtTime`: when `dim` is set, the scrim is drawn with the theme scrim's colour (black when the theme
  has none) at alpha `dim` (the colour's own alpha is not multiplied in this case); with `dim` 0 no scrim. Not drawn for
  the plain colour or `none`, as today. `RequestKey` includes it.
- Snapshot and `preview render` draw it (same paint path). Tests in the background tests; goldens unchanged (no golden
  sets `dim`).
- Site: the backgrounds page and the configuration reference gain `dim`.

### D6. Theme quick-switch

In the Pages header (right of Undo/Redo): a `DropDownButton` "Theme  <name>  ⌄" whose flyout lists the themes (a swatch
dot and the name, the current one checked) and "All themes and backgrounds…" (navigates to the section). Choosing writes
`theme`.

## Risks / Trade-offs

- [Rendering eleven pictures costs a few seconds of CPU] → one at a time, off the UI thread, cached by content hash; the
  swatches keep the grid usable meanwhile.
- [`dim` and the theme's scrim confuse theme authors] → documented: the theme sets the default, the user may override.
