## 1. Fonts

- [x] 1.1 Compare `BarlowCondensed-Bold.ttf`, `IBMPlexMono-Regular.ttf` in `spikes/theme-look-s3/fonts/` with their upstream releases; note each version and copyright line; confirm each licence file is OFL 1.1
- [x] 1.2 Copy the three fonts and `BarlowCondensed-OFL.txt`, `IBMPlexMono-OFL.txt` to `src/UrDeck.Engine/Themes/Builtin/`; check they appear as `UrDeck.Themes.<file>` resources
- [x] 1.3 Add both families to `THIRD-PARTY-NOTICES.md` (holder, version, licence, use); rebase on whichever sibling change landed first
- [x] 1.4 Make Blueprint's title weight 500 real with `IBMPlexMono-Medium.ttf` (per-role file per `fonts.md`); if the typography model cannot name a file per role, record a follow-up and set the title weight the file has

## 2. Validate the drafts and correct them

- [x] 2.1 Run `dotnet run --project tools/UrDeck.Cli -c Release -- theme validate` on both drafts; list every finding
- [x] 2.2 Fix Ember's tick colour `#ffffff1c` to `#1cffffff`; check every other hex value in both themes for AARRGGBB mistakes
- [x] 2.3 Remove `typography.valueScale` from both (Ember 1.2, Blueprint 1.05)
- [x] 2.4 Set Ember's `body` weight to the weight the Bold file has (700) so no warning is logged
- [x] 2.5 Fix contrast findings (`THEME060` to `THEME062`) expected on Blueprint (muted text and `critical` on the blended translucent card fill, the dim track) and any on Ember; change only the offending value, record old and new
- [x] 2.6 Add readout room below the middle for Ember's tach and Blueprint's marker gauge (ticks plus inside label) with existing keys; record a follow-up if no key does it

## 3. Built-in themes

- [x] 3.1 Move the drafts to `src/UrDeck.Engine/Themes/Builtin/ember.json` and `blueprint.json` (with the corrections); add `ember` and `blueprint` to `ThemeStore.BuiltInNames`
- [x] 3.2 Check each background the themes name exists in `src/UrDeck.Engine/Backgrounds/Builtin` (`embers`, `warpgate`, `equalizer`, `plotter`, `contour`, `streams`)
- [x] 3.3 Delete `Fixtures/themes/ember` and `blueprint`; repoint every test that used them (`GoldenTests`, `CardChromeTests`, `CardUnderlayTests`, `GaugeThemeTests`, pattern tests) at the built-ins
- [x] 3.4 Test: each loads with no themes folder, logs no font or weight warning, and resolves its roles to the families in the spec (Windows and Linux)
- [x] 3.5 Test: every colour of each theme equals its own file, none from `default-dark`; Ember's tick colour has alpha 0x1c
- [x] 3.6 Test: `ThemeCatalog` lists both with name, mode and pairings; `PairingIndex` recommends `embers` and `plotter` first; `BackgroundPlan.Resolve` gives the default background, then the static fallback
- [x] 3.7 Test: motion levels (`full`, `reduced`) resolve; a user theme folder named `ember` or `blueprint` takes precedence
- [x] 3.8 `urdeck-cli theme validate` on both built-ins passes clean (extend the CI built-in check if it lists names)

## 4. Goldens

- [x] 4.1 Redraw `ember-page`, `blueprint-page`, `blueprint-dock` and add `ember-dock` from the built-ins (`URDECK_UPDATE_GOLDENS=1`, Windows); look at each (pattern, outline icons, gauges, fonts)
- [x] 4.2 Confirm the v1 goldens and the other drafts' goldens are unchanged

## 5. Tuning on the panel

- [x] 5.1 Install a local copy; show Ember and then Blueprint on the 1100x3840 panel at 100 % and 150 %, over the default background and the static fallback, with a deck like the site deck
- [x] 5.2 Look at Ember's slanted values on a 4x2 clock (lean about 50 px past the rectangle): reduce slant if clipped or crowded; record the engine follow-up (should the fit and unit gap count the lean)
- [x] 5.3 Look at gauges: readout room, Ember's redline and needle, Blueprint's tick numbers and crosshair; confirm motion levels and icon tints
- [x] 5.4 Measure Ember with its glow on the panel (private memory 25 s after start, CPU over 20 s; compare with 180 MB / 5.9 % in `docs/perf/theme-look.md`); accept or reduce `gauge.glow.blur` / `valueGlow.alpha`; record the numbers
- [x] 5.5 Apply the owner's adjustments; keep the table of key, draft value, final value and reason for the PR; update any assertion a changed value moves
- [x] 5.6 Redraw goldens after tuning (repeat 4.1) and get the owner's approval of the panel look

## 6. Contrast record

- [x] 6.1 Compute WCAG contrast on the (blended) card fill for text, muted text, accent, good, warning and critical of each theme with final values; text at least 4.5:1, muted at least 3:1; put the table in the PR

## 7. Website and docs

- [x] 7.1 Add `site/decks/ember/urdeck-config.json` and `site/decks/blueprint/urdeck-config.json` following `06-themes.md` ("Site deck widgets")
- [x] 7.2 Render with `pwsh scripts/Render-SiteDecks.ps1 ember blueprint` to `site/src/assets/decks/`; add entries with caption and alt text to `site/src/data/themes.json`
- [x] 7.3 Update `site/src/content/docs/themes.md` and `docs/themes.md`: each new theme's look, fonts, default background, and the `"theme": "ember"` / `"blueprint"` snippet
- [x] 7.4 Tick the theme lines in `site/MEDIA-CHECKLIST.md`
- [x] 7.5 `cd site; npm ci; npm run build` passes
- [x] 7.6 Reconcile shared files (`BuiltInNames`, `THIRD-PARTY-NOTICES.md`, `GoldenTests.cs`, `docs/themes.md`, `themes.json`) with sibling theme changes; rebase on whichever landed first

## 8. Wrap-up

- [x] 8.1 `dotnet build urdeck.slnx -c Release` with 0 warnings; `dotnet test urdeck.slnx -c Release`; `dotnet format urdeck.slnx --severity warn --verify-no-changes`; Linux test projects on WSL
- [x] 8.2 `openspec validate themes-ember-blueprint --strict`
- [x] 8.3 Update `docs/design/look-and-feel/STATUS.md`: PR 14 row done, follow-ups (lean in the fit, readout room key if missing)
- [x] 8.4 Pull request `feat(theme): Ember and Blueprint built-in themes` with the tuning table, contrast table, perf numbers and pictures
