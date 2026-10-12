# Backgrounds

The deck can show a moving picture or a wallpaper behind its cards. It is one layer for the whole deck: pages slide over
it, cards are drawn on it with their own transparency (a translucent theme such as `glass` lets it show through), and it
never takes a swipe or a tap.

Without a `background` setting nothing of this exists: no surface, no frames, no cost.

## The `background` setting

A top-level object in `urdeck-config.json`. It applies when the file is saved, without a restart.

```json
"background": { "type": "shader", "name": "aurora", "speed": 0.5, "angle": 30 }
"background": { "type": "shader", "name": "plasma", "colors": ["#0f1a4d", "#8c268c", "#fa9e40"] }
"background": { "type": "shader", "name": "my-thing", "scale": 0.25, "density": 3.0, "glow": "#ff8800" }
"background": { "type": "image", "path": "D:/Pictures/wall.jpg" }
```

| Key | For | Meaning | Default | Valid |
|---|---|---|---|---|
| `type` | both | `shader` or `image` | required | those two |
| `name` | shader | a built-in name, or a folder in `backgrounds/` (see [Authoring backgrounds](#authoring-backgrounds)) | required | letters, digits, `-`, `_` |
| `speed` | shader | multiplies time: 2 is twice as fast, 0 stops it | `1` | 0 to 10 |
| `scale` | shader | the resolution it is drawn at, as a fraction of the panel's | `0.5` | 0.1 to 1 |
| `colors` | shader | replaces the theme's palette | the theme's | 1 to 4 colours |
| `path` | image | the picture | required | see [Images](#images) |
| any other key | shader | a parameter of that shader | the shader's default | |

A value that is wrong (not an object, an unknown `type`, a missing `name` or `path`) is logged and the deck shows the
theme's background colour. `speed` or `scale` out of range is logged and the default is used. A page's own `background`
is not supported: it is ignored, and `urdeck.log` says so; set the top-level one.

## Built-in shaders

All four draw only with the palette, so they follow the theme (see [Colours](#colours)). Each is a folder with a
`manifest.json`, which is the source of truth for its parameters and their ranges; this table is written by hand and a
later change will check it against the manifests.

| Name | Looks like | Parameters |
|---|---|---|
| `plasma` | slow warped fields of colour | `angle` (degrees, 0), `zoom` (3.0) |
| `aurora` | three glowing curtains swaying along the longer side | `intensity` (1.0) |
| `gradient` | a soft gradient whose bands drift | `angle` (degrees, 30) |
| `julia` | a Julia set whose constant circles slowly (the heaviest) | `zoom` (1.0), `radius` (0.7885) |

## Speed, scale and cost

A moving shader is drawn on the GPU about 30 times a second, at `scale` times the panel's size in each direction and
stretched smoothly to fill it. At the default half size the measured cost on the owner's panel is about 3% of one
CPU core and under 1% of the GPU, plus about 25 MB of memory ([the spike](perf/backgrounds-spike.md)).

- **`scale` is the remedy for a heavy shader.** If a shader is too heavy for your GPU, or a game beside the deck stutters,
  lower it (`0.25`). The shader never sees the real resolution, only `iResolution`, so nothing else changes. There is no
  automatic guard yet.
- Frames stop while the deck is hidden (its display is disconnected) and while Windows has switched the display off, and
  continue from where they stopped. `urdeck.log` says `Background frames started (...)` and `Background frames stopped
  (...): N frames in S s`.
- A background that does not move (an image, `speed` 0, a shader that does not use `iTime`) is drawn once and costs
  nothing afterwards.
- Time wraps to 0 after four hours of scaled time (`speed` times the seconds shown), so motion stays smooth after weeks of
  uptime. A shader that is not periodic over that span jumps once at the wrap; the built-ins are slow enough that it is
  hard to see. Time restarts at 0 only when the background itself changes, not on an unrelated config save.

## Colours

Every shader gets four opaque colours, `iColor0` to `iColor3`, from the first of these that applies:

1. `background.colors` in the configuration (one to four `#RRGGBB` colours; fewer than four repeat the last).
2. The theme's `colors.backgroundPalette` (see [Themes](themes.md#background-palette)).
3. Derived from the theme: its background colour mixed towards its accent by 0, 20, 40 and 60%.

An invalid colour drops the whole `colors` value with a warning. Transparency in a palette colour is ignored. A shader of
your own may ignore the palette and use its own colours.

## Images

`"type": "image"` shows a picture file covering the whole panel: scaled to the smaller size that fills both directions,
centred, the overflow cropped. A 3840x2160 picture on the 1100x3840 panel is scaled to the panel's height and its left and
right parts are cropped.

- `path` is absolute, or relative to the folder of `UrDeck.Host.exe`. Environment variables are expanded:
  `%USERPROFILE%/Pictures/wall.jpg`.
- PNG, JPEG, BMP and WebP files work, and the first frame of a GIF, up to 8192x8192 pixels. A photo's stored orientation
  (rotation) is applied.
- The picture is decoded once at the panel's size; the decoded original is released. While it loads the theme colour
  shows. Edit the file and save the config to load it again.
- Keys other than `type` and `path` are logged and ignored.

## Authoring backgrounds

A background is a **folder** next to `UrDeck.Host.exe`, `backgrounds/<name>/`, and the folder's name is what you select
with `"name": "<name>"`:

```
backgrounds/waves/
  manifest.json     required: what the background is called, what its parameters mean
  waves.sksl        the shader (or the file the manifest's "entry" names)
  sample.png        optional pictures, listed under "samples"
```

Copy a folder in to install it, delete it to remove it. A folder with the name of a built-in one replaces it. The
folder is kept when you install a copy with `scripts/Install-Local.ps1`. Folders are re-read whenever the configuration is
reloaded: after editing a file, save the config. The four built-ins are folders of the same shape inside the program
(`src/UrDeck.Engine/Backgrounds/Builtin/`): copy one to start.

A loose `backgrounds/<name>.sksl` is **not** loaded any more. If the configuration selects it, `urdeck.log` says which
folder to create and the deck shows the theme's colour.

### The minimal manifest

Copy this, save it as `manifest.json` next to your shader and change the words:

```json
{
  "$schema": "https://urdeck.app/schemas/v1/shader-manifest.json",
  "manifest": 1,
  "title": "Waves",
  "description": "Slow waves of colour."
}
```

Only `"manifest": 1` is required. Everything else makes the background easier to understand and to find in a settings
page later; leaving it out costs a lint warning (below), never the background. Comments (`//`) and trailing commas are
accepted, as in the configuration file.

### The manifest reference (version 1)

| Key | Type | Meaning |
|---|---|---|
| `$schema` | string | the schema's URL, for completion in an editor; ignored |
| `manifest` | integer | **required**, the version of the format: `1` |
| `title`, `description` | string | text for a person |
| `entry` | string | the shader file inside the folder; default `<name>.sksl`. No absolute path, no `..` |
| `author`, `license`, `version` | string | for a person and a store; never used to decide what runs |
| `tags` | array of strings | for finding it |
| `parameters` | object | annotations, keyed by the name of a uniform the shader declares (below) |
| `samples` | array | pictures of the background (below) |

A parameter's annotation holds `label`, `description` (text), `kind` (`number`, the default, or `color` for a three- or
four-component parameter), `min`, `max` and `step` (numbers; `min` and `max` apply to each component of a vector), `unit`
(text) and `hidden` (true keeps it out of an editor; the configuration can still set it). **The shader decides which
parameters exist, their types and their defaults; the manifest only annotates them.** A configured value outside `min` /
`max` logs a warning and the default is used, as `speed` and `scale` do; a parameter with neither is not checked.

A sample is `{ "type": "image", "file": "sample.png", "caption": "...", "order": 1 }`. `file` is inside the folder. A
`video` type is accepted for later and never played. Sample pictures are never read by the deck.

A key the format does not define, a `kind` or sample `type` it does not define, and a value of the wrong JSON type each
log one warning and are ignored, so a manifest written for a newer version still loads. The names `type`, `name`, `path`,
`speed`, `scale`, `colors` and the host inputs (`iTime`, `iResolution`, `iColor0` to `iColor3`) belong to the product and
cannot be annotated.

What stops a background is short: no `manifest.json`, one that is not a JSON object, a `manifest` that is not the integer
1, an `entry` that is missing or leaves the folder, a shader that does not compile, a `uniform shader`. Everything else
shows the background and logs a warning.

### Lint

When a background loads, `urdeck.log` gets one line per finding, once per change of the background's files or settings:

```
[2026-10-12 11:04:12.311] WARN Background 'waves': SHADER042 parameter 'density' has no description. (manifest.json)
```

The same check is `ShaderLint.Check` in the engine, which a command line tool will call. The codes never change meaning.
**error** means the folder is broken or breaks a rule (a store would turn it away); only the codes marked "stops" also stop
the background. **warning** means it works but could be better described. **info** is a remark.

| Code | Severity | Meaning and how to fix it |
|---|---|---|
| SHADER001 | error, stops | `manifest.json` is missing: add the file (see the minimal manifest) |
| SHADER002 | error, stops | not valid JSON, or the top level is not an object: fix the syntax at the position given |
| SHADER003 | error, stops | `manifest` is missing, not an integer, or not a version this product reads: use `"manifest": 1` |
| SHADER004 | warning | an unknown key in the manifest, a parameter or a sample: fix the spelling or remove it |
| SHADER005 | warning | a value has the wrong JSON type (a number where text is expected): fix the value |
| SHADER010 | error, stops | the entry file does not exist: create it, or fix `entry` |
| SHADER011 | error, stops | `entry` is absolute or leaves the folder: name a file inside it |
| SHADER020 | error, stops | the shader does not compile: the compiler's message follows, with its line number |
| SHADER021 | error, stops | the shader declares a `uniform shader`: the host has nothing to give it; remove it |
| SHADER022 | warning | a uniform has a type that cannot be set (`int`, a matrix): it stays 0; use `float` |
| SHADER023 | warning | a `// default:` cannot be read: give numbers matching the type, or a `#RRGGBB` colour |
| SHADER030 | warning | the manifest annotates a parameter the shader does not declare: fix the name |
| SHADER031 | warning | the shader declares a parameter the manifest does not annotate: add it under `parameters` |
| SHADER032 | error | the manifest annotates a name that belongs to the product (`speed`, `iTime`, ...): remove it |
| SHADER033 | warning | `kind` `color` on a parameter without three or four components: use `float3` or `float4` |
| SHADER034 | warning | an unknown `kind`: use `number` or `color` |
| SHADER035 | warning | `min` is above `max`, or the shader's default is outside the range: fix the range or the default |
| SHADER040 | warning | no `title` |
| SHADER041 | warning | no `description` |
| SHADER042 | warning | a parameter that is not `hidden` has no `description` |
| SHADER050 | warning | no `samples`: render a picture and list it |
| SHADER051 | error | a sample file does not exist: create it or fix `file` |
| SHADER052 | error | a sample `file` is absolute or leaves the folder: name a file inside it |
| SHADER053 | warning | an unknown sample `type`: use `image` (or `video`) |
| SHADER060 | info | the shader says `layout(color)` and the manifest says `kind` `number`: the manifest wins |

The four built-ins produce none of these; a test requires it.

### The shader

A shader is [SkSL](https://skia.org/docs/user/sksl/), written like GLSL, with one function:

```glsl
half4 main(float2 fragCoord)
```

`fragCoord` is in pixels of the render surface, origin top left. The result is shown opaque over the theme's background
colour: where you return transparency, the theme colour shows.

**Inputs the host sets**, each only if you declare it with this type:

| Uniform | Type | Value |
|---|---|---|
| `iTime` | `float` | seconds, times `speed`, wrapped as above |
| `iResolution` | `float2` | the render surface's size in pixels |
| `iColor0` to `iColor3` | `float4` | the palette, red, green, blue, alpha from 0 to 1 |

**Parameters** are every other uniform you declare, one per line: `float`, `float2`, `float3`, `float4` (and the aliases
`vec2`, `vec3`, `vec4`, `half`, `half2`, `half3`, `half4`). A configuration key with the same name sets it: a number for
`float`, an array of numbers for a vector, a `#RRGGBB` string for a three- or four-component one. Give a default in a
trailing comment: a number, comma-separated numbers, or a colour. A declaration may start with `layout(...)`, as shaders
written for Skia or Flutter do: `layout(color) uniform half4 glow;` is read as a colour parameter (an editor offers a colour
picker), and the manifest's `kind` wins if it says otherwise.

```glsl
uniform float iTime;
uniform float2 iResolution;
uniform float4 iColor0;
uniform float4 iColor1;
uniform float density;                      // default: 6.0
layout(color) uniform float4 glow;          // default: #ff8800

half4 main(float2 fragCoord) {
    float2 uv = fragCoord / iResolution;
    float v = 0.5 + 0.5 * sin(uv.y * density + iTime);
    float3 col = mix(iColor0.rgb, iColor1.rgb, v);
    col += glow.rgb * 0.15 * smoothstep(0.8, 1.0, v);
    return half4(half3(col), 1.0);
}
```

with this `manifest.json`:

```json
{
  "manifest": 1,
  "title": "Waves",
  "description": "Slow bands of colour with a glow on the crests.",
  "parameters": {
    "density": { "label": "Density", "description": "How many bands fit on the display.", "min": 1, "max": 20, "step": 0.5 },
    "glow": { "label": "Glow", "description": "The colour of the crests." }
  },
  "samples": [{ "type": "image", "file": "sample.png" }]
}
```

Select it with `{ "type": "shader", "name": "waves", "density": 9, "glow": "#00ccff" }`.

Parameters are read from the source text, one `uniform` per line. A declaration it cannot read (two on one line, one inside
a block comment) stays 0 and `urdeck.log` names it. Other uniform types (`int`, matrices, arrays) stay 0 with a warning, and a
`uniform shader` makes the file unusable: there is nothing to give it.

**Porting from Shadertoy** takes three lines: rename `mainImage(out vec4 fragColor, in vec2 fragCoord)` to
`half4 main(float2 fragCoord)` returning the colour, declare `uniform float iTime; uniform float2 iResolution;` (the names
match), and replace `vec` with `float` types where SkSL wants them (`vec3` works as an alias). Shaders that read textures
or the mouse (`iChannel0`, `iMouse`) cannot be ported as they are. Then add a `manifest.json`.

Keep it cheap: a loop of a hundred iterations per pixel is fine at `scale` 0.5 and may not be at 1.

### Regenerating the built-in samples

The pictures in `src/UrDeck.Engine/Backgrounds/Builtin/<name>/sample.png` (270 x 480, the default theme, `iTime` 10) are made
by `BackgroundRender`. Until the command line tool exists, a helper in the test project renders them; it only runs when asked:

```powershell
dotnet test tests/UrDeck.Engine.Tests -c Release --filter "FullyQualifiedName~BuiltInSampleTool" -- --explicit on
```

Run it after changing a built-in shader, look at the four pictures, and commit them with the change.

## When something is wrong

The deck shows the theme's background colour, the widgets are unaffected, and `urdeck.log` has one line that starts with
`Background` and names the cause:

- an unknown shader name, a folder that cannot be read, or a loose `.sksl` file (the line names the folder to make);
- a missing or unreadable `manifest.json` (`SHADER001` to `SHADER003`), or an `entry` that is missing or leaves the folder;
- SkSL that does not compile (the compiler's message is in the line, with its line number);
- a picture that is missing, too large, or not an image (its path is in the line);
- a failure while painting, or a GPU surface that does not start.

Warnings that do not stop the background (a parameter without a description, an unknown manifest key) are logged the same
way, with their `SHADERnnn` code: see [Lint](#lint). A background that failed is not tried again until its configuration,
its files or the theme changes; fix the file and save the config.

`UrDeck.Host.exe --snapshot out.png` draws the background too (a shader at `iTime` 10, whatever the speed, with the palette of the
theme in use), so a snapshot with the same configuration is the same every time. The snapshot is drawn on the CPU and does
not need the GPU.
