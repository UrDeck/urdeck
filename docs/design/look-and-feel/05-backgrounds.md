# 05 Backgrounds (12 new animated SkSL shaders)

All twelve are in `assets/shaders/<name>/` as a complete background folder (`<name>.sksl`, `manifest.json`, `sample.png`). They
are original work (Pat is the author of record, GPL-3.0-or-later, matching the built-ins). Copy each folder to
`src/UrDeck.Engine/Backgrounds/Builtin/<name>/` and embed the files the way `aurora` is.

## 1. What was and was not verified

| Check | Status |
|---|---|
| Compiles as an SkSL runtime effect | **Yes**, with skia-python 144 `RuntimeEffect.MakeForShader` (CPU) |
| Rendered and looked at (270x480, t=10 s, theme palette) | **Yes**; `sample.png` is that render |
| Uniform rules (float only, `layout(color)`, `// default:`) | **Yes**, checked with a script that mirrors `ShaderDeclarations` |
| Manifest validates against `docs/schemas/v1/shader-manifest.json` | **Yes** (jsonschema) |
| Lint-style checks (SHADER030/031/032/035/041/042: parameters annotated, no reserved names, defaults inside range, descriptions) | **Yes**, by script |
| `urdeck-cli shader validate --all ... --fail-on-warnings` | **NO. dotnet was not available.** First task of PR 6: run it and fix anything. One *known* item: `pairsWith` is an unknown manifest key until PR 5 teaches `BackgroundManifest` about it (SHADER004). |
| GPU timing on the panel | **NO.** CPU raster numbers below are relative hints only |
| Visual parity between skia-python CPU and the host's GPU SkSL | Not guaranteed (precision, `atan` seam, `fract` of large arguments). **Look at each on the panel.** |

Things the CPU harness cannot catch that you should check on the panel: banding in dark gradients; an `atan` seam at ±π in Warp Gate and
Vortex (a faint vertical line left of centre); hash artefacts at large `iTime` (check Nebula and Embers after the time has wrapped);
and the time wrap itself. Every periodic motion divides 4 h (14 400 s), so the wrap should be seamless.

## 2. Authoring conventions used (keep them for future shaders)

- **Palette roles.** The first lines of each file say what `iColor0..3` mean *for that shader*. The host gives every shader the same four
  colours from `colors.backgroundPalette`; the draft themes put the right colours in the right roles for their paired shaders (`iColor0` = ground,
  `iColor3` = brightest accent, mostly). A mismatched theme still works but looks off; that is why pairing exists (`07`).
- **Time wrap.** Every cycle length divides 14 400 s (3, 4, 5, 6, 8, 10, 12, 16, 20, 24, 30, 36, 40, 48, 60, 120, 180 s). Scroll rates are chosen so
  `rate * 14400` is a whole number of cells.
- **No `int` uniforms.** Counts (`bars`, `count`, `arms`, `cells`) are floats; loops use a constant bound and break on `float(i) >= count`.
- **Reserved names.** `speed`, `scale`, `colors` and the host inputs belong to the product (SHADER032); none is used as a parameter.
- **Top quiet.** Where widgets sit (Embers, Equalizer) the busy part is at the bottom.
- **Brightness ceiling.** Backgrounds leave text legible over translucent cards; `background.scrim` in a theme is the knob when a brighter palette is chosen.

## 3. The twelve

| Shader | One line | Palette roles (iColor0..3) | CPU ms* | Tags |
|---|---|---|---|---|
| **Horizon** (`horizon`) | A retro sun setting over a glowing perspective grid that streams toward you. | iColor0 sky, iColor1 horizon glow, iColor2 sun, iColor3 grid | 15 | retro, synthwave, gaming |
| **Spectrum** (`spectrum`) | Slow shifting rainbow light, the RGB-keyboard look, kept soft so widgets stay readable. | iColor0 ground; iColor3 only when saturation is 0; iColor1 and iColor2 are not used (the colours are hues) | 31 | rgb, gaming, calm |
| **Contour** (`contour`) | Topographic contour lines drifting slowly like a survey map. | iColor0 ground, iColor1 ground lit, iColor2 minor lines, iColor3 index lines (every fifth) | 18 | technical, calm, business |
| **Nebula** (`nebula`) | Drifting colour clouds with an irregular scatter of twinkling stars, some in loose clusters. | iColor0 space, iColor1..3 cloud colours | 115 | space, calm |
| **Streams** (`streams`) | Fine columns of light flowing downward, like data in a server room. | iColor0 background, iColor1 faint trail, iColor2 body, iColor3 bright head | 22 | technical, data, gaming |
| **Ripple** (`ripple`) | Sunlight caustics rippling across a pool floor. | iColor0 deep water, iColor1 water, iColor2 light, iColor3 hot highlights | 25 | water, calm |
| **Embers** (`embers`) | Sparks lifting off a glowing horizon and fading as they rise. Dark at the top where widgets live. | iColor0 night, iColor1 heat glow, iColor2 spark, iColor3 white-hot spark | 32 | fire, warm, gaming |
| **Plotter** (`plotter`) | A drafting grid with a slow scan line and dashed rings that pulse out and vanish. | iColor0 paper, iColor1 minor grid, iColor2 major grid and rings, iColor3 scan line | 20 | technical, business, blueprint |
| **Shapes** (`shapes`) | Flat outlined shapes bobbing and turning slowly. Made for light, bold themes. | iColor0 ground, iColor1..3 fills (the shapes cycle through them and two tints of them) | 167 | playful, light, flat |
| **Warp Gate** (`warpgate`) | Falling through an event horizon at light speed: starlight streaks, pulsing rings of light and a swirling bright core. | iColor0 deep space, iColor1 tunnel wall, iColor2 streaks and rings, iColor3 white-hot core | 33 | space, sci-fi, gaming |
| **Equalizer** (`equalizer`) | Spectrum bars dancing along the bottom edge, with peak caps that fall slowly. | iColor0 ground, iColor1 bar bottom, iColor2 bar middle, iColor3 bar top | 43 | music, rgb, gaming |
| **Vortex** (`vortex`) | Light rays spiralling into a glowing core in two layers turning in opposite directions. | iColor0 void, iColor1 outer rays, iColor2 inner rays, iColor3 core | 11 | hypnotic, space, gaming |

*CPU raster at 270x480 on the authoring VM; the GPU on the panel is far cheaper. Relative only. **Nebula (115) and Shapes (167) are the heaviest**: measure them first on the panel (`scale` 0.25 is the remedy, see `docs/backgrounds.md`). Shapes is a 12-iteration SDF loop; Nebula is three star layers plus fBm clouds. If either is over budget, reduce `count` or the star layers by default rather than shipping them heavy.

## 4. Parameters (annotated in each manifest)

### Horizon (`horizon`)

| Parameter | Default | Range | Step | What it does |
|---|---|---|---|---|
| `gridDensity` | 1.0 | 0.4..3 | 0.1 | Grid density: Number of grid lines; 1 is the designed look. |
| `sunSize` | 1.0 | 0.4..1.8 | 0.05 | Sun size: Size of the sun; 1 is the designed look. |
| `horizon` | 0.55 | 0.3..0.8 | 0.01 | Horizon: Height of the horizon line as a fraction of the display, from the top. |

Pairs well with themes: **rigline, prism**. Sample palette: `#0d0221, #7a1c6b, #ff9a2e, #19e6ff`.

### Spectrum (`spectrum`)

| Parameter | Default | Range | Step | What it does |
|---|---|---|---|---|
| `saturation` | 0.85 | 0..1.5 | 0.05 | Saturation: Colour strength. |
| `intensity` | 1.0 | 0..2 | 0.05 | Intensity: Brightness. |
| `hueShift` | 0.0 | 0..1 | 0.01 | Hue shift: Rotates the colours around the wheel. |
| `vignette` | 0.6 | 0..1 | 0.05 | Vignette: How strongly the edges darken. |

Pairs well with themes: **rigline, prism**. Sample palette: `#07060d, #2a1a5e, #7a5cff, #19e6ff`.

### Contour (`contour`)

| Parameter | Default | Range | Step | What it does |
|---|---|---|---|---|
| `density` | 14.0 | 4..40 | 1 | Density: Number of contour lines. |
| `lineWidth` | 1.0 | 0.3..3 | 0.1 | Line width: Thickness of the lines; 1 is the designed look. |
| `relief` | 1.0 | 0.3..2 | 0.05 | Relief: How rugged the terrain is. |

Pairs well with themes: **carbon, paper, blueprint**. Sample palette: `#090b0e, #10161b, #1f6f61, #4ff0c8`.

### Nebula (`nebula`)

| Parameter | Default | Range | Step | What it does |
|---|---|---|---|---|
| `zoom` | 1.0 | 0.5..2.5 | 0.05 | Zoom: Scale of the clouds; larger is closer. |
| `starDensity` | 1.0 | 0..3 | 0.05 | Star density: How many stars. |
| `starClustering` | 0.6 | 0..1 | 0.05 | Star clustering: 0 spreads the stars evenly, 1 gathers them into clumps. |
| `twinkle` | 1.0 | 0..2 | 0.05 | Twinkle: How much the stars flicker. |

Pairs well with themes: **carbon, prism, obsidian**. Sample palette: `#0b1030, #3a2a8f, #0f8fa8, #b23a82`.

### Streams (`streams`)

| Parameter | Default | Range | Step | What it does |
|---|---|---|---|---|
| `density` | 1.0 | 0.3..3 | 0.05 | Density: Number of streams. |
| `trail` | 1.0 | 0.3..3 | 0.05 | Trail: Length of each trail. |

Pairs well with themes: **carbon, rigline, blueprint**. Sample palette: `#060a0d, #0f4d41, #1f9e82, #8affd8`.

### Ripple (`ripple`)

| Parameter | Default | Range | Step | What it does |
|---|---|---|---|---|
| `ripples` | 1.0 | 0.4..3 | 0.05 | Ripple density: How many ripples fit across the display; larger is smaller ripples. |
| `sharpness` | 1.0 | 0.3..2.5 | 0.05 | Sharpness: Contrast of the bright lines. |

Pairs well with themes: **prism, paper, pop**. Sample palette: `#0a1d3d, #12407a, #3a8fd0, #bff0ff`.

### Embers (`embers`)

| Parameter | Default | Range | Step | What it does |
|---|---|---|---|---|
| `density` | 1.0 | 0.2..2 | 0.05 | Density: How many sparks. |
| `heat` | 1.0 | 0..2 | 0.05 | Heat: Strength of the glow at the bottom. |

Pairs well with themes: **rigline, ember**. Sample palette: `#0b0604, #5a1a07, #ff7a1a, #ffd9a0`.

### Plotter (`plotter`)

| Parameter | Default | Range | Step | What it does |
|---|---|---|---|---|
| `cells` | 16.0 | 6..40 | 1 | Cells: Number of major grid cells across the width. |
| `ringRate` | 1.0 | 0.3..2 | 0.05 | Ring size: How far the rings grow; 1 is the designed look. |
| `scan` | 1.0 | 0..2 | 0.05 | Scan line: Brightness of the scan line; 0 hides it. |

Pairs well with themes: **carbon, paper, blueprint**. Sample palette: `#0c2d52, #123c6b, #9fd0ff, #ffd34d`.

### Shapes (`shapes`)

| Parameter | Default | Range | Step | What it does |
|---|---|---|---|---|
| `count` | 9.0 | 2..12 | 1 | Count: Number of shapes, up to 12. |
| `outline` | 1.0 | 0..3 | 0.1 | Outline: Thickness of the outlines; 0 removes them. |
| `outlineColor` | #111111 | colour |  | Outline colour: Colour of the outlines. |

Pairs well with themes: **paper, pop**. Sample palette: `#f6f0e0, #3b5bff, #ff8fc2, #b9f24a`.

### Warp Gate (`warpgate`)

| Parameter | Default | Range | Step | What it does |
|---|---|---|---|---|
| `streaks` | 1.0 | 0.2..2 | 0.05 | Streaks: How many starlight streaks. |
| `ringRate` | 1.0 | 0.3..2 | 0.05 | Ring size: How far the rings of light reach. |
| `coreSize` | 1.0 | 0.4..2.5 | 0.05 | Core size: Size of the glowing core. |
| `swirl` | 1.0 | 0..3 | 0.05 | Swirl: How tightly the tunnel walls twist. |

Pairs well with themes: **carbon, rigline, ember, obsidian**. Sample palette: `#02050f, #0b3a66, #4aa8ff, #f2fbff`.

### Equalizer (`equalizer`)

| Parameter | Default | Range | Step | What it does |
|---|---|---|---|---|
| `bars` | 16.0 | 4..40 | 1 | Bars: Number of bars. |
| `reach` | 0.55 | 0.2..0.9 | 0.01 | Reach: How tall the bars can grow, as a fraction of the display. |
| `gap` | 0.28 | 0..0.7 | 0.01 | Gap: Space between bars. |

Pairs well with themes: **rigline, ember**. Sample palette: `#0a0716, #7a5cff, #ff3ea5, #19e6ff`.

### Vortex (`vortex`)

| Parameter | Default | Range | Step | What it does |
|---|---|---|---|---|
| `arms` | 12.0 | 3..24 | 1 | Arms: Number of spiral arms. |
| `twist` | 1.0 | 0..3 | 0.05 | Twist: How tightly the arms wind. |
| `core` | 1.0 | 0.3..3 | 0.05 | Core: Size of the glowing core. |

Pairs well with themes: **rigline, prism, obsidian**. Sample palette: `#06040f, #3a2a8f, #7a5cff, #19e6ff`.

## 5. Shipping checklist per shader (PR 6 and 7)

1. Copy the folder; embed the three files; register the name where `aurora` is registered (`src/UrDeck.Engine/Backgrounds`).
2. `urdeck-cli shader validate src/UrDeck.Engine/Backgrounds/Builtin/<name> --fail-on-warnings` is clean.
3. `urdeck-cli shader render ... -o frame.png --size 270x480 --time 10 --theme <paired theme>` and replace `sample.png` with the **host's** render (the
   authoring render came from skia-python). Update `settings.theme` and `settings.colors` in the manifest to match what was rendered.
4. Run on the panel at default `scale`: read the `Background frames started/stopped` log lines; compare with `docs/perf/backgrounds-spike.md`.
5. Add the background to the site list and its poster/loop (`site/MEDIA-CHECKLIST.md` works by file name).
6. List it in `docs/backgrounds.md`, "Built-in shaders", with its parameters. Add a docs check that every built-in is listed.

## 6. Design intent per shader (for reviewers and for tuning)

- **Horizon**: retro sun over a perspective grid. `gridDensity` (Pat's request) scales the line count; `sunSize`; `horizon` sets the line's height. The grid streams toward the viewer on a cycle that divides the wrap.
- **Spectrum**: slow RGB wash from three roaming lights. `hueShift` lets the editor offer "match my keyboard". Keep contrast low.
- **Contour**: topographic lines on a drifting height field; `density` is the number of contour levels.
- **Nebula**: clouds plus **irregular stars** (Pat: the first version was too uniform): jittered positions, varied sizes and brightness, grouped by `starClustering`, per-star twinkle phase.
- **Streams**: vertical data streams; `trail` is the tail length (renamed from `length`, which collides with the SkSL builtin).
- **Ripple**: tileable caustics (the well-known domain-warped sine construction, written from scratch). **The parameter is `ripples`, not `scale`**, because `scale` is reserved by the product.
- **Embers**: three parallax layers of sparks on a scrolling cell grid; each cell holds at most one spark; halos fall off inside the cell so there is no square clipping.
- **Plotter**: drafting grid, a scan line (12 s) and dashed pulse rings (6 s).
- **Shapes**: flat outlined shapes, up to 12, with their own outline colour (`layout(color)`).
- **Warp Gate**: Pat's brief, *Stargate wormhole crossed with Star Trek warp speed*. A tunnel wall with a slowly rotating swirl, about 90 angular starlight streaks that accelerate outward, three light rings per cycle and a churning white-hot core. Parameters: `streaks`, `ringRate`, `coreSize`, `swirl` (there is no tint parameter).
- **Equalizer**: bars from sums of sines with falling peak caps. Free-running; a future host audio-level uniform would multiply the heights (not specified here).
- **Vortex**: two counter-rotating logarithmic spiral layers around a glowing core.

## 7. Host changes the shaders need (small)

- `BackgroundManifest` and `docs/schemas/v1/shader-manifest.json`: add optional `pairsWith: { themes: string[] }` (additive). **Must land before the shaders**, or the lint warns SHADER004. Keep `additionalProperties: true`.
- Built-in registry: add the twelve names.
- Site: a poster and loop per background.
