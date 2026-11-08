Before starting: `editor-forms` must be applied (and therefore `editor-shell` and its prerequisites). Read
`proposal.md`, `design.md` (D1–D6), the specs here and `docs/handoff/editor-design/ThemeBackground.dc.html` (its
`THEMES` and `PARAMS` tables mirror the built-ins; the real data comes from the catalogs). Model: Sonnet.

## 1. Engine

- [x] 1.1 `background.dim` (D5): own key in `BackgroundSettings`, clamped, warning when not a number; `DeckBackground.PaintAtTime` and `RequestKey`; tests (shader, picture, none, theme without scrim, 0); goldens unchanged
- [x] 1.2 `ManifestSettings.From(BackgroundCatalogEntry)` → range `SettingDescriptor`s with defaults (find where a parameter's default lives: `BackgroundParameter`/the shader's uniform default) and tests on the sixteen built-ins
- [x] 1.3 Background status for the editor: which candidate is shown and why the configured one failed, as data from `BackgroundPlan`/`DeckBackground` (no behaviour change), exposed through `ISettingsHost.BackgroundStatus`

## 2. Thumbnails

- [x] 2.1 `ThemeThumbnails` (D2): a preview session per theme of the current page, Sample readings, still background, one offscreen render, PNG cache in `cache/theme-previews/` keyed by content hash; one at a time off the UI thread; swatch placeholder
- [x] 2.2 Background tiles from manifest samples; a cached still render for a user background without a sample

## 3. Section

- [x] 3.1 Navigation item "Theme and background" after Pages; layout (D1)
- [x] 3.2 Theme grid with "Your own theme folder…"
- [x] 3.3 Background chips and tiles, writes per D3
- [x] 3.4 Options pane (D4): preview, Moving/Still, parameters form with Reset, Dim behind cards, picture drop/choose/change and the fallback line, motion level
- [x] 3.5 Theme quick-switch in the Pages header (D6)

## 4. Docs

- [x] 4.1 Site: the backgrounds page and the configuration reference describe `dim`; the themes page mentions choosing a theme in the editor; README

## 5. Verify

- [x] 5.1 `dotnet build urdeck.slnx -c Release` (0 warnings), `dotnet test`, coverage floor, `dotnet format --verify-no-changes`, Linux tests, site build
- [x] 5.2 Owner, manual on the panel: every theme picture appears and matches the panel after choosing; Theme's choice follows a theme change; None; a picture (drop and choose; then rename the file: fallback line); Still and Moving; drag parameters and Reset; Dim; motion levels; CPU with Still recorded
- [x] 5.3 `openspec validate editor-theme-background --strict`
