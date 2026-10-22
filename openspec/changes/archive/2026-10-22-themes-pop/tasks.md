## 1. Font

- [x] 1.1 Compare `Archivo-Variable.ttf` in `spikes/theme-look-s3/fonts/` with the upstream Google Fonts release; note version and copyright line; confirm `Archivo-OFL.txt` is OFL 1.1
- [x] 1.2 Copy the font and `Archivo-OFL.txt` to `src/UrDeck.Engine/Themes/Builtin/`; check both appear as `UrDeck.Themes.<file>` resources; do not bundle Archivo Black
- [x] 1.3 Add Archivo to `THIRD-PARTY-NOTICES.md` (holder, version, licence, use); rebase on whichever sibling theme change landed first

## 2. Built-in theme

- [x] 2.1 Move the draft to `src/UrDeck.Engine/Themes/Builtin/pop.json`; remove `typography.valueScale` (design 2); add `pop` to `ThemeStore.BuiltInNames`
- [x] 2.2 Run `dotnet run --project tools/UrDeck.Cli -c Release -- theme validate src/UrDeck.Engine/Themes/Builtin/pop.json --fail-on-warnings` (docs/themes.md "Checking a theme") and record the findings
- [x] 2.3 Fix the expected contrast findings: ink on the blue tint `#3b5bff` (`THEME060`, lighten the tint), `textMuted` if `THEME061` fires, `warning` equal to the yellow card fill (`THEME062`, use an orange); recheck on all five tints until clean
- [x] 2.4 Delete `tests/UrDeck.Engine.Tests/Fixtures/themes/pop`; repoint `GoldenTests`, `CardUnderlayTests` and any other user at the built-in
- [x] 2.5 Test: `pop` loads with no themes folder, logs no font or weight warning, and resolves value (900), unit, label and body to Archivo (Windows and Linux)
- [x] 2.6 Test: every v1 colour of `pop` equals the value in `pop.json`, none from `default-dark`
- [x] 2.7 Test: `ThemeCatalog` lists Pop with name, mode and pairings; `PairingIndex` recommends `shapes` first; every background it names is a built-in
- [x] 2.8 Test: with no `background` configured, `BackgroundPlan.Resolve` gives `shapes` from the theme default, then the static colour; a user theme folder named `pop` takes precedence
- [x] 2.9 Check the hard shadow reach logging with Pop's opaque fills (shadow at full reach, not clipped to the gap, none at the page edge or under the dock)

## 3. Goldens

- [x] 3.1 Redraw `pop-page.png` and `pop-dock.png` from the built-in (`URDECK_UPDATE_GOLDENS=1`, Windows) and look at each
- [x] 3.2 Confirm the v1 goldens and the other themes' goldens are unchanged

## 4. Tuning on the panel

- [x] 4.1 Build and install a local copy; show Pop over `shapes` on the 1100x3840 panel at 100 % and 150 % scaling with a deck like the site deck (thick bars, status chips, Clock, Weather)
- [x] 4.2 Show it over its static colour
- [x] 4.3 Check the numerals are huge without `valueScale`; tune `card.padding`, `unitRatio`, `tracking.value`; record a follow-up if only the engine can fix it
- [x] 4.4 Apply the owner's adjustments; keep a table of key, draft value, final value and reason for the pull request; update any assertion a changed value moves
- [x] 4.5 Redraw the goldens after tuning (repeat 3.1) and get the owner's approval of the panel look

## 5. Contrast record

- [x] 5.1 Compute WCAG contrast of text, muted text and status colours on each of the five tints (final values); put the table in the pull request and confirm `theme validate` is clean

## 6. Website and docs

- [x] 6.1 Add `site/decks/pop/urdeck-config.json` (Clock, Stats 2x1 thick bars, Weather, four labelled shortcuts)
- [x] 6.2 Render with `pwsh scripts/Render-SiteDecks.ps1 pop` to `site/src/assets/decks/`; add a `site/src/data/themes.json` entry with caption and alt text (merge with siblings)
- [x] 6.3 Update `site/src/content/docs/themes.md` and `docs/themes.md`: Pop's look, font, default background and the `"theme": "pop"` snippet; tick the theme lines in `site/MEDIA-CHECKLIST.md`
- [x] 6.4 `cd site; npm ci; npm run build` passes

## 7. Wrap-up

- [x] 7.1 `dotnet build urdeck.slnx -c Release` with 0 warnings; `dotnet test urdeck.slnx -c Release`; `dotnet format urdeck.slnx --severity warn --verify-no-changes`; the Linux test projects on WSL
- [x] 7.2 `openspec validate themes-pop --strict`
- [x] 7.3 Update `docs/design/look-and-feel/STATUS.md` (PR 15 row done, follow-ups found)
- [x] 7.4 Pull request `feat(theme): Pop built-in theme` with the tuning table, the contrast table and the panel pictures
