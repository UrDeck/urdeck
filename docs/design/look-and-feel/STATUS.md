# Look-and-feel work: status and how to pick it up

**Updated 2026-10-19** (`themes-carbon-paper` proposed; the order of the remaining work is in "Plan from here"). The other files in this folder are a snapshot of the original design handoff and are not edited.
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
| PR 2: card chrome, 1 of 4: painter split, shape, fill, tint, border, hairlines, cached gradient backs | `card-chrome-back` | **Done**, archived. `ResolvedCardStyle`, `CardLook`, `CardOrder`, `CardChrome`, `SizedCardChrome`. Goldens for Ember, Blueprint, Obsidian, Pop (page and dock); every draft within 1.5x `default-dark`; the cached backs cost about 8 to 9 MB on the panel, accepted (`docs/perf/card-chrome.md`, "As built"). Looked at on the panel with Ember: approved. Findings in its design (`openspec/changes/archive/2026-10-18-card-chrome-back/design.md`): the dash is fitted per side with a dash on every corner, the error card wipes the shape only. Decisions for all four: `docs/design/card-chrome.md` |
| PR 2: card chrome, 2 of 4: highlight, sheen, accent strip, corner marks on the card | `card-chrome-front` | **Done**, archived. Highlight as a path per size drawn after the clip; sheen and strip in the cached back. Goldens for Carbon, Paper, Prism, Rigline; Obsidian redrawn. Prism paints at 1.83x `default-dark` (budget 1.5x, its back alone 1.39x): **accepted by the owner** (`docs/perf/card-chrome.md`, "As built (card-chrome-front)"). Looked at on the panel with Obsidian and Carbon: approved. Findings in its design (`openspec/changes/archive/2026-10-18-card-chrome-front/design.md`) |
| PR 2: card chrome, 3 of 4: shadow, glow and outside marks as sprites, reach limit, press, dock | `card-underlay` | **Done**, archived. `CardUnderlay` (one routine for the window and the snapshot), `UnderlayPicture` (nine-grid or exact size, half size when all blurred, kept on the style), the host's `UnderlayView` (one sprite per card, a PNG decoded into a `LoadedImageSurface`). Goldens for Paper, Pop, Prism, Rigline (page and dock), Blueprint and Carbon. **Memory budget changed by the owner from 2 MB to 10 MB** for a theme's pictures: Paper measures +7.7 MB, mostly the decoding path; a `CompositionDrawingSurface` with a Direct3D device of the host's own cost +30 MB and was dropped (`docs/perf/card-chrome.md`, "As built (card-underlay)"). Warm build 1.3 to 12.6 ms. Looked at on the panel by the owner (press, slide, Prism's limited shadow, 150 %): approved. Findings in its design (`openspec/changes/archive/2026-10-18-card-underlay/design.md`) |
| PR 2: card chrome, 4 of 4: compact cards (dock slots, 1x1), circle, partial overrides | `card-compact` | **Done**, archived. `ResolvedCardStyle.Compact` (the compact values merged key by key, a style of its own with its own pictures and caches; the style itself when the theme sets none), `ForCard(item)` picks it for a 1x1 card, the dock uses it for every slot; `ThemeResolver.Resolve(..., compact: true)` gives the dock widgets the compact radius and border width, a 1x1 widget on a page keeps the page theme. Goldens for Obsidian, Blueprint, Prism and Rigline redrawn (the sample page has four 1x1 shortcuts), docks for Obsidian and Blueprint added. Looked at on the panel by the owner (Obsidian's circular dock, Rigline's tile strips): approved. Memory and cost: no new pictures beyond a compact style's own, made only for a theme that sets `card.compact` and draws outside its cards; the compact style is dropped with its parent. Not built: a compact padding (`docs/design/theme-format-2-deferred-keys.md`) |
| PR 8: pattern layer and motion policy | `page-pattern`, later `motion` | **Done** (pattern; the owner looked at Carbon on the panel at 100, 125 and 150 %: no seams). `PatternTile` (one routine for the host and the snapshot: whole-pixel spacing and lines, a hatch snapped within a degree, a block of whole repeats), the host's `PatternLayer` (compositor sprites over one decoded block, between the background and the pages). Carbon, Blueprint, Ember and Rigline goldens redrawn. On the panel +2 to +6 MB private (up to +8 MB working set), 6 ms to remake (`docs/perf/page-pattern.md`); the page-sized surface (+33 MB) is not used. `motion` is still to come |
| PR 4: gauge styles and modifiers | `theme-gauges` | **Built.** `GaugeStyle` Meter, Tach, Marker, ThickBar; `GaugeTheme` on `Theme.Gauge` (SDK 0.10.0), `Accent2`, `Ink`, `Ramp`; `gauge.layout`. v1 draws via `LegacyGauge`, byte-identical. A tach has default ticks and a redline. Findings: an arc with ticks and an inside label needs extra readout room below the middle (Ember, Blueprint); the drafts' hex colours such as Ember's tick `#ffffff1c` read as AARRGGBB (yellow), check in the theme change; the track is segmented as well as the fill. Cost: inside 2x without a glow, 3.5x to 5.1x with one (`docs/perf/theme-look.md`); on the panel a tach is +3 MB over a ring |
| PR 4: sparkline, status chip, divider, well, clock options, weather sections | `theme-widget-parts` | **Done**, archived. SDK 0.11.0: `Chip`, `Divider`, `Well`, `Sparkline`; `Theme.Chip`, `Divider` (default is Weather's old rule, byte-identical), `Well`; `GaugeTheme.Sparkline` and `Status`; `GaugeOptions.History` and `Status`. The gauge carves a status row (label left, chip right) and a sparkline strip out of its rectangle. Stats keeps a per-position history ring (at most 600 floats, only while the sparkline is on and the position is 0.9 of a cell tall) and chooses the status; the Clock has `header`, `subtitle`, `showWeek`, `showSeconds`, `blinkSeparator` (opt-in; a per-second repaint costs about 10 ms of CPU a minute); Weather's 4x4 rule, forecast wells and condition chip use the parts. Pop's goldens redrawn (pill); the rest unchanged. Carbon and Paper rendered beside the board. Findings in its design (`openspec/changes/archive/2026-10-18-theme-widget-parts/design.md`): Rigline's fixture has no `well`; the status works at single positions; the blink draws a space; the dimension label is not printed yet |
| PR 10: icon sets, themed weather and glyph shortcuts | `theme-icons` | **Implemented** (pending the pull request). SDK 0.13.0: `IIconSet`, `IconGlyph`, `IconTheme` on `Theme.Icons`, `ThemedIcon`, `ImageTile`'s glyph key. The engine parses a whitelist of SVG (`IconSvgParser`), reads sets through a manifest (`IconSetReader`, `IconSetStore`) and ships `outline` (Lucide 1.60.0, copied once). Weather draws static glyphs under a theme that names a set (no Lottie, no posters, no frames; the `ramp` tint maps -10 to 40 C); Shortcut has `theme:<key>` and a glyph by target kind when the icon service gives up. `icons.animated` is gone. Lint: `THEME044` is real, `THEME070` to `THEME076` are new. Carbon and Paper name `outline` again; the draft themes whose fixtures name it (Obsidian, Ember, Blueprint, Pop, Prism, Rigline) were redrawn as well, the v1 themes are byte-identical. **Not done:** the panel look at 100, 125 and 150 % and the host CPU and memory numbers (`docs/perf/weather.md`, `docs/perf/theme-look.md` have the harness numbers). **Prism's open icon question:** its draft sets `animated: true` with `set: "outline"`, now outline glyphs in the text tint; its own theme change decides whether it wants Meteocons (drop `set`). The design copies under `docs/design/look-and-feel/assets/themes` still carry `animated`, as the handoff snapshot |
| PR 11: `urdeck-cli theme validate`, `render`, `init` | `theme-cli` | **Implemented** (pending the pull request), `theme validate`, `render`, `init` and `schema`; the validator findings below are built (`THEME001` to `THEME090`, `docs/themes.md`, "Checking a theme"), contrast is owned here (`THEME060` to `THEME062`) and the built-in themes check clean. `SamplePage` moved to `src/UrDeck.Preview`. Was: to run in parallel (another worktree). Independent. Carries the validator findings listed below and owns the contrast rule (`themes-carbon-paper` checks contrast by hand and does not edit `ThemeDefinition.Rules.cs`). The sample page is in the engine tests (`Support/SamplePage.cs`); the CLI has to move or share it, and `GoldenTests.cs` is the only overlap with the theme change. Whichever lands second must validate Carbon and Paper clean |
| PR 9: animated border, value fill, pattern scroll, `reduceMotion` | `motion` | After card chrome and pattern; **Opus**. Spike S2 decided the mechanism |
| PR 12: Carbon and Paper | `themes-carbon-paper` | **Done**, archived (#66). Carbon and Paper are built in with Instrument Sans and JetBrains Mono; the fixture copies are gone and their goldens redrawn in the real fonts (`carbon-dock.png` added). Carbon's dot grid waits for `page-pattern`, which redraws Carbon's goldens and gets a second panel look. Compared on the panel with the board; the differences that only the engine can fix are the follow-ups below |
| PR 13 to 16: the other six themes | one change per pair | After the knobs they use. Order: Prism and Obsidian; Ember and Blueprint; Pop; Rigline last |
| PR 17: docs, site gallery, media | with each theme, rest at the end | The site needs a deck and pictures per theme (`site/MEDIA-CHECKLIST.md`) |
| PR 18: preview renderer and catalog seams | `theme-preview` | `ThemeCatalog`, `BackgroundCatalog` and `PairingIndex` exist; `PreviewRenderer` does not |

## Plan from here (decided 2026-10-19)

```
now        themes-carbon-paper          Carbon and Paper built in, Carbon without its dots
parallel   theme-cli                    another worktree; owns the contrast rule
done       page-pattern                 Carbon's dots are drawn (goldens redrawn); Ember and Blueprint are unblocked
           theme-icons                  every theme gets themed icons (done, see PR 10)
any time   Prism + Obsidian, Pop        need nothing unbuilt (icons fall back to the widgets' own), slot in between
last       motion (Opus), then Rigline  Rigline needs the animated border
```

- **No theme picker for now.** The approach for the configuration editor is not decided, and a one-off theme or background
  picker would be thrown away by it. Themes are chosen with `theme` in `urdeck-config.json`; each theme's site page shows the
  snippet. Anything that describes themes stays in the engine (`ThemeCatalog`, `BackgroundCatalog`, `PairingIndex`), where a
  future editor can use it. The editor's approach deserves its own explore.
- **Follow-ups found on the panel (`themes-carbon-paper`, against the design board):**
  - Stats: the board's card order is label (top left), value, sparkline, bar; the engine's stacked layout puts the value above the bar and the sparkline only when the card is tall enough and has history.
  - Stats: the value is too large in a 2x2 Stats card; a value is limited to its fitted size, so `valueScale` only shrinks (and would shrink every widget).
  - Clock: the board pins the time bottom left, with a header row above; the engine centres the time. There is no alignment key.
  - Clock: Paper's colon is the accent colour on the board; no theme key colours the separator.
- **Follow-ups found in the explore:** a gauge fill that follows the status level (Carbon's bar would turn amber at `WARM`;
  `colorBy` has only `accent` and `ramp` today). `valueScale` above 1 has no effect (a value is limited to its fitted size), so
  the drafts that set it should drop it in their theme changes.

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
- **Card chrome as built (underlay):** a shadow without `color` is black at 40 %, without `blur` a hard edge; a glow without `color`
  takes `colors.cardBorder`. The reach limit scales offset and blur together, not the spread, and logs one information line per
  loaded theme. The press binds the sprite to the card's visual with expressions; `WidgetView` did not change. The pictures are
  made in physical pixels, so a display scaling changes nothing but their stretch.
- **Card chrome as built (back):** a theme with no card value of format 2 draws exactly as before (the goldens prove it); any
  such value puts it on the new path, where a border of 2 px or less is a whole pixel. A dashed border has a dash centred on
  every corner (fitted per side, not to the perimeter as the design said). The error card wipes only the card's shape. A back
  with a fill gradient is an image per (tint, size), border included: about 8 to 9 MB for a gradient theme on the panel,
  accepted by the owner over the design's 5 MB. Defaults are the schema's (`cut` 0.14, fill angle 90, border angle 135).
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
  in software). The press carries the sprite. Prism's limited shadow was looked at on the panel and approved (`card-underlay`);
  its theme change may still widen the gap.
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

- Whether the border's 15-step rotation looks smooth: only still captures were taken (S2). The underlay's page slide was looked at on
  the panel (`card-underlay`).
- A shader run on the GPU past the 4-hour wrap; only CPU renders at 14,399 s were made.
- Value fill animation and pattern scroll were never tested (deferred).

## Separate backlog items found on the way

- The unchanged host holds about 54 MB more after 20 theme switches, even after a collection; the first switch alone adds about 21 MB.
  Not caused by this work; it will show more once themes switch visibly.
- The cached card backs of a gradient theme cost about 8 to 9 MB on the panel (`docs/perf/card-chrome.md`), accepted; if memory ever
  matters, the fill can move to a `CompositionLinearGradientBrush` under each card (`docs/design/card-chrome.md` 10).
- The underlay's memory (+7.7 MB for Paper, mostly the PNG decoding path): XAML `Image` elements with a `WriteableBitmap` and
  `Image.NineGrid` (the XAML device, no decoder) were not tried, and may cost less (`docs/perf/card-chrome.md`, "As built
  (card-underlay)").
- Website posters and loops for the twelve backgrounds; a "Pairs with" badge on the site (package `08`).
