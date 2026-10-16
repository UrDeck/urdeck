# Look-and-feel work: status and how to pick it up

**Updated 2026-10-15.** The other files in this folder are a snapshot of the original design handoff and are not edited.
This file says what has been built, what changed on the way, and what is next. Where it disagrees with them, this file
and the OpenSpec specs (`openspec/specs/`) win.

## How to start a session on any remaining item

1. Read this file, then the package files its row points to (the `02` to `11` numbers below are files in this folder).
2. Read `docs/design/theme-format-2-deferred-keys.md` (keys set aside, and decisions the spikes made) and the combined
   spike notes in `docs/perf/theme-look.md`.
3. Run `/opsx:explore <item>` when the design is open, or `/opsx:propose <item>` when it is settled (the column says which).
4. One change per branch and pull request; `AGENTS.md` has the workflow. Tick `tasks.md` only when verified.

## Package work items against what exists

| Package item | Change | State |
|---|---|---|
| PR 0: spikes S1 underlay, S2 animated border, S3 fonts | none (notes only) | **Done.** `docs/perf/theme-look.md`; prototype code is on local branches `spike/theme-look-s1`, `-s2`, `-s3` |
| PR 0: golden-image harness (`GoldenImage.Compare`, `--update-goldens`, sample page) | `theme-golden-harness` | **Done**, archived (#55). Goldens for the three v1 themes and a dock page; update with `URDECK_UPDATE_GOLDENS=1` (`CONTRIBUTING.md`, "Golden images") |
| PR 1: format 2 model | `theme-format-2` | **Done**, archived (#49) |
| PR 5: backgrounds, pairing, plan, scrim, catalogs | `theme-backgrounds` | **Done**, archived (#52). `PreviewRenderer` (PR 18) is not built |
| PR 6, PR 7: twelve shaders | `builtin-shaders` | **Done**, archived (#53). Site posters and loops not made |
| PR 3: typography (per-role fonts, case, tracking, slant, value fill, glow) | `theme-typography` | **Done**: per-role fonts with the fallback chain, `typography.opticalSize`, the weight warnings, `Theme.TextStyle` (SDK 0.9.0), drawn by `Readout` and `TextLine`. Findings for the theme changes are in its design ("Findings during apply") |
| PR 2: card chrome, 1 of 4: painter split, shape, fill, tint, border, hairlines, cached gradient backs | `card-chrome-back` | **Proposed. Next: apply. Model: Opus** (the painter's shape, the cache, the per-card look through host and snapshot). Decisions for all four: `docs/design/card-chrome.md`; numbers: `docs/perf/card-chrome.md` |
| PR 2: card chrome, 2 of 4: highlight, sheen, accent strip, corner marks on the card | `card-chrome-front` | **Proposed**; apply after `card-chrome-back`. **Model: Sonnet** (additive pieces against goldens) |
| PR 2: card chrome, 3 of 4: shadow, glow and outside marks as sprites, reach limit, press, dock | `card-underlay` | **Proposed**; apply after `card-chrome-front`. **Model: Opus** (compositor work, measured on the panel) |
| PR 2: card chrome, 4 of 4: compact cards (dock slots, 1x1), circle, partial overrides | `card-compact` | **Proposed**; apply last. **Model: Sonnet** (settled rules against goldens) |
| PR 8: pattern layer and motion policy | `page-pattern`, later `motion` | After card chrome. Pattern uses a page-sized surface; the memory cost is accepted |
| PR 4: gauge styles and modifiers | `theme-gauges` | **Proposed.** Does not need card chrome. SDK `GaugeStyle` gains `Meter, Tach, Marker, ThickBar`; adds one key, `gauge.layout` (the board draws Carbon's, Paper's and Pop's bars with the value above the bar) |
| PR 4: sparkline, status chip, divider, well, clock options, weather sections | `theme-widget-parts` | **Proposed**; apply after `theme-gauges`. Stats keeps its own short history (a hub-provided one is the alternative); the Clock header and per-second options are opt-in |
| PR 10: icon sets, themed weather and glyph shortcuts | `theme-icons` | After gauges. About 30 icons still to draw (`04`) |
| PR 11: `urdeck-cli theme validate`, `render`, `init` | `theme-cli` | Independent; can start any time. Carries the validator findings listed below. The sample page is in the engine tests (`Support/SamplePage.cs`); the CLI has to move or share it |
| PR 9: animated border, value fill, pattern scroll, `reduceMotion` | `motion` | After card chrome and pattern; **Opus**. Spike S2 decided the mechanism |
| PR 12 to 16: the eight themes | one change per pair | After the knobs they use. Order: Carbon and Paper; Prism and Obsidian; Ember and Blueprint; Pop; Rigline last |
| PR 17: docs, site gallery, media | with each theme, rest at the end | The site needs a deck and pictures per theme (`site/MEDIA-CHECKLIST.md`) |
| PR 18: preview renderer and catalog seams | `theme-preview` | `ThemeCatalog`, `BackgroundCatalog` and `PairingIndex` exist; `PreviewRenderer` does not |

## What changed on the way (read these before relying on the package)

- **Keys:** only the keys the eight drafts use were built. The rest, with definitions, are in the deferred-keys document.
  `card.accentStrip.glow` and `card.compact.cornerMarks` were added back (Rigline needs the former; the latter was missing).
- **Drafted themes are test fixtures** (`tests/UrDeck.Engine.Tests/Fixtures/themes/`), not built-ins. Each moves to
  `src/UrDeck.Engine/Themes/Builtin/<name>.json` (flat layout) in its theme change. Four edits were made to the drafts: Prism lost
  `card.blur`, Rigline lost `valueFill.animate` and `pattern.scroll`, and Pop takes its numerals from Archivo at weight 900.
- **Gauge styles** `meter`, `tach`, `marker`, `thickbar` are accepted in a theme and draw as `ring` until the gauge change.
- **Background:** an absent `background` means "the theme's choice" (D7), `{"type":"none"}` opts out, and `motion.level` does
  not touch the background (the package contradicted itself; `02` won).
- **Underlay (S1):** compositor sprites with a nine-grid, not a page-sized canvas. **Animated border (S2):** a sprite with a stepped
  rotation at 15 steps per second, a host constant.
- **Installer size is not a concern.** CPU and memory are.
- **Goldens guard v1 pixels** (`theme-golden-harness`): `default-dark`, `default-light`, `glass` at 1100x1920 and a dock page, Windows
  only. Any change that draws must leave them unchanged or say why in its pull request.
- **Typography as built:** the SDK member is `Theme.TextStyle` (`Theme.Text` is the text colour). `valueScale` is limited to the
  fitted size, and the mocks draw numerals smaller than the fitted readout, so the themes need no padding workaround. The value glow
  costs 2 to 3.7 times a plain readout (over the package's 2x; 0.19 ms per repaint with the copy cached). A slanted value leans past
  its rectangle (about 50 px for Ember on a 4 x 2 clock); Ember and Rigline should check it. `docs/perf/theme-look.md`, "Value glow
  and fill".
- **Weight warnings on the drafts:** Ember's and Rigline's body weight 500 is on a Bold-only file, so each logs one warning on load.
  Their theme changes either use a real 500 file or set the weight the file has.
- **The shaders:** five of the twelve had periods that did not divide the 4-hour time wrap and were fixed in their sources.
  Nebula and Shapes cost no more than the others on the GPU, so no default was lightened (`docs/perf/builtin-shaders.md`).

## Open design points (decide in the explore for the change that owns them)

- **Card chrome:** decided in `docs/design/card-chrome.md`. Two decisions changed: the engine limits a shadow's or glow's reach to
  the gap when the card fill is translucent (Prism's 193 px showed as a cloud through its neighbours, and its picture was over both
  underlay budgets), and a back with a gradient or a sheen is cached as an image per size (a gradient over a card costs about 1.1 ms
  in software). The press carries the sprite. The owner should look at `prism-reach.png` (branch `spike/card-chrome-paint`).
- **Typography:** `opsz` was decided in `theme-typography` (`opticalSize: auto`: the display cut for values, the pixel size for the
  rest). Still open: sharing the base typeface between page and dock (a second family measured 2.3 MB on the panel; optional), and
  whether a slanted value should count its lean in the fit and the unit gap (Ember, Rigline; see that change's design).
- **Motion:** each of `motion.level`, a user `reduceMotion`, display off and a hidden deck must stop the border animation.
  Whether a reduced-motion setting should swap a shader for the theme's still background (the package said both yes and no).

## Validator findings the theme CLI must carry

A weight outside a variable font's range, or a weight a static font does not have, is now one warning per role when the theme
loads (`theme-typography`); the CLI should report the same without the host. Shadow or glow reach beyond the gap over a
translucent fill (the engine limits it, `docs/design/card-chrome.md`); text contrast on the card fill (under 4.5:1), and on every
`cardTints` entry (a tinted card keeps `Theme.Text`); outside corner marks with `|inset| + thickness` over half the gap; a theme's default background that is not in its own `pairsWith` list;
`icons.set` and `meta.pairsWith.backgrounds` naming things that do not exist; keys that are unknown (with "did you mean").

## Not verified, in case it matters later

- Whether the border's 15-step rotation and the underlay's page slide look smooth: only still captures were taken (S1, S2).
- A shader run on the GPU past the 4-hour wrap; only CPU renders at 14,399 s were made.
- The sprite underlay at display scalings other than 100 %.
- Value fill animation and pattern scroll were never tested (deferred).

## Separate backlog items found on the way

- The unchanged host holds about 54 MB more after 20 theme switches, even after a collection; the first switch alone adds about 21 MB.
  Not caused by this work; it will show more once themes switch visibly.
- Website posters and loops for the twelve backgrounds; a "Pairs with" badge on the site (package `08`).
