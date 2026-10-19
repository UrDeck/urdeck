## 1. Fonts

- [ ] 1.1 Compare `InstrumentSans-Variable.ttf` and `JetBrainsMono-Variable.ttf` in `spikes/theme-look-s3/fonts/` with their upstream releases; note each version and copyright line; confirm each licence file is OFL 1.1
- [x] 1.2 Copy both fonts and `InstrumentSans-OFL.txt`, `JetBrainsMono-OFL.txt` to `src/UrDeck.Engine/Themes/Builtin/`; check they appear as `UrDeck.Themes.<file>` resources in the built assembly
- [x] 1.3 Add both families to `THIRD-PARTY-NOTICES.md` (copyright holder, version, licence, where they are used)

## 2. Built-in themes

- [x] 2.1 Move the drafts to `src/UrDeck.Engine/Themes/Builtin/carbon.json` and `paper.json`; remove Paper's `typography.valueScale` (design 4); add `carbon` and `paper` to `ThemeStore.BuiltInNames`
- [x] 2.2 Delete `tests/UrDeck.Engine.Tests/Fixtures/themes/carbon` and `paper`; repoint `GoldenTests`, `CardChromeTests`, `CardUnderlayTests`, `GaugeThemeTests` (and any other user) at the built-ins; keep the other six drafts as fixtures
- [ ] 2.3 Test: each new built-in loads with no themes folder, logs no font or weight warning, and resolves the value, unit, label and body roles to the families in the spec (on Windows and Linux)
- [x] 2.4 Test: every v1 colour of `paper` (background, card fill, card border, text, muted text, accent, accent dim, good, warning, critical) equals the value in `paper.json`, none from `default-dark`; same for `carbon`
- [x] 2.5 Test: `ThemeCatalog` lists both with name, mode and pairings; `PairingIndex` recommends `contour` first for each; every background they name is a built-in
- [x] 2.6 Test: with no `background` in the config, `BackgroundPlan.Resolve` for each gives `contour` from the theme default, then the theme's static colour
- [x] 2.7 Check that a user theme folder named `carbon` still takes precedence (existing test covers built-in names generally; extend if it does not)

## 3. Goldens

- [x] 3.1 Redraw `carbon-page.png`, `paper-page.png`, `paper-dock.png` from the built-ins and add `carbon-dock.png` (`URDECK_UPDATE_GOLDENS=1`, Windows); look at each before committing
- [x] 3.2 Confirm the v1 goldens (`default-dark`, `default-light`, `glass`, page and dock) and the other drafts' goldens are unchanged

## 4. Tuning on the panel

- [x] 4.1 Build and install a local copy; show Carbon and then Paper on the 1100x3840 panel over `contour`, at 100 % and 150 % scaling, with a deck like the site deck (sparkline, status words, Clock header and week)
- [ ] 4.2 Show each over its static colour (shaders unavailable or a scratch override)
- [ ] 4.3 Apply the owner's adjustments to `carbon.json` and `paper.json`; keep a table of key, draft value, final value and reason for the pull request; update any test assertion a changed value moves, with the reason
- [x] 4.4 Decide Carbon's `meta.description` wording about load colour (design 4); confirm `gauge.colorBy` stays `accent` or record why not
- [ ] 4.5 Check whether Paper draws accent-coloured text anywhere a user must read it; if so darken the accent to at least 4.5:1 on the card fill and record it
- [ ] 4.6 Redraw the goldens after tuning (repeat 3.1) and get the owner's approval of the panel look

## 5. Contrast record

- [x] 5.1 Compute WCAG contrast on the card fill for text, muted text, accent, good, warning and critical of each theme (final values); text must be at least 4.5:1 and muted text at least 3:1; put the table in the pull request

## 6. Website and docs

- [x] 6.1 Add `site/decks/carbon/urdeck-config.json` (Clock with header label "Local time", `showSeconds`, `blinkSeparator`; Stats 2x1 CPU and GPU with sparkline; Weather 4x2; four labelled shortcuts) and `site/decks/paper/urdeck-config.json` (Clock with `showWeek`; Stats 2x1 meters; Weather 4x2; four labelled shortcuts)
- [x] 6.2 Render both with `pwsh scripts/Render-SiteDecks.ps1 carbon paper` to `site/src/assets/decks/`; add entries with caption and alt text to `site/src/data/themes.json`
- [x] 6.3 Update `site/src/content/docs/themes.md`: five built-ins, each new one with a line on its look, its fonts, its default background and the `"theme": "carbon"` / `"paper"` snippet
- [x] 6.4 Update `docs/themes.md` the same way, and tick the theme lines in `site/MEDIA-CHECKLIST.md`
- [x] 6.5 `cd site; npm ci; npm run build` passes

## 7. Wrap-up

- [ ] 7.1 `dotnet build urdeck.slnx -c Release` with 0 warnings; `dotnet test urdeck.slnx -c Release`; `dotnet format urdeck.slnx --severity warn --verify-no-changes`; the Linux test projects on WSL
- [ ] 7.2 `openspec validate themes-carbon-paper --strict`
- [x] 7.3 Update `docs/design/look-and-feel/STATUS.md`: the PR 12 row done; Carbon's pattern waits for `page-pattern` (which redraws Carbon's goldens and gets a second panel look); follow-ups found while tuning (for example a fill colour that follows the status level)
- [ ] 7.4 Pull request `feat(theme): Carbon and Paper built-in themes` with the tuning table, the contrast table and side-by-side pictures with the board
