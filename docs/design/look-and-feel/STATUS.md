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
| PR 3: typography (per-role fonts, case, tracking, slant, value fill, glow) | `theme-typography` | Next. Propose directly |
| PR 2: card chrome (shape, borders, shadow, glow, highlight, sheen, marks, compact, tint) | `card-chrome` | Next. **Explore first** (see "Open design points") |
| PR 8: pattern layer and motion policy | `page-pattern`, later `motion` | After card chrome. Pattern uses a page-sized surface; the memory cost is accepted |
| PR 4: gauge styles, sparkline, status chip, divider, well, clock options | `gauges-and-widgets` | After typography. SDK `GaugeStyle` gains `Meter, Tach, Marker, ThickBar` here |
| PR 10: icon sets, themed weather and glyph shortcuts | `theme-icons` | After gauges. About 30 icons still to draw (`04`) |
| PR 11: `urdeck-cli theme validate`, `render`, `init` | `theme-cli` | Independent; can start any time. Carries the validator findings listed below |
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
- **The shaders:** five of the twelve had periods that did not divide the 4-hour time wrap and were fixed in their sources.
  Nebula and Shapes cost no more than the others on the GPU, so no default was lightened (`docs/perf/builtin-shaders.md`).

## Open design points (decide in the explore for the change that owns them)

- **Card chrome:** how `WidgetPainter` splits into back and front passes while a v1 theme stays byte-identical; whether the press
  effect carries the shadow sprite; dashed border on a chamfered card (a nine-grid cannot stretch a dash); the dock's own underlay;
  a translucent card showing its neighbours' shadow when the reach exceeds the gap (a validator warning, not an error; Prism does it).
- **Typography:** drive the `opsz` axis from the role's size (Bodoni Moda sits at its text cut otherwise); sharing the base typeface
  between page and dock saves about 0.9 MB per family (optional).
- **Motion:** each of `motion.level`, a user `reduceMotion`, display off and a hidden deck must stop the border animation.
  Whether a reduced-motion setting should swap a shader for the theme's still background (the package said both yes and no).

## Validator findings the theme CLI must carry

A weight outside a variable font's range, or any weight on a static font, is silent today; shadow or glow reach beyond the gap over a
translucent fill; text contrast on the card fill (under 4.5:1); a theme's default background that is not in its own `pairsWith` list;
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
