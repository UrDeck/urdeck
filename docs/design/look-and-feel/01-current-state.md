# 01 Current state (audit)

Audited against repo `urdeck/urdeck` at commit `e1198e4` ("archive landing-site and snapshot-frames", #47).
Line numbers are deliberately not quoted; they rot. **Re-verify each claim when you start a PR**: the repo moves.

## Stack

WinUI 3 host, SkiaSharp (`SKXamlCanvas`, CPU raster, GPU-composited by the system compositor), plugin widgets loaded
into collectible `AssemblyLoadContext`s. Licences: `sdk/UrDeck.Sdk` MIT; `src/UrDeck.Engine` and `src/UrDeck.Host`
GPL-3.0-or-later; widgets reference only `UrDeck.Sdk`.

## The theme pipeline today

```
theme.json (user: themes/<name>/theme.json; built-ins embedded: default-dark, default-light, glass)
  -> ThemeStore.Load(name)            src/UrDeck.Engine/Themes/ThemeStore.cs
       parse, Sanitize(), MergeOver(default-dark) -> LoadedTheme(Name, Definition, ThemeFont)
  -> ThemeResolver                    src/UrDeck.Engine/Themes/ThemeResolver.cs
       cell-fraction sizes -> pixels for a given cell size; per-weight typeface clones (variable `wght` axis);
       fonts must stay inside the theme folder (path-escape rejected)
  -> SDK Theme (immutable, per surface size)   sdk/UrDeck.Sdk/Rendering/Theme.cs
       colours, CardRadius/BorderWidth/Gap/Padding in px, Label/Body/TitleSize, UnitRatio, StrokeRatio, StrokeCap,
       GaugeStyle, Typefaces[TextRole]
  -> WidgetPainter.Paint              src/UrDeck.Engine/Rendering/WidgetPainter.cs
       CardShape (SKRoundRect, radius capped at half the smaller side) -> DrawCard (fill + inside border)
       -> ClipRoundRect -> widget.Render(WidgetRenderContext)
       a throwing widget is replaced by a themed error card
```

`ThemeDefinition` (`ThemeDefinition.cs`) is a nullable partial: `Colors, Card, Typography, Stroke, Gauge, Indicator,
Press, Dock`. `MergeOver(baseline)` fills gaps from default-dark **except** `colors.backgroundPalette`, which is
deliberately not inherited (a light theme must not get the dark palette; a missing one is derived from the theme's own
colours: background mixed toward accent by 0/20/40/60 %). `Sanitize(themeName)` clears each invalid value (so the merge
replaces it with the default) and logs a warning via `UrDeckLog.Warn`. Colour regex: `^#([0-9a-fA-F]{6}|[0-9a-fA-F]{8})$`
(alpha first). Unknown JSON keys are ignored silently (System.Text.Json default). That is what makes format 2 degrade
gracefully on an old host. **Consequence for us:** typos in a theme are silent today; see `08` for the lint we add.

`ThemeFont(string Spec, Func<string,Stream?> Open)` holds **one** font spec per theme. Per-role fonts (format 2
`typography.fonts`) therefore need a change here (see `03`).

### Current v1 keys (all kept)

| Group | Keys |
|---|---|
| colors | background, cardFill, cardBorder, text, textMuted, accent, accentDim, good, warning, critical, backgroundPalette |
| card | radius (0..1), borderWidth (0..1), gap (0..1), padding (0..1) |
| typography | font; weights{value,unit,label,body,title} (1..1000); labelSize, bodySize, titleSize (0.005..1); unitRatio (0.05..1) |
| stroke | thickness (0.001..1), cap (round\|square) |
| gauge | style (ring\|bar\|verticalBar; `plain` rejected) |
| indicator | bandHeight, dotSize, pillLength, spacing, active, inactive, backdrop |
| press | scale (0.5..1), opacity (0.1..1) |
| dock | height (0.2..1) |

Built-in defaults (default-dark): radius 0.08, borderWidth 0, gap 0.06, padding 0.065, Inter variable, weights
600/500/400/400/300, label .091, body .0845, title .11, unitRatio .4, stroke .08 round, dock .71, press .96/.85.

## Rendering architecture facts that constrain the design

- **One surface per widget.** `WidgetView` (`src/UrDeck.Host/WidgetView.cs`) hosts each widget in its own
  `SKXamlCanvas` of exactly card size. It repaints on demand (widget refresh policy, `IsAnimating`, a host frame clock
  at 30 fps that runs only while some widget animates). Consequence: **anything drawn outside the card rectangle
  (shadow, glow, outside corner marks) has no pixels to live in today.** See "bleed" in `03`.
- **Press feedback** is done by the compositor (scale + opacity of the whole card including border), not by the widget.
- **Background** is an `SKSwapChainPanel` under the cards (`src/UrDeck.Host/BackgroundLayer.cs`), 30 fps, rendered at
  `scale` 0.5, shader time wraps every 4 h of scaled time. Pages slide over the background. Window corners are square.
- **Page rendering** in `src/UrDeck.Engine/Rendering/PageRenderer.cs` (snapshots, headless) and the page indicator in
  `PageIndicator.cs`.
- **Snapshots** (`UrDeck.Host.exe --snapshot out.png --size 1100x3840 [--frames N --fps 30 --start <time>]`) render
  without a screen using the same paint path. These are the basis of the golden-image tests we add.

## Components (SDK) today

`sdk/UrDeck.Sdk/Components/`: `Gauge` (+`GaugeStyle`: Plain, Ring, Bar, VerticalBar; `GaugeOptions`: Label, Fraction,
TargetFraction, Level, ReadoutScale; `GaugeScale`), `Readout` (equal-width digits already, so there is **no need for a
`typography.numerals` knob**), `TextLine`, `TextMetrics`, `ImageTile`, `LottieAnimation` (Skottie), `Alignment`,
`Attribution`.

- Ring: 270° arc, start 135° (hard-coded constants `RingSweep`, `RingStart` in `Gauge.cs`). A "270° arc" gauge style
  already exists; do not add one.
- Gauge fill colour: accent, or warning/critical once `GaugeLevel` reaches that level; track = same colour at
  `AccentDim` alpha; stale/pending/unavailable = `TextMuted`.
- A gauge places its own readout (inside a ring, below a bar, over a vertical bar). It keeps **no state**; motion
  between values (an ease) belongs to the widget.

## Widgets today

| Widget | Config keys | Notes |
|---|---|---|
| Clock | `Format` ("24h"…), `ShowDate`, `TextColor`, `FontSize`, `Style` ("simple" \| "flap") | Flap = split-flap digits (`FlapDisplay.cs`), animated. |
| Stats | `Slots[]`: Reading, Label, Unit, Decimals, Style, Min, Max, Warning, Critical (+ ExtensionData); 1x1, 2x2, 4x2, 4x4 | Eased fill. No history buffer. |
| Weather | Location/Latitude/Longitude, Label, Unit, Motion, PeriodSeconds, Days, AnimateForecast | 4x2; Meteocons Lottie icons via `WeatherIcons.cs` and `Icons/`; has a multi-day forecast. |
| Shortcut | Target, Arguments, Icon, Label | Real app icons through `IIconSource` (`sdk/UrDeck.Sdk/Icons/IIconSource.cs`); tap-to-launch; press feedback by the compositor. |

## Backgrounds today

`src/UrDeck.Engine/Backgrounds/`: `BackgroundManifest`, `ShaderDeclarations`, `ShaderDiagnostic`, `BackgroundPalette`;
built-ins in `Builtin/<name>/{<name>.sksl,manifest.json,sample.png}`: `plasma, aurora, gradient, julia`.
Rules (`docs/backgrounds.md`, `docs/schemas/v1/shader-manifest.json`): SkSL entry `half4 main(float2 fragCoord)`; host
uniforms `iTime, iResolution, iColor0..3`; parameters are `float/float2/float3/float4` only (no `int`), `layout(color)`
for colours, `// default: v` comments; `manifest.json` (`"manifest": 1`) annotates parameters (label, description, min,
max, step, kind, hidden, unit), tags, samples; lint codes `SHADER###`; the CLI is `urdeck-cli shader init|validate|render`.
Unknown manifest keys are allowed (warned), which is how we add `pairsWith` without a schema bump.

Config: `"background": { "type": "shader", "name": "aurora", "speed": .., "scale": .., "colors": [..], <params> }`.
The `colors` there win over the theme palette; the theme palette wins over the derived one.

## Site today

`site/` (Astro; Node 22): `themes.md`, `decks/<theme>/urdeck-config.json` for each built-in theme, `themes.json`,
`MEDIA-CHECKLIST.md` (pictures found **by file name**), `scripts/Render-SiteDecks.ps1`, `npm run media`.
Each new built-in theme needs a deck folder and pictures, or it is left out of the gallery.

## Gaps this work fills (summary; the long version is `11-gap-audit.md`)

| Need | Today |
|---|---|
| Card shapes other than rounded rect | none |
| Gradient / dashed / animated borders | solid inside border only |
| Shadow, glow, highlight, sheen | none |
| Registration/corner marks | none |
| Page texture (dots, grid, scanlines) | none (a shader can do it, but shaders are user-switchable) |
| Per-role fonts; case; tracking; slant; gradient value fill | one font + weights |
| Gauge: segmented, tach+redline, marker, thick outlined, ticks, scale labels, gradient, glow | ring/bar/verticalBar/plain |
| Sparkline, status chip | none |
| Theme icon sets | weather = Meteocons Lottie; shortcuts = app icons |
| Theme default / static background; pairing | none; `colors.backgroundPalette` only |
| Theme-level motion ceiling / reduced motion | none for themes |
