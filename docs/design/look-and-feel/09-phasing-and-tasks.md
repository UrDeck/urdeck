# 09 Phasing and work items

One work item = one pull request = (usually) one OpenSpec change. Each item is written at **proposal size**: *Why*, *What changes*, *Impact*, *Tasks*,
*Acceptance*, *Tests*, *Risks*. Pat's OpenSpec skill turns these into `openspec/changes/<id>/{proposal,design,tasks,specs}`; the item text is
deliberately close to that shape. Suggested model follows `docs/BACKLOG.md`'s convention (Opus = architecture/spikes/hard debugging; Sonnet = the rest).

## Dependency graph

```
PR0 spikes+harness ─┬─> PR1 model ─┬─> PR2 chrome ───────────────┐
                    │              ├─> PR3 typography ───────────┤
                    │              ├─> PR4 gauges+widgets ───────┤
                    │              └─> PR5 pairing+bg plan ─> PR6/PR7 shaders
                    │                                            ├─> PR8 pattern+motion ─> PR9 animated chrome
                    │                                            ├─> PR10 icons
                    └────────────────────────────────────────────┴─> PR11 CLI ─> PR12..PR16 themes ─> PR17 docs+site ─> PR18 editor seams
```
PR6/PR7 (shaders) only need PR5. PR11 can start after PR1. Themes (PR12-16) need the PRs for the knobs they use (listed per theme).

## Global definition of done (every PR)

- `dotnet build urdeck.slnx -c Release` 0 warnings; `dotnet test` green; `dotnet format urdeck.slnx --severity warn` clean; `openspec validate --all --strict` clean.
- **v1 invariance:** golden snapshots of `default-dark`, `default-light`, `glass` unchanged (from PR 0 on).
- Public docs for any user-visible key updated in the same PR (`docs/themes.md` or `docs/backgrounds.md`).
- PR title is a conventional commit; description says *why*, *what*, how verified, and includes before/after pictures when pixels changed.
- Tick `tasks.md` only when verified; archive the change when all tasks are done.
- Never touch `main` directly.

---

## PR 0: `chore(theme): spikes and golden-snapshot harness`  (Opus, M)

**Why.** Three assumptions decide the architecture (bleed, animated borders, fonts) and nothing protects v1 pixels yet.
**Changes.**
1. **Golden harness:** `GoldenImage.Compare`, `--update-goldens`, fixture `sample-page.json` (see `07 §3`), goldens for the three current themes at 1100x1920.
2. **Spike S1 (bleed/underlay):** prototype a page-level `SKXamlCanvas` below the widgets in `PageHost`. Measure memory, slide-animation smoothness, press effect, z-order, input pass-through on the panel. Write `docs/perf/theme-look.md` §S1 with a go/no-go and the chosen option (A or B of `03 §1.3`).
3. **Spike S2 (animated border):** try (a) compositor-masked rotating gradient, (b) cached ring bitmaps blit per frame at 15/30 fps. Measure CPU/memory for 8 cards. Pick one or declare "ship Rigline's border static".
4. **Spike S3 (fonts):** confirm the variable-font `wght` axis path works for each font in `assets/fonts.md` (Bricolage, Instrument Sans, JetBrains Mono, Jost, Bodoni Moda, Archivo; static files for Chakra Petch, Barlow Condensed, IBM Plex Mono, Archivo Black); check the installer-size delta; confirm licences.
**Acceptance.** Goldens committed; `docs/perf/theme-look.md` has the three decisions with numbers; PR 2 and PR 9 designs updated to match.
**Risks.** S1 fails → option A with capped bleed (`03`). S2 fails → Rigline border static (still handsome).

## PR 1: `feat(theme): format 2 definition model`  (Sonnet, L)  depends: 0

**Why.** Everything else needs the data model, merge rules and validation first, with no rendering change.
**Changes.** New nullable members/classes in `ThemeDefinition` for **every key in `02` Appendix A**; `MergeOver` (with the non-inherited exceptions of `02 §3`); `Sanitize` with `ValueCheck.Enum/Colors/Angle/Array`; cross-field rules of `02 §5`;
`meta`, `format`, reserved `card.blur`; split the file by group; copy the schema to `docs/schemas/v2/theme.json`; add the 8 draft themes under `src/UrDeck.Engine/Themes/Builtin/` **but do not register them in `BuiltInNames`** yet.
**Tasks.**
- [ ] Add classes: `ThemeMetaDefinition, ThemeCardChromeDefinition (shadow, glow, highlight, sheen, cornerMarks, borderGradient, borderDash, borderAnimate, fillGradient, compact), ThemePatternDefinition, ThemeDividerDefinition, ThemeChipDefinition, ThemeWellDefinition, ThemeAccentStripDefinition, ThemeFontsDefinition, ThemeCaseDefinition, ThemeTrackingDefinition, ThemeValueFillDefinition, ThemeGlowDefinition, ThemeGaugeDefinition (extended), ThemeIconsDefinition, ThemeBackgroundDefinition, ThemeMotionDefinition`.
- [ ] `MergeOver` for each; unit-test the exceptions.
- [ ] `Sanitize` for each key; warning texts follow the v1 template exactly.
- [ ] `GaugeStyle` enum additions in the SDK (`Meter, Tach, Marker, ThickBar`; append only) and `ThemeGaugeDefinition.Parse`.
- [ ] Schema-vs-model reflection test ("no drift").
- [ ] Round-trip: each draft theme loads with **zero warnings**.
**Acceptance.** All draft themes load clean; v1 goldens unchanged; a theme with every key invalid loads with the default's values and one warning per key; coverage floor for `UrDeck.Engine` still met.
**Risks.** Size: it is mostly mechanical; keep it reviewable by committing group by group. A `System.Text.Json` enum/array edge: use string enums parsed manually as v1 does.

## PR 2: `feat(card): card chrome (shape, borders, shadow, glow, marks, compact)`  (Sonnet; Opus review of S1 fallout, L)  depends: 1, 0/S1

**Why.** Cards are what the eye sees first; this is the biggest visual lever.
**Changes.** `CardChrome`, `CardShape`, `PageUnderlay` (option B) or capped bleed (option A); `WidgetPainter` front/back split with the v1 fast path;
`card.compact` resolution (second SDK `Theme` for compact cards); `tint: cycle`; chamfer and circle shapes; borders solid/dashed/dotted/gradient (conic and linear, **static**); shadow, glow, highlight, sheen, cornerMarks.
**Tasks.**
- [ ] `CardShape.Build` (round fast path, chamfer path, circle) + analytic geometry tests.
- [ ] `CardChrome.DrawBack/DrawFront` per `02 §4.3` order.
- [ ] `PageUnderlay` (live host `PageHost` + `PageRenderer`): draws shadows, glows, outside marks for all cards from layout rects; redraw on layout/theme change only.
- [ ] Card index plumbed for `tint` (page order, dock slots).
- [ ] Compact detection (<= 1x1 cell or dock slot) and compact `Theme`.
- [ ] Press effect still scales the card with its border; confirm underlay stays put without artefacts.
- [ ] Hairline pixel snapping (`03 §3`).
- [ ] Goldens for Carbon, Paper, Ember, Blueprint, Pop (cards only; widgets as they are).
**Acceptance.** Chrome of Carbon/Ember/Blueprint/Pop/Paper/Obsidian matches the mocks at card level; v1 goldens unchanged; paint time per card <= 1.5x v1.
**Risks.** Z-order and hit testing of the underlay (S1); conic border AA at corners; dashed borders on chamfer paths (use `SKPathEffect.CreateDash` on the stroked path).

## PR 3: `feat(typography): per-role fonts, case, tracking, slant, value fill and glow`  (Sonnet, M)  depends: 1

**Changes.** `ThemeFonts` (per-role, fallback chain, path-escape test per role); `TextLine`/`Readout` honour `case`, `tracking`, `slant`, `valueScale`, `valueFill` (static; animation comes in PR 9), `valueGlow`; caches; SDK `Theme.Type`.
**Tasks.** Font resolver + tests; component changes behind default-off paths (byte-identical v1); tracking keeps equal-width digits; glow cache; docs "Fonts and licences"; bundle fonts + OFL texts for the eight themes (`assets/fonts.md`).
**Acceptance.** Type specimens of Obsidian (Bodoni values, tracked caps labels), Rigline (gradient digits), Ember (slanted glow), Carbon (mono values) match mocks; missing font falls back with one warning; v1 goldens unchanged.
**Risks.** Variable font axis availability per family (S3); `SKFont` kerning vs manual tracking; installer size.

## PR 4: `feat(gauge): new styles and modifiers; stats sparkline and status; clock options`  (Sonnet, L)  depends: 1, 3

**Changes.** `Gauge` split per style; `Meter, Tach, Marker, ThickBar` and all modifiers of `02 §4.8`; `Chip`, `Divider`, `Sparkline` components; Stats history buffer + status chip + fraction thresholds; Clock `ShowSeconds, ShowWeek, Subtitle, BlinkSeparator, Header`; `colors.ramp/accent2/ink` plumbing; `GaugeOptions` additions.
**Tasks.** One sub-checklist per style with a golden; component caches; Stats ring buffer sized by `historySeconds`; Clock header layout; docs for the widget options; SDK minor version bump and API compatibility check (no signature changes).
**Acceptance.** Gauges of all eight themes match the mocks (segmented, tach+redline, dial+needle, thickbar, marker, gradient+glow, meter+scale); sparkline appears only on cards big enough; no allocation per paint in steady state (verify with a counter or the existing perf harness).
**Risks.** Component statefulness (caches) vs the "keeps no state" doc; Stats at 1x1 must not try to show a sparkline.

## PR 5: `feat(background): theme backgrounds, pairing, scrim and manifest pairsWith`  (Sonnet, M)  depends: 1

**Changes.** `BackgroundManifest.PairsWith` (+ `docs/schemas/v1/shader-manifest.json`, additive); `BackgroundPlan` precedence (`02 §4.11`, `07 §2`); config `background: {"type":"none"}`; static image/gradient/colour layer; scrim; `PairingIndex`; `ThemeCatalog`/`BackgroundCatalog` (read-only listings); add `meta` (name, description, mode) to the three existing themes and **no** `background.default` (their pixels and behaviour must not change); log line for the resolved background.
**Tasks.** Plan table tests; image decode with size cap; fallback on compile failure; docs; pairing symmetry test for built-ins (will pass once PR 6/7 land).
**Acceptance.** v1 configs unchanged; a theme with `background.default` selects it when the config has no `background`; `type: none` shows the plain colour; precedence table fully tested.
**Risks.** Decision D7 (absence semantics). Static image memory (decode once at layer scale).

## PR 6: `feat(background): horizon, spectrum, contour, nebula, streams, ripple`  (Sonnet, M)  depends: 5

**Changes.** Add `assets/shaders/{six}/` to Builtin; register; docs; site posters.
**Tasks.** Run `urdeck-cli shader validate ... --fail-on-warnings` (first real run!). Fix findings. Render samples with the host and replace `sample.png`. Panel check at `scale` 0.5: log numbers into `docs/perf/theme-look.md`. Check the `atan` seam / banding / large-time artefacts (`05 §1`).
**Acceptance.** Validator clean; all six render on the panel at <= the built-ins' cost (`docs/perf/backgrounds-spike.md` ballpark: about 3 % of a core); Nebula measured and tuned.
**Risks.** GPU vs CPU differences; Nebula cost.

## PR 7: `feat(background): embers, plotter, shapes, warpgate, equalizer, vortex`  (Sonnet, M)  depends: 5
Same as PR 6 for the other six. Extra: Shapes is the heaviest per pixel: measure first; Warp Gate and Vortex `atan` seam check.

## PR 8: `feat(page): pattern layer and motion policy`  (Sonnet, M)  depends: 2

**Changes.** `PatternRenderer` (tile bitmap shader; dots, grid+major, lines, hatch, scanlines; static), drawn in the underlay; `MotionPolicy` (theme x user x OS); SDK `Theme.Motion`; widgets respect it (Clock separator blink, flap).
**Acceptance.** Carbon dots, Blueprint grid with majors, Ember hatch match mocks; pattern tile periodicity test; `reduceMotion` truth table tests.
**Risks.** Tile seam at non-integer cell size: snap tile to whole pixels and scale the matrix.

## PR 9: `feat(motion): animated borders, value fill and pattern scroll`  (Opus, L)  depends: 8, 3, 2, S2

**Changes.** The S2 outcome: animated conic border and `valueFill.animate` and `pattern.scroll` through the frame clock; `IsAnimating` from theme; `reduceMotion` user setting in `urdeck-config.json`; frame budget guard; perf doc.
**Acceptance.** Rigline with 8 cards stays inside the budget of `03 §5`; with `motion.level` `reduced`/`off` everything stops and the deck still looks finished; display-off stops the clock (existing behaviour).
**Risks.** This is the performance risk of the whole project. If it cannot meet budget, ship Rigline with static border and animated value fill only, and record why.

## PR 10: `feat(icons): theme icon sets, themed weather and glyph shortcuts`  (Sonnet, L)  depends: 1, 4

**Changes.** `IconSet` loader and validator rules; built-in `outline` set (the seven seeds in `assets/icons/outline/` plus the remaining canonical keys, **drawn**: this is an art task, see below); `ThemedIcon`; Weather resolution order; Shortcut `theme:<key>`; `icons.stroke/tint/animated`.
**Tasks.** The seeds cover 3 of the 13 weather keys and 4 of the 24 app keys, so **10 weather + 20 app (+ optionally 6 ui) icons are still to draw**, in the style of the seeds (24 grid, stroke only, round caps, no transforms); 10b (optional, later) per-theme sets.
**Acceptance.** Weather widget with each theme shows themed icons for the keys present and Meteocons for the rest; a theme with `animated:false` shows the poster frame; icon validator catches every forbidden construct.
**Risks.** Icon art volume (hand-drawing 36 icons). Mitigation: only the 13 weather + 8 most common app glyphs are needed for the first release; the rest fall back.

## PR 11: `feat(cli): theme validate, render, init, schema`  (Sonnet, M)  depends: 1 (can run in parallel with 2-10)

**Changes.** `urdeck-cli theme ...` per `08 §1` including all THEME codes with fixtures; `theme render` uses `PageRenderer` + sample page; SARIF/JSON output as for shaders; CI step.
**Acceptance.** Each code has a failing fixture and a passing one; built-ins validate clean; `render` produces the same pixels as the goldens harness for the same inputs.

## PR 12..16: the themes (register each in `ThemeStore.BuiltInNames`, add fonts, goldens, site deck)

Each theme PR: move the draft from `assets/themes/<n>/theme.json` to `src/UrDeck.Engine/Themes/Builtin/<n>/theme.json`, tune on the panel, record final values and why, add goldens (1100x1920 and 1100x3840), add the site deck config, run `theme validate`, side-by-side picture with the mock.

| PR | Themes | Needs | Notes |
|---|---|---|---|
| 12 | **Carbon**, **Paper** | 2, 3, 4, 8 | cheapest, no animation; Paper needs `meter` scale |
| 13 | **Prism**, **Obsidian** | 2, 3, 4 | Prism ships "frostless" (D1); Obsidian needs `marker` and circle compact |
| 14 | **Ember**, **Blueprint** | 2, 3, 4, 8 | Ember tach+redline, Blueprint dashed + outside marks + dimension divider |
| 15 | **Pop** | 2, 3, 4 | hard shadow (underlay), `tint: cycle` |
| 16 | **Rigline** | 2, 3, 4, 8, 9 | animated; last because it is the budget test |

## PR 17: `docs(site): gallery, themes and backgrounds pages, docs rewrite`  (Sonnet, M)  depends: 12-16, 6, 7
Everything in `08 §3-4` that was not done inside the earlier PRs; media render pass on Windows; `MEDIA-CHECKLIST.md` ticked.

## PR 18: `feat(editor): preview renderer and catalog seams`  (Sonnet, S-M)  depends: 5, 11
`PreviewRenderer`, `ThemeCatalog`, final shape of `PairingIndex` as consumed by the future editor; no UI.

---

## Spikes recap

| ID | Question | Time-box | If it fails |
|---|---|---|---|
| S1 | Page underlay below widget surfaces (cost, z-order, input) | 1 day | option A with capped bleed |
| S2 | Cheap animated border (compositor mask or cached ring blits) | 1-2 days | static Rigline border |
| S3 | Variable fonts + installer size + licences | 0.5 day | static instances (more files) |

## Suggested order of merge
0, 1, 5, 11 → 2, 3 → 4 → 6, 7 → 8 → 10 → 12, 13, 14, 15 → 9 → 16 → 17 → 18.
(Shaders and CLI do not block the card work; Rigline last.)
