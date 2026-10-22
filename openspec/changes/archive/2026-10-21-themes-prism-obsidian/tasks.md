## 1. Fonts

- [x] 1.1 Compare `BricolageGrotesque-Variable.ttf`, `BodoniModa-Variable.ttf` and `Jost-Variable.ttf` in `spikes/theme-look-s3/fonts/` with their upstream releases; note each version and copyright line; confirm each licence file is OFL 1.1
- [x] 1.2 Copy the three fonts and their `-OFL.txt` files to `src/UrDeck.Engine/Themes/Builtin/`; check they appear as `UrDeck.Themes.<file>` resources in the built assembly
- [x] 1.3 Add the three families to `THIRD-PARTY-NOTICES.md` (copyright holder, version, licence, where used)

## 2. Built-in themes

- [x] 2.1 Move the drafts to `src/UrDeck.Engine/Themes/Builtin/prism.json` and `obsidian.json`; remove `typography.valueScale` from both (design 4); keep Prism's `icons.set: "outline"` (design 5); add `prism` and `obsidian` to `ThemeStore.BuiltInNames`
- [x] 2.2 Delete `tests/UrDeck.Engine.Tests/Fixtures/themes/prism` and `obsidian`; repoint every test that used them (`GoldenTests`, `CardChromeTests`, `CardUnderlayTests` and any other user) at the built-ins; keep the other four drafts as fixtures
- [x] 2.3 Test: each built-in loads with no themes folder, logs no font or weight warning, and resolves the value, unit, label and body roles to the families in the spec (Windows and Linux)
- [x] 2.4 Test: every v1 colour of `obsidian` and of `prism` equals the value in its own file, none from `default-dark`
- [x] 2.5 Test: `ThemeCatalog` lists both with name, mode and pairings; `PairingIndex` recommends the first paired background first; every background they name is a built-in
- [x] 2.6 Test: with no `background` in the config, `BackgroundPlan.Resolve` gives each theme's default shader, then its static gradient
- [x] 2.7 Check that a user theme folder named `prism` or `obsidian` still takes precedence
- [x] 2.8 Test: built-ins validate with no error and no warning (extend the existing built-ins-validate-clean test to the new names)

## 3. Lint and contrast

- [x] 3.1 Run `dotnet run --project tools/UrDeck.Cli -c Release -- theme validate` on both built-ins with the fonts in place; list the findings
- [x] 3.2 Resolve `THEME054` for Prism per design 6 (shadow limited to the gap) without changing the approved look, or record why it cannot be
- [x] 3.3 Fix any THEME060 to THEME062 contrast finding in the theme values (Prism muted text and status colours over translucent cards; Obsidian `critical` and muted text); validate clean
- [x] 3.4 Put a WCAG contrast table (text, muted text, accent, good, warning, critical on the card fill; for Prism on the worst background) in the PR

## 4. Goldens

- [x] 4.1 Redraw `prism-page.png`, `prism-dock.png`, `obsidian-page.png`, `obsidian-dock.png` from the built-ins (`URDECK_UPDATE_GOLDENS=1`, Windows); look at each before committing
- [x] 4.2 Confirm the v1, Carbon, Paper and remaining drafts' goldens are unchanged

## 5. Tuning on the panel

- [x] 5.1 Build and install a local copy; show Prism and Obsidian on the 1100x3840 panel over their default backgrounds and their static gradients, at 100 % and 150 % scaling, with a deck like the site deck
- [x] 5.2 Decide Prism's icons: outline (default) or Meteocons (drop `set`); if Meteocons, re-measure CPU, GPU and memory and record in `docs/perf/theme-look.md`
- [x] 5.3 Re-measure Prism's paint cost against `default-dark` with the real font (`docs/perf/card-chrome.md` method); record it; take it to the owner if it exceeds 1.83x
- [x] 5.4 Apply the owner's adjustments; keep a table of key, draft, final value and reason for the PR; update assertions a changed value moves
- [x] 5.5 Redraw the goldens after tuning (repeat 4.1) and get the owner's approval of the panel look

## 6. Website and docs

- [x] 6.1 Add `site/decks/prism/urdeck-config.json` and `site/decks/obsidian/urdeck-config.json` following `06-themes.md` "Site deck widgets"
- [x] 6.2 Render both with `pwsh scripts/Render-SiteDecks.ps1 prism obsidian` to `site/src/assets/decks/`; add entries with caption and alt text to `site/src/data/themes.json`
- [x] 6.3 Update `site/src/content/docs/themes.md`: each new theme with its look, fonts, default background and the `"theme": "prism"` / `"obsidian"` snippet
- [x] 6.4 Update `docs/themes.md` the same way and tick the theme lines in `site/MEDIA-CHECKLIST.md`
- [x] 6.5 `cd site; npm ci; npm run build` passes

## 7. Wrap-up

- [x] 7.1 Rebase on `main` (or on whichever sibling theme change landed first); resolve conflicts in `ThemeStore.BuiltInNames`, `THIRD-PARTY-NOTICES.md`, `GoldenTests.cs`, `docs/themes.md` and `site/src/data/themes.json` by keeping every theme's entries
- [x] 7.2 `dotnet build urdeck.slnx -c Release` with 0 warnings; `dotnet test urdeck.slnx -c Release`; `dotnet format urdeck.slnx --severity warn --verify-no-changes`; the Linux test projects on WSL
- [x] 7.3 `openspec validate themes-prism-obsidian --strict` and `openspec validate --all --strict`
- [x] 7.4 Update `docs/design/look-and-feel/STATUS.md`: PR 13 row done (Prism and Obsidian), icon question settled, follow-ups found while tuning
- [x] 7.5 Pull request `feat(theme): Prism and Obsidian built-in themes` with the tuning table, contrast table, cost figure and side-by-side pictures
