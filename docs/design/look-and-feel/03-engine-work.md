# 03 Engine and host work

How the format-2 knobs become pixels. Read `01` first. File names are where the work most likely lands; confirm.

## 1. Architecture decisions (proposed; each has a spike or decision tag)

### 1.1 A `CardChrome` in the engine, not in widgets
New `src/UrDeck.Engine/Rendering/CardChrome.cs` replaces the body of `WidgetPainter.DrawCard`. It takes
`(SKCanvas, SKPath/SKRoundRect shape, ResolvedCardStyle, int cardIndex, TimeSpan animationTime, bool compact)` and exposes
`DrawBack(...)` (fill, sheen, border) and `DrawFront(...)` (highlight, inside corner marks). `WidgetPainter.Paint` becomes:

```
shape = CardShape.Build(size, theme)          // SKPath for chamfer/circle, SKRoundRect fast path for round
CardChrome.DrawBack(canvas, shape, style)
canvas.Clip(shape)
widget.Render(context)
CardChrome.DrawFront(canvas, shape, style)    // after content, still clipped for the highlight; unclipped for marks
```
Keep the **fast path**: a v1 theme (no new keys) must take exactly today's `SKRoundRect` code path and produce identical
pixels. `CardShape` returns a small record `{ SKRoundRect? Round; SKPath? Path; SKRect Bounds }`.

The SDK's `Theme` object (what widgets see) gains only what widgets need (see `04`): resolved gauge options,
typography role features, icon set handle, chip/divider styles, `Motion` level, `Time` helpers. Card chrome properties
stay **engine-internal** (`ResolvedCardStyle`), so the MIT SDK API does not grow with host-only concepts.

### 1.2 Resolved model (`ThemeResolver`)
Add `ResolvedTheme` (engine) alongside the SDK `Theme`: `Theme` for widgets; `ResolvedCardStyle`, `ResolvedPattern`,
`ResolvedBackgroundPlan`, `ResolvedMotion` for the host. Resolution is per surface size (cell px), exactly like today.
Everything resolved to **pixels** and prebuilt Skia objects where cheap (paints, shaders that do not depend on size).
Dispose together with `Theme`. No allocations in the paint path except unavoidable `SKPath`s (cached per size).

### 1.3 Bleed: shadows, glow, outside marks (**spike S1**)
Today each widget surface is exactly card-sized, so outside-the-card drawing has nowhere to go. Options:

| Option | How | Pros | Cons |
|---|---|---|---|
| **A. Larger surfaces** | each `SKXamlCanvas` = card + bleed margin B on every side; XAML margin = -B; hit test limited to the card | simple mental model; shadow moves with press scaling | memory: +(2B/w)(2B/h) per card, CPU raster cost scales with area; a 4x4 card on 1100 px with B=0.4 cell is +50 % pixels. Violates the perf notes in `docs/perf/theme-card-paint.md`. |
| **B. Page underlay (recommended)** | one extra surface per page, drawn **beneath** the widget surfaces, painting all shadows/glows/outside marks for every card using the layout rects; redrawn only on layout/theme change (and never per widget tick) | zero cost to widget surfaces; shadows are static (cheap); also hosts `pattern` | press scale (compositor) shrinks the card but not its shadow (acceptable, arguably nice); animated glow needs the underlay ticking (don't support animated glows in v1) |
| C. Cheat: no outside drawing | shadows/glow rendered **inside** the card edge (inner shadow) | trivial | wrong look for Pop (hard shadow must be outside) |

**Recommendation: B.** The underlay is also where `pattern` is drawn, so one new layer solves two features. In the live
host the underlay is a `SKXamlCanvas` inserted by `PageHost` below the widget elements and sliding with the page; in
`PageRenderer` (snapshots) it is just a draw pass before the cards. Spike S1 (≤ 1 day): confirm that (a) an additional
full-page `SKXamlCanvas` below the widgets costs under ~2 MB and does not disturb the page-slide animation or the
compositor press effect; (b) Z-order and hit testing (it must not take input). **If S1 fails** fall back to A, capped to
`B <= 0.25 cell`, with themes using hard shadows only.

Constraints that fall out of B (put them in the validator): shadows and glows are painted *under* every card so
they cannot cover another card's content; outside corner marks must lie in the gap, `|inset| + thickness <= gap/2`.

### 1.4 Animation: one clock, opt-in per feature
The host already has one frame clock at 30 fps that runs only while a widget reports `IsAnimating`. Theme-driven motion:

- `borderAnimate`, `valueFill.animate` need **every affected widget surface to repaint each frame**: that is a CPU
  raster of the whole card per frame at 30 fps for *every card on the page*. That is the dominant cost of Rigline.
- `pattern.scroll` repaints the underlay (one surface) at the clock rate.

Mitigations (all required before merging Rigline's animated border):
1. **Mask the animated gradient in the compositor instead of repainting the card (spike S2).** Draw the card normally
   once; draw the animated border as a *separate* small surface/visual (a ring-shaped mask over a rotating gradient
   brush using the system compositor, not Skia). If S2 is not feasible in the WinUI 3 + SKXamlCanvas stack, fall back to:
2. **Lower frame rate for theme animation:** 15 fps for `borderAnimate`/`valueFill`, 30 fps only for widget content that needs it.
   Render the animated border into a cached ring bitmap per angle step (e.g. 72 steps) and blit: cost per frame is
   one bitmap draw, not a gradient+stroke.
3. `motion.level reduced|off`, the user `reduceMotion` setting and display-off (existing `DisplayPowerWatcher`) all stop the clock.
4. Budget: **all animated theme features on a typical 4x8 page must stay under 8 % of one core at 30 fps and under
   +15 MB working set** on the reference machine (Pat's panel host). Measure with the method in `docs/perf/stats-gauges.md`
   and record in a new `docs/perf/theme-look.md`. If a feature busts the budget it ships behind `motion` defaults
   or is cut (decision D4).

### 1.5 Fonts: per-role
`ThemeFont(string Spec, Func<string,Stream?> Open)` becomes `ThemeFonts(IReadOnlyDictionary<TextRole,string> Specs, string Default, Func<string,Stream?> Open)`.
`ThemeResolver` creates typefaces per role (reusing a `SKTypeface` when specs are equal, as it clones per weight today),
and disposes them together. Missing file or family: warn once per theme, fall back along spec → default → bundled Inter.
The path-escape rejection is unchanged and **must be covered by a test for each role**.

Built-in themes embed their fonts as assembly resources (today only Inter + `Inter-OFL.txt`). Adding ~10 families
raises the installer by several MB: **use variable fonts where they exist, subset only if the licence allows (OFL
does, with a renamed Reserved Font Name where required; do not subset without checking each font's RFN)**, and ship each
OFL text next to the fonts. Decision D6 (embed vs. download on first use) in `10`; the default recommendation is embed.

### 1.6 Icons
`IconSet` loader in the engine: folder `icons/<set>/<group>/<key>.svg`. Avoid a new SVG dependency: icons are authored as
**simple stroke-only SVGs (`<path d>` and `<circle>`/`<line>`/`<rect>`, no transforms, no CSS, no gradients)** on a 24x24 viewBox
and loaded into cached `SKPath`s per key with `SKPath.ParseSvgPathData` (circles and rects are converted to paths by the loader).
The loader concatenates all shapes of a file into one path; anything else is ignored with one warning per file. See `04` for keys. Fallback chain and tinting rules there.

### 1.7 Pattern layer
`PatternRenderer`: builds a tile bitmap (pixel-snapped) for `dots/grid/lines/hatch/scanlines` at the page's cell size, caches by
`(kind, spacing, thickness, color, angle, size)`, draws via `SKShader.CreateBitmap(Repeat, Repeat, matrix)`. `grid` with
`majorEvery` uses a tile of `majorEvery` cells. Scroll: matrix translate by `scroll * time`, wrapped modulo the tile so there is no
precision drift over hours (same lesson as the shader's 4 h wrap).

### 1.8 Background selection (`BackgroundPlan`)
New `BackgroundPlan.Resolve(config.Background, theme.Definition.Background, motionLevel)` returns one of:
`{Shader(name, params, colors) | Static(image|gradient|color) | None}`. `BackgroundLayer` renders Static as a one-shot (no 30 fps
clock). Failure to compile a shader falls back to `Static` (today it falls back to the theme background colour; keep
that as step 4). **Precedence in 02 §4.11.** A new `Resolved` record is logged once: "Background: shader 'horizon' (theme default)".

### 1.9 Reduced motion
New `MotionPolicy` (engine): `Effective = Min(theme.Motion.Level, config.ReduceMotion→level, OsAnimationsEnabled ? Full : Off)`.
The host feeds `UISettings.AnimationsEnabled` (WinRT) and re-evaluates when the setting changes (**verify
a change event exists)**; otherwise poll on config reload. Widgets read `theme.Motion` (SDK) and must stop *looping* effects
when it is not `Full` (Clock flap keeps its single flip at `Reduced`, none at `Off`).

## 2. Changes by file (starting map)

| Area | File | Change |
|---|---|---|
| Definition | `Themes/ThemeDefinition.cs` | New nullable members/classes for every key in `02`; `MergeOver` and `Sanitize` for each; `meta`, `format` |
| Definition | new `Themes/ThemeDefinition.Card.cs` etc. | Split the file by group to keep it readable (partial class already) |
| Resolver | `Themes/ThemeResolver.cs` | Resolve to `ResolvedTheme`; per-role fonts; conversions; compact style; ramp/tint colours |
| Store | `Themes/ThemeStore.cs` | `BuiltInNames` extended (see §4); `ThemeFonts`; load `icons/<set>` lazily; keep "same name as built-in replaces it" |
| Painter | `Rendering/WidgetPainter.cs` | `CardChrome`, fast path, front/back |
| New | `Rendering/{CardChrome,CardShape,PatternRenderer,PageUnderlay,MotionPolicy,IconSet}.cs` | as above |
| Page | `Rendering/PageRenderer.cs` | underlay pass; card index for `tint`; compact detection; dock slots |
| Host | `PageHost.cs`, `WidgetView.cs`, `BackgroundLayer.cs`, `FrameClock.cs` | underlay element; animated chrome ticking; `BackgroundPlan`; reduceMotion |
| SDK | `Theme.cs`, `Components/*` | see `04` (additive, MIT, careful) |
| Config | `Config/*` | `reduceMotion`; `background: "theme"`/omitted semantics (the **absence** of `background` now means "use the theme's default", which changes behaviour for users with no background; see D7) |
| Backgrounds | `Backgrounds/Builtin/<12 new>` | files in `assets/shaders` |
| CLI | `tools/UrDeck.Cli` | `theme validate`, `theme render`, schema (see `08`) |

## 3. Resolver conversions (pseudo-code)

```csharp
float cell = widthPx / 4f;                      // as today
float Px(double? cells) => (float)(cells * cell);
// card
radiusPx    = Min(Px(card.radius), Min(w,h)/2)
cutPx       = Min(Px(card.cut), Min(w,h)/2)
shadow.blur = Px(shadow.blur); sigma = blurPx / 2f           // SKMaskFilter.CreateBlur(Normal, sigma)
dashOnPx    = Px(borderDash.on) ...
// text
tracking    = tracking.value * fontSizePx                    // em -> px
// gauge
sweep       = (start, span) degrees as given; v1 defaults 135/270
```
Round everything that is *a line width ≤ 2 px* to whole pixels at 1x to keep hairlines crisp (Carbon, Blueprint,
Obsidian rely on 1 px lines); log if a hairline resolves under 1 px (it will blur) at panel resolution.

## 4. Built-in set

Recommended: **keep `default-dark` (the baseline every theme merges over; also still the default) and `default-light` and `glass` as-is**, and **add the eight** as
built-ins. 11 built-ins is fine: the editor groups by `meta.mode`/tags. Decision D5: *replace* `glass` by Prism?
Recommendation: no. Keep glass for continuity (existing users, the site), and let Prism be the "grown-up glass". If Pat later
prefers replacement it is a one-line deletion plus a site deck swap.

`ThemeStore.BuiltInNames` order = display order in the editor: `default-dark, default-light, glass, prism, carbon, paper, rigline, ember, blueprint, obsidian, pop`.

## 5. Performance budgets and how to measure

| Item | Budget | How |
|---|---|---|
| v1 theme, any page | **no regression** (±2 % CPU, ±1 MB) | compare `docs/perf/stats-gauges.md` scenario before/after PR 1-4 |
| Static chrome (gradient/dashed/chamfer/shadow) per card repaint | ≤ 1.5× v1 card paint time | microbenchmark `WidgetPainter.Paint` for 1100x550 card, 100 iterations |
| Underlay | built once per layout/theme change; ≤ 20 ms; ≤ 2 MB | stopwatch + working set |
| Animated border, 8 cards, 30 fps | ≤ 5 % of a core, ≤ +8 MB | S2 |
| Pattern scroll | ≤ 1 % of a core | |
| Value glow / valueFill | ≤ 2× the readout draw time; cache the blurred glow per (string,size) | |
| Shaders (backgrounds) | CPU raster numbers in `05`; GPU on the panel is what counts | `docs/backgrounds.md` "Speed, scale and cost" |

Record results in `docs/perf/theme-look.md` (new), following the structure of the existing perf notes.

## 6. Tests (engine; run on Linux too: `dotnet test tests/UrDeck.Engine.Tests`)

1. **Golden snapshots**: render a fixed page (clock+stats+weather+shortcuts, fixed time and readings) with each theme at 1100x1920 using `PageRenderer`; compare with checked-in PNGs under a per-pixel tolerance (the existing snapshot tests, if any, define the helper; else add `GoldenImage.Compare` with max channel diff 2, max differing pixels 0.1 %). Fonts must be embedded so goldens are machine-independent. Regenerate with a `--update-goldens` switch.
2. **v1 invariance**: default-dark/default-light/glass goldens unchanged by PRs 1-4.
3. **Definition**: merge/sanitize per key (valid, out of range, wrong type, missing), including every cross-field rule in `02 §5`, asserting the logged warning text.
4. **Schema**: each draft theme validates against `theme.schema.json` (use `JsonSchema.Net` in tests only, or a tiny keyed walker; do not add a runtime dependency).
5. **Fonts**: each role resolves; path escape rejected per role; missing file falls back and warns once.
6. **CardChrome**: shape path area for chamfer = rect − 2 triangles (analytic check); `circle` on non-square falls back.
7. **Motion**: `MotionPolicy` truth table (3 inputs).
8. **BackgroundPlan**: precedence table (4 rows × user/theme combos), including `type: none`.
9. **Pattern tile**: periodicity (tile edge pixels match opposite edge) so scroll has no seam.
10. **Icons**: path parsing, fallback chain, tint modes, oversize file ignored.
