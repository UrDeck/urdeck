## 1. Font

- [x] 1.1 Compare `ChakraPetch-Bold.ttf` in `spikes/theme-look-s3/fonts/` with its upstream release; note version and copyright line; confirm the licence file is OFL 1.1
- [x] 1.2 Copy `ChakraPetch-Bold.ttf` and `ChakraPetch-OFL.txt` to `src/UrDeck.Engine/Themes/Builtin/`; check both appear as `UrDeck.Themes.<file>` resources
- [x] 1.3 Add Chakra Petch to `THIRD-PARTY-NOTICES.md` (holder, version, licence, use)

## 2. Built-in theme

- [x] 2.1 Move the draft to `src/UrDeck.Engine/Themes/Builtin/rigline.json`; set every weight to the file's 700; confirm `valueScale` is not above 1 and `valueFill.animate` and `pattern.scroll` are absent; add `rigline` to `ThemeStore.BuiltInNames`
- [x] 2.2 Run `dotnet run --project tools/UrDeck.Cli -c Release -- theme validate` on the built-in; fix every finding (expected `THEME060` to `THEME062`; `THEME020` goes with the font); it must be clean
- [x] 2.3 Check every colour in the draft for the `#AARRGGBB` finding (`accentDim`, pattern, track, well fill, indicator colours) and confirm `card.glow.color` `#aa7a5c` is intended
- [x] 2.4 Delete `tests/UrDeck.Engine.Tests/Fixtures/themes/rigline`; repoint `GoldenTests`, `CardChromeTests`, `CardUnderlayTests` and any other user (search for `rigline`) at the built-in
- [x] 2.5 Test: loads with no themes folder, logs no font or weight warning, and resolves every text role to Chakra Petch (Windows and Linux)
- [x] 2.6 Test: every colour of `rigline` equals its file, none from `default-dark`
- [x] 2.7 Test: `ThemeCatalog` lists it with name, mode and pairings; `PairingIndex` recommends `horizon` first; every background it names is built in
- [x] 2.8 Test: with no `background`, `BackgroundPlan.Resolve` gives `horizon` from the theme default, then the static gradient
- [x] 2.9 Test: built-in themes validate clean (extend the existing "built-ins validate clean" test); a user theme named `rigline` takes precedence
- [x] 2.10 Test: at effective level `full` the resolved style has a border animation, at `reduced` and `off` it does not (extends the motion tests)

## 3. Goldens

- [x] 3.1 Redraw `rigline-page.png` and `rigline-dock.png` from the built-in (`URDECK_UPDATE_GOLDENS=1`, Windows); look at each
- [x] 3.2 Confirm the v1 goldens and the other themes' goldens are unchanged

## 4. Tuning on the panel

- [x] 4.1 Install a local copy; show Rigline on the 1100x3840 panel at 100 % and 150 % over `horizon` and over the static gradient, with the site deck
- [x] 4.2 Check the slant overhang (about 50 px on a 4x2 clock) on the Clock and Stats against the card edge; shorten `slant` if it clips; record whether the fit should count the lean
- [x] 4.3 Check the compact cards: tile strips and the turning border together; record whether `card.compact.borderAnimate` is needed
- [x] 4.4 Check the conic seam at the start angle, the glow colour, and the body text weight (does it want a real 500 file?)
- [x] 4.5 Apply the owner's adjustments; keep a table of key, draft value, final value, reason for the pull request; update any assertion a value moves
- [x] 4.6 Redraw the goldens after tuning and get the owner's approval of the panel look

## 5. Measure and accept the whole theme

- [x] 5.1 Measure CPU, GPU and private memory on the panel for Rigline at motion `full`, `reduced` and `off`, with glow, ramp arcs, scanlines and a moving `horizon`, as `docs/perf/motion.md` does; add a Rigline section to that file
- [x] 5.2 Confirm the border pauses with the display off and with the deck hidden, and that `reduced` and `off` cost what the still theme costs
- [x] 5.3 Owner accepts the numbers, or lighten in the order of design decision 4 and measure again
- [x] 5.4 Skipped by the owner (recorded in STATUS "Not verified"): Optional: run `horizon` under Rigline for over four hours and check for a jump or stall at the 4-hour wrap; record the result in `STATUS.md` "Not verified"

## 6. Contrast record

- [x] 6.1 Compute WCAG contrast on the card fill for text, muted text, accent, good, warning, critical (final values); text at least 4.5:1, muted at least 3:1; put the table in the pull request
- [x] 6.2 Check the conic border colours and the three `ramp` colours against the card fill (3:1 for graphics); lighten any under it and record the new value

## 7. Website and docs

- [x] 7.1 Add `site/decks/rigline/urdeck-config.json` (Clock, Stats 2x1 with ring gauges, Weather 4x2, four labelled shortcuts)
- [x] 7.2 Render it with `pwsh scripts/Render-SiteDecks.ps1 rigline` to `site/src/assets/decks/`; add an entry with caption and alt text to `site/src/data/themes.json`
- [x] 7.3 Skipped (the media tool has no theme loops; recorded as a PR 17 follow-up): Optional: spinning-border loop with `npm run media` (`render --frames`)
- [x] 7.4 Update `site/src/content/docs/themes.md` and `docs/themes.md`: the look, the font, the default background, the `"theme": "rigline"` snippet, and a note on `motion`; tick the theme lines in `site/MEDIA-CHECKLIST.md`
- [x] 7.5 `cd site; npm ci; npm run build` passes

## 8. The seconds fix (found on the panel)

- [x] 8.1 `Readout`: `ReadoutOptions.WidestUnit`; measure and draw a unit with digits at equal advances (`sdk/UrDeck.Sdk/Components/Readout.cs`)
- [x] 8.2 Clock: pass `WidestUnit`, remove `KeepSize`
- [x] 8.3 Tests: unit digits of `11` to `00` give the same size and width in Rigline's font; widest unit reserves its width; the Clock's time does not move between `:41` and `:44`
- [x] 8.4 Delta specs for `components` and `widget-clock`; proposal and design updated; other themes' goldens unchanged

## 9. Wrap-up

- [x] 9.1 `dotnet build urdeck.slnx -c Release` with 0 warnings; `dotnet test urdeck.slnx -c Release`; `dotnet format urdeck.slnx --severity warn --verify-no-changes`; the Linux test projects on WSL
- [x] 9.2 `openspec validate themes-rigline --strict`
- [x] 9.3 Update `docs/design/look-and-feel/STATUS.md`: the PR 16 row done, the theme work complete, follow-ups found (`card.compact.borderAnimate`, slant in the fit, a real 500 weight)
- [x] 9.4 Pull request `feat(theme): Rigline built-in theme` with the tuning table, the contrast table and the measurements
