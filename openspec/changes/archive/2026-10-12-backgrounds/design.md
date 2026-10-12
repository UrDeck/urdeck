## Context

See `proposal.md` for the motivation and scope, and `docs/perf/backgrounds-spike.md` for the measurements every number
here comes from. What the code does today, and what constrains the approach:

- `MainWindow` stacks three layers in `_root` (a `Grid`): `_background` (a `Grid` whose brush is the theme's
  `colors.background`, set in `ApplyBackground`), `_surface` (the pager's pages, one `SKXamlCanvas` per widget) and
  `_overlay`. The pager moves pages inside `_surface`; `_background` never moves. So a deck-wide background that stays
  put during a swipe is what the layer stack already gives.
- Widget surfaces are `SKXamlCanvas`: Skia draws on the CPU, the GPU composites. `SKSwapChainPanel` (Skia on the GPU
  through ANGLE) was rejected for cards because it is opaque. The bottom layer is opaque anyway.
- `--snapshot` runs on the engine alone (no XAML) through `PageRenderer.Render`, which starts with
  `canvas.Clear(theme.Background)`. Anything a snapshot should show has to be drawn by the engine, on the CPU.
- `UrDeckConfig` has no top-level `background`. `PageConfig.Background` (a `JsonElement?`) exists and is unused.
- Themes are data: `ThemeDefinition` is merged over `default-dark`, sanitised value by value, and re-read on every
  config reload. Built-in themes are embedded resources of the engine (`UrDeck.Themes.<file>`); user themes are folders
  under `themes/` next to the executable (`HostContext` passes the folder to `ThemeStore`).
- The deck is hidden by `PlacementController` through `MainWindow.HideDeck`, and `ReleasePage` drops the page of a
  hidden deck. `RebuildLayout` returns early while `_pageHeld` is set.
- `FrameClock` is a `DispatcherQueueTimer` at 1/30 s that invalidates animating widgets. The spike measured a XAML
  `DispatcherTimer` at that interval delivering about 22 frames per second; `FrameClock` itself was not measured.
- Framework code stays in `UrDeck.Host`; logic that can be tested without WinUI goes into `UrDeck.Engine`, where the
  line-coverage floor applies. The SDK is not touched: widgets do not see the background.

## Goals / Non-Goals

**Goals:**
- A moving background generated from parameters, a few built in, and the user's own as a text file.
- A wallpaper image.
- The cost the spike measured, on the real host: about 3% of one core and under 1% GPU at the default scale, and
  exactly nothing when there is no background or it is still.
- The snapshot shows the background, from the same engine code as the window.
- Everything that can be decided without a window (parsing, validation, uniform binding, palette, time, cover-fit) is
  in the engine and unit-tested.

**Non-Goals:**
- Per-page backgrounds, a video file, backgrounds driven by readings, blur behind translucent cards.
- Guarding against a heavy user shader (measuring frame time, lowering the scale automatically). `scale` is the tool.
- A configurable frame rate. It is 30.
- A settings-window page for backgrounds. Configuration is the JSON file.
- Changing `FrameClock`. Its real rate is measured and recorded (task 9.5), not fixed here.

## Decisions

### 1. Live SkSL on one GPU surface, not video, not the compositor

A moving background is an SkSL shader (`SKRuntimeEffect`) drawn into one `SKSwapChainPanel` in the `_background`
layer. Spike numbers at half resolution: 3.3 to 3.6% of one core, 0.5 to 0.9% GPU 3D, about 25 MB private over the
static page.

Alternatives, all measured: a video of the same picture at native resolution (9 to 15% video decode, +130 to 170 MB
private, +250 to 285 MB video memory, and parameters cannot change live); an animated composition gradient brush (5.5%
CPU and 5.6% 3D, the worst live option, and no shaders); the shader on an `SKXamlCanvas` (half a core at quarter
resolution). HLSL through Win2D or ComputeSharp would add a dependency and a second shading language, and the shader
could not run in a snapshot.

### 2. Configuration shape

A top-level `background` object. It is read as a `JsonElement?` on `UrDeckConfig` and parsed leniently by the engine
(`BackgroundSettings.Parse`), so a wrong shape costs a warning and the theme colour, never the whole configuration.

```json
"background": { "type": "shader", "name": "aurora", "speed": 0.5, "angle": 30 }
"background": { "type": "shader", "name": "my-thing", "scale": 0.25, "density": 3.0, "glow": "#ff8800" }
"background": { "type": "shader", "name": "plasma", "colors": ["#0f1a4d", "#8c268c", "#fa9e40"] }
"background": { "type": "image", "path": "D:/Pictures/wall.jpg" }
```

| Key | Kind | Meaning | Default | Valid |
|---|---|---|---|---|
| `type` | both | `shader` or `image` | required | those two |
| `name` | shader | a built-in name or a user file's name without `.sksl` | required | letters, digits, `-`, `_` |
| `speed` | shader | multiplies time | `1` | 0 to 10 |
| `scale` | shader | render resolution as a fraction of the panel's | `0.5` | 0.1 to 1 |
| `colors` | shader | replaces the theme's palette | the theme's | 1 to 4 colours |
| `path` | image | the picture | required | see decision 9 |
| anything else | shader | a parameter of the shader (decision 4) | the file's default | |

Rules: absent or `null` means no background (today's behaviour). A value that is not an object, an unknown `type`, or
a missing required key logs a warning and means no background. An out-of-range `speed` or `scale` logs a warning and
uses the default. More than four `colors` uses the first four with a warning; one invalid colour drops the whole
`colors` value with a warning. For `image`, any key other than `type` and `path` logs a warning. `pages[].background`
stays unused; when a page has one, one info line per reload says it is not supported and names the top-level setting.

The sample `src/UrDeck.Host/urdeck-config.json` is not changed: it would replace working configs in `bin/` (see
`AGENTS.md`) and change every existing snapshot.

Why configuration and not the theme: which background the deck has is content, like the widget list. The theme stays
"how things look" and contributes the colours (decision 6).

### 3. Shader files: built-in and user, one mechanism

- Built-in shaders are `.sksl` files in `src/UrDeck.Engine/Backgrounds/Builtin/`, embedded like the themes
  (`<EmbeddedResource Include="Backgrounds\Builtin\*.sksl" LogicalName="UrDeck.Backgrounds.%(Filename)%(Extension)" />`).
- User shaders are `backgrounds/<name>.sksl` next to the executable. `HostContext` passes the folder to a
  `ShaderStore`, as it passes `themes/` to `ThemeStore`.
- `ShaderStore.Load(name)` looks in the user folder first, then the built-ins, so a user file with a built-in's name
  replaces it (the rule themes have). A name with a path separator, `..` or any character outside letters, digits, `-`
  and `_` is rejected before any file is touched.
- Shaders are re-read when the configuration is reloaded, like themes: after editing a `.sksl` file, save the config.
  No file watcher on `backgrounds/`.
- Built-ins are written with the same inputs and the same default comments a user file uses, so they are the examples.

A shader file is plain SkSL with `half4 main(float2 fragCoord)`. `fragCoord` is in pixels of the render surface, origin
top left. There is no manifest: what the shader takes is what it declares (decision 4).

### 4. Shader inputs and parameters

SkiaSharp exposes the names of a compiled effect's uniforms but not their types, so the engine reads the declarations
from the source text. One regular expression over the lines:

```
uniform <type> <name>;            // default: <value>
```

- Types: `float`, `float2`, `float3`, `float4`, and the aliases `vec2`/`vec3`/`vec4`, `half`, `half2`/`half3`/`half4`.
  A uniform of any other type (`int`, matrices, arrays, `shader`) is left at zero with a warning; a `uniform shader`
  child makes the file unusable (failure, decision 10).
- The trailing comment is optional. `<value>` is a number, comma-separated numbers (as many as the type has
  components), or a `#RRGGBB`/`#AARRGGBB` colour for `float3`/`float4`. A default that does not parse or does not fit
  the type is a warning and zero.

Inputs the host sets, each only if the shader declares it with the type shown:

| Uniform | Type | Value |
|---|---|---|
| `iTime` | `float` | seconds, already multiplied by `speed`, wrapped (decision 5) |
| `iResolution` | `float2` | the render surface's size in pixels |
| `iColor0` to `iColor3` | `float4` | the palette (decision 6), straight RGBA, each component 0 to 1 |

The names follow Shadertoy so a port is mostly renaming `mainImage`. Everything else the shader declares is a
parameter: a configuration key with the same name sets it. Accepted values: a number for `float`; an array of numbers
of the right length for a vector; a colour string for `float3` (alpha dropped) or `float4`. A value of the wrong form,
a non-finite number, a key that names no uniform, and a key that names one of the host's inputs each log one warning
and are ignored (the default stays). Colours are passed as written: no colour management.

The shader's result is treated as opaque: the surface is cleared with the theme's `colors.background` and the shader
is drawn over it, so a shader that returns alpha below 1 shows the theme colour through.

A background is **moving** when it is a shader, `speed` is above 0 and the shader declares `iTime`. Otherwise it is
**still** (decision 7).

### 5. Time: scaled, wrapped, paused

A `float` holds seconds exactly enough for smooth motion only for a few hours: at one day the step is 8 ms, a quarter
of a frame, and motion starts to judder; at a week it is 62 ms. The deck runs for weeks.

- The host keeps elapsed time as a `double` that advances only while frames are produced (a stopwatch that is stopped
  with the frames). Hiding the deck and showing it again continues the motion where it stopped.
- `iTime = (elapsed * speed) mod 14400`. At four hours the step is about 1 ms. The price is one visible jump every
  four hours of uninterrupted display (divided by `speed`) for a shader that is not periodic over the wrap. That is
  documented, not hidden.
- Time restarts at 0 only when the background itself is rebuilt (decision 8), not on every config reload.
- The wrap is a pure function in the engine (`BackgroundClock.Wrap(elapsedSeconds, speed)`), tested at the boundary.
- A snapshot uses `iTime = 10` whatever the speed, so snapshots are reproducible.

Alternative: pass a second, periodic phase input for shaders to build seamless loops on. Left out until someone needs
it; it can be added without breaking files.

### 6. Colour: theme palette, configuration override, shader's choice

Every shader gets four colours. Where they come from, first match wins:

1. `background.colors` in the configuration.
2. The theme's `colors.backgroundPalette` (new, optional, 1 to 4 colours).
3. Derived from the theme: `colors.background` mixed towards `colors.accent` by 0%, 20%, 40% and 60% (in sRGB values,
   component by component).

Fewer than four colours repeat the last one. Alpha is ignored: palette colours are opaque.

`backgroundPalette` is deliberately **not** merged from `default-dark`. Every other theme value falls back to the
default theme's, but a light user theme written before this change would then get the dark palette. A theme without
the key gets the derived palette from its own colours, with no warning. A `backgroundPalette` that is not an array of
1 to 4 valid colours is dropped with a warning and the derived palette is used.

The palette lives on `ThemeColorsDefinition` (`BackgroundPalette`, a `List<string>?`) and is resolved by a small engine
function (`BackgroundPalette.Resolve(configColors, loadedTheme)` returning four `SKColor`s). It does not go into the
SDK's `Theme`: widgets have no use for it.

Starting values for the built-in themes (the owner settles them by eye, task 9.3):

| Theme | `backgroundPalette` |
|---|---|
| `default-dark` | `#0f0f1a`, `#1a2350`, `#3a2a6e`, `#24507a` |
| `default-light` | `#e9ebf2`, `#cfd8f0`, `#e3d3ee`, `#c5e2ee` |
| `glass` | `#131c4f`, `#1b2760`, `#3a3f9c`, `#6b3fa0` |

The built-in shaders draw only with `iColor0` to `iColor3`, so they follow the theme. A user shader may ignore the
palette and hard-code its colours or declare its own colour parameters.

### 7. Two surfaces: GPU for moving, CPU for still

The engine has one painter, `DeckBackground.Paint(SKCanvas, width, height, seconds)`, that draws either kind on any
Skia canvas. The host chooses the surface:

- **Moving:** an `SKSwapChainPanel` with `EnableRenderLoop = false`, invalidated by the host's pacing (decision 8).
- **Still** (an image, a shader with `speed` 0, a shader without `iTime`): an `SKXamlCanvas`, painted once, exactly
  like a card that never changes. No GPU context is created, no frame source runs, and the idle cost is the static
  baseline of the spike (0.2% of one core, 0 GPU).
- **None:** no element at all. `_background` keeps its theme-colour brush, as today.

Both elements are children of `_background`, with `IsHitTestVisible = false` so the pager at `_root` keeps every
pointer. The theme-colour brush stays on `_root` and `_background` under them: it is what shows before the first paint
and after a failure.

**Render scale.** The element's size is the root's size in DIPs times `scale`, aligned top left, and its composition
visual is scaled back up: `ElementCompositionPreview.GetElementVisual(element).Scale = new Vector3(1/scale, 1/scale, 1)`.
A XAML `RenderTransform` (or a `Viewbox`) must not be used: it feeds the panel's composition scale and the panel then
renders at full size again (spike finding 7). The compositor's stretch is linear. An image is always at scale 1; a
still shader uses `scale` too (one CPU frame of a heavy shader at full resolution took 0.4 s in the spike).

The surface's pixel size is logged when the background is applied (`Background: shader 'plasma' (built-in), surface
550x1920, scale 0.5, moving`), which is also how the scale is verified at display scalings other than 100%.

### 8. The host's background layer: lifetime, pacing and when frames stop

A small host class (`BackgroundLayer`) owns the element. The engine's `DeckBackground` carries a **key** with value
equality: the kind, the shader's name, its source text (or the file's last write time and length), `speed`, `scale`,
the resolved parameters, the four palette colours, and for an image the path with its last write time and length.

- `MainWindow.RebuildLayout` asks the layer to apply the configured background after the `_pageHeld` check and before
  the `LayoutKey` comparison. An equal key keeps the running layer untouched: saving an unrelated config edit does not
  restart the motion. A different key disposes the old element and background and builds the new one, with time at 0.
  A theme change changes the palette and so the key.
- The layer follows the root's size: `RebuildLayout` passes the screen size and scaling, and the layer resizes its
  element when they change.
- `ReleasePage` (the deck is hidden and its page dropped) releases the layer too: the element is removed and disposed,
  and the GPU context goes with it. `HideDeck` stops the frames at once.

**Pacing.** While the layer is running it subscribes to `CompositionTarget.Rendering` and invalidates the panel when
at least 1/30 s (less a quarter interval of slack) has passed since the last frame it asked for, exactly as the spike
did (steady 30.06 frames per second over 50 s). It unsubscribes when it stops: a subscribed `Rendering` handler keeps
XAML ticking at the display's rate. The background does not join `FrameClock`: a 33 ms dispatcher timer delivered
about 22 frames per second in the spike, and the background is the one thing on screen that moves all the time.

**Running** means: the background is moving, the deck is shown, and the display is on. The layer logs each change with
the reason and, when stopping, the frames produced and the seconds they took (`Background frames stopped (deck hidden):
8997 frames in 300.0 s`). Nothing is logged while nothing changes.

**Display off.** A monitor that sleeps does not always disconnect, so placement may not hide the deck. The host
registers for `GUID_CONSOLE_DISPLAY_STATE` (`6FE69556-704A-47A0-8F24-C28D936FDA47`) with
`RegisterPowerSettingNotification` on the deck window and reads `WM_POWERBROADCAST` / `PBT_POWERSETTINGCHANGE` in a
window subclass of its own (a new small class beside `WindowFocus`, with a different subclass id; `WindowFocus` is not
installed in the activatable development mode, so it cannot carry this). Data 0 is off; 1 (on) and 2 (dimmed) are on.
The log says `Windows: display off` / `Windows: display on`. PC sleep needs nothing: no frames are produced while
suspended.

**After resume.** The render-host spike tested sleep and resume with `SKXamlCanvas` only. Whether an
`SKSwapChainPanel` survives sleep, a display mode change and a hot-plug is checked on the panel (task 9.2). If it does
not, the layer is rebuilt when placement reports `Resumed` or a verified placement; that hook is cheap because the
layer already rebuilds from a key.

### 9. Images

- `path` is absolute, or relative to the application folder. Environment variables in it are expanded
  (`%USERPROFILE%/Pictures/wall.jpg`), as the shortcut widget does for its targets.
- The engine decodes with Skia (`ImageFiles` in `UrDeck.Engine/Icons` already loads image files; reuse it and make sure
  the encoded orientation of a photo is applied). Formats are what Skia decodes: PNG, JPEG, BMP, WebP, and the first
  frame of a GIF.
- The picture is scaled once to **cover** the panel: the smallest scale at which it fills both dimensions, centred,
  the overflow cropped. The scaled copy is kept (about 16 MB for 1100x3840) and the decoded original is released, so a
  huge photo does not stay in memory. `BackgroundImage.CoverRect(imageSize, targetSize)` is a pure function.
- Decoding and scaling run on a thread-pool thread. The theme colour shows until the result arrives; a result for a
  background that has been replaced meanwhile is discarded. The scaled copy belongs to the size it was made for: a
  change of the panel's size makes a new one.
- The live layer and the snapshot draw it through the same `DeckBackground.Paint`.

Alternative: a XAML `Image` with `UniformToFill`. It would use a second decoder (WIC) whose formats and orientation
handling differ from the snapshot's.

### 10. Failures

Every failure ends the same way: one warning in `urdeck.log` that names the cause, and no background (the theme
colour). The cases: an unknown shader name; a file that cannot be read; SkSL that does not compile (the compiler's
message is logged); a `uniform shader` child; an image that is missing or does not decode; an exception while
painting (logged once, then the layer is dropped and not retried until the key changes); the GPU surface failing to
initialise. A failure never throws out of the config reload and never takes widgets down with it.

### 11. Snapshot and tests

`PageRenderer.Render` and `RenderToBitmap` take an optional `DeckBackground`. After `canvas.Clear(theme.Background)`
the background is painted: a shader into an offscreen CPU surface of the panel's size times `scale`, then stretched
with linear filtering onto the page (so the snapshot has the softness of the live layer); an image at full size.
`SnapshotCommand` builds the background the way the window does (settings from the config, palette from the theme in
effect, including `--theme`) and waits for the image decode.

This runs Skia's CPU backend, which also makes the engine tests possible on CI without a GPU: compile each built-in,
paint a small surface, probe pixels.

### 12. Engine and host split

Suggested types; names may change, the split may not:

| Where | Type | Does |
|---|---|---|
| Engine `Backgrounds/` | `BackgroundSettings` | Parses and validates the `background` element; logs the warnings |
| | `ShaderStore` | Finds a shader's text by name: user folder, then embedded |
| | `ShaderSource` / declaration parser | Uniform declarations and defaults from the text |
| | `BackgroundPalette` | The four colours from config, theme or derivation |
| | `BackgroundClock` | The wrapped, scaled time |
| | `BackgroundImage` | Path resolution, decode, cover-fit |
| | `DeckBackground` (`IDisposable`) | Built from the settings, the theme and the stores; has the key, `IsMoving`, `Scale`, and `Paint(canvas, w, h, seconds)`; owns the compiled effect or the scaled bitmap |
| Engine `Rendering/` | `PageRenderer` | Paints the background for snapshots |
| Engine `Config/` | `UrDeckConfig.Background` | The raw `JsonElement?` |
| Host | `BackgroundLayer` | The element, the render scale, pacing, running state, logging |
| Host | display power class | `GUID_CONSOLE_DISPLAY_STATE` to an on/off event |
| Host | `MainWindow`, `HostContext`, `SnapshotCommand` | Wiring |

The built-in shaders are GPL (they are in the engine). A user's `.sksl` file is the user's.

### 13. Built-in shaders

Four, each drawing with the palette only. `plasma` and `julia` are the spike's shaders with the palette and a few
parameters added; `aurora` and `gradient` are new. These sources are starting points: each must compile and pass the
tests, and the owner settles the look on the panel (task 9.3). Keep them cheap: no loop over 128 iterations, no
texture lookups.

`plasma.sksl` (slow warped colour fields):

```glsl
uniform float iTime;
uniform float2 iResolution;
uniform float4 iColor0;
uniform float4 iColor1;
uniform float4 iColor2;
uniform float4 iColor3;
uniform float angle;   // default: 0
uniform float zoom;    // default: 3.0

half4 main(float2 fragCoord) {
    float a = radians(angle);
    float2 uv = fragCoord / iResolution.y;
    float2 p = float2(uv.x * cos(a) - uv.y * sin(a), uv.x * sin(a) + uv.y * cos(a)) * zoom;
    float t = iTime * 0.15;
    for (int i = 1; i < 5; i++) {
        float fi = float(i);
        p.x += 0.6 / fi * sin(fi * p.y + t + 0.3 * fi) + 0.5;
        p.y += 0.6 / fi * cos(fi * p.x + t * 1.3 + 0.3 * fi) - 0.5;
    }
    float v = 0.5 + 0.5 * sin(p.x + p.y);
    float3 col = mix(iColor0.rgb, iColor1.rgb, smoothstep(0.0, 0.45, v));
    col = mix(col, iColor2.rgb, smoothstep(0.35, 0.8, v));
    col = mix(col, iColor3.rgb, smoothstep(0.7, 1.0, v));
    return half4(half3(col), 1.0);
}
```

`julia.sksl` (a Julia set whose constant circles slowly; the fractal of the original idea):

```glsl
uniform float iTime;
uniform float2 iResolution;
uniform float4 iColor0;
uniform float4 iColor1;
uniform float4 iColor2;
uniform float4 iColor3;
uniform float zoom;     // default: 1.0
uniform float radius;   // default: 0.7885

half4 main(float2 fragCoord) {
    // The longer side of the surface is the real axis, so the set fills a tall panel.
    float2 centred = fragCoord - 0.5 * iResolution;
    float longSide = max(iResolution.x, iResolution.y);
    float2 z = (iResolution.y > iResolution.x ? centred.yx : centred.xy) / longSide * 3.4 / zoom;
    float t = iTime * 0.1;
    float2 c = radius * float2(cos(t), sin(t));
    float n = 0.0;
    for (int i = 0; i < 128; i++) {
        if (dot(z, z) > 16.0) break;
        z = float2(z.x * z.x - z.y * z.y, 2.0 * z.x * z.y) + c;
        n += 1.0;
    }
    float v = sqrt(n / 128.0);
    float3 col = mix(iColor0.rgb, iColor1.rgb, smoothstep(0.0, 0.35, v));
    col = mix(col, iColor2.rgb, smoothstep(0.3, 0.7, v));
    col = mix(col, iColor3.rgb, smoothstep(0.65, 1.0, v));
    return half4(half3(col), 1.0);
}
```

`gradient.sksl` (a soft gradient along `angle` whose bands drift):

```glsl
uniform float iTime;
uniform float2 iResolution;
uniform float4 iColor0;
uniform float4 iColor1;
uniform float4 iColor2;
uniform float4 iColor3;
uniform float angle;   // default: 30

half4 main(float2 fragCoord) {
    float2 uv = (fragCoord - 0.5 * iResolution) / max(iResolution.x, iResolution.y);
    float a = radians(angle);
    float d = dot(uv, float2(cos(a), sin(a)));
    float v = 0.5 + 0.5 * sin(d * 3.0 + iTime * 0.20);
    float w = 0.5 + 0.5 * sin(d * 1.7 - iTime * 0.13 + 1.3);
    float3 col = mix(iColor0.rgb, iColor1.rgb, v);
    col = mix(col, iColor2.rgb, w * 0.6);
    col = mix(col, iColor3.rgb, v * w * 0.5);
    return half4(half3(col), 1.0);
}
```

`aurora.sksl` (three glowing curtains that sway along the longer side, over `iColor0`):

```glsl
uniform float iTime;
uniform float2 iResolution;
uniform float4 iColor0;
uniform float4 iColor1;
uniform float4 iColor2;
uniform float4 iColor3;
uniform float intensity;   // default: 1.0

half4 main(float2 fragCoord) {
    float2 uv = fragCoord / iResolution;
    // "along" runs down the longer side, "across" over the shorter one.
    float along = iResolution.y > iResolution.x ? uv.y : uv.x;
    float across = iResolution.y > iResolution.x ? uv.x : uv.y;
    float t = iTime * 0.1;
    float3 col = iColor0.rgb;
    for (int i = 0; i < 3; i++) {
        float fi = float(i);
        float wave = 0.5 + 0.18 * sin(along * (2.0 + fi) + t * (1.0 + 0.4 * fi) + fi * 2.1)
                         + 0.08 * sin(along * (7.0 + 2.0 * fi) - t * 1.7 + fi);
        float x = (across - wave) / (0.18 + 0.05 * fi);
        float band = exp(-x * x);
        float3 c = i == 0 ? iColor1.rgb : (i == 1 ? iColor2.rgb : iColor3.rgb);
        col += c * band * (0.35 + 0.15 * sin(t * 2.0 + fi * 1.3 + along * 3.0)) * intensity;
    }
    return half4(half3(clamp(col, 0.0, 1.0)), 1.0);
}
```

### 14. Budget

Measured on the panel with the method of `docs/perf/nexus-baseline.md`, the owner's usual page, once without a
background and once with each case (task 9.4). The spike's deltas plus headroom:

| Case | Over the same page without a background |
|---|---|
| No `background` | nothing: no element, no frame source, no GPU context; the numbers of today |
| Still (image, `speed` 0) | after the first paint: CPU and GPU as without; memory up by the surface (about 16 MB at 1100x3840) |
| Moving built-in at the default scale | at most 5% of one core, 2% GPU 3D, 60 MB private, 60 MB dedicated video memory |

`julia` at scale 1 is expected above the GPU line on a lesser GPU; the budget is for the default scale.

## Risks / Trade-offs

- **`SKSwapChainPanel` does not survive sleep, a mode change or a hot-plug** -> checked on the panel (task 9.2); the
  fallback is rebuilding the layer on resume and after a verified placement (decision 8).
- **A user shader that is too heavy stalls the GPU or the game beside it** -> accepted for this version; `scale` is
  documented as the remedy and the docs say so plainly. A guard is a later change.
- **Contention with a game** was not measured -> the owner runs the deck beside a game once (task 9.4) and the result
  is recorded. The 3D share is small, but it is the same engine the game uses.
- **A time jump every four hours** for shaders that are not periodic -> documented; the built-ins are slow enough that
  one frame's discontinuity is hard to see. A periodic phase input can be added later.
- **Banding in dark gradients** at 8 bits -> not seen in the spike. If the owner sees it, add a one-line dither to the
  built-ins (a pixel hash of amplitude 1/255 added before the return); no host change needed.
- **The uniform parser is a regular expression, not a compiler** -> a declaration it cannot read (two uniforms on one
  line, a block comment around one) leaves the uniform at zero. The compiled effect's uniform names are compared with
  the parsed ones and each name the parser missed is logged, so the user is told.
- **Moving content under text** -> legibility is the theme's job (card fill opacity). The built-in palettes are dark
  and low in contrast for that reason.
- **Display scaling other than 100%** was not in the spike -> the surface size is logged and checked once at 150%
  (task 9.2).

## Migration Plan

Nothing to migrate. A configuration without `background` behaves as before, and so does a theme without
`backgroundPalette`. Rolling back is removing the `background` key.

## Open Questions

- Does `FrameClock` deliver 30 frames per second on the real host? Measured in task 9.5 and written to the backlog; it
  does not block this change.
