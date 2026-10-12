# Backgrounds Specification

## Purpose

Defines what the deck shows behind its cards: the `background` setting, generated (shader) and picture backgrounds, the
inputs and parameters of a shader, the colour palette, frame pacing and render scale, when frames are produced, failure
behaviour, the background in snapshots and its cost budget.
## Requirements
### Requirement: Background Setting
The configuration MUST accept an optional top-level `background` object that selects what the deck shows behind its
cards:

- `type` is `shader` (a generated, possibly moving picture; needs `name`) or `image` (a picture file; needs `path`)
- A configuration without `background`, or with `background` set to `null`, has no background: the deck shows the
  theme's background colour, as it did before this capability existed
- A `background` that is not an object, has an unknown `type`, or lacks the key its type needs logs a warning and
  counts as no background. It MUST NOT make the configuration fail to load
- The background is selected on startup and again on every configuration reload
- A page's own `background` value is not supported: it is ignored, and one log line per reload says so

#### Scenario: No background configured
- **WHEN** the configuration has no `background`
- **THEN** the deck shows the theme's background colour and nothing else is created or run for a background

#### Scenario: Shader selected
- **WHEN** the configuration has `"background": { "type": "shader", "name": "plasma" }`
- **THEN** the deck shows the built-in `plasma` background behind its cards

#### Scenario: Background changed while running
- **WHEN** `background.name` is changed from `plasma` to `aurora` and the file is saved
- **THEN** the deck shows `aurora` without a restart

#### Scenario: Unknown type
- **WHEN** the configuration has `"background": { "type": "video", "path": "a.mp4" }`
- **THEN** a warning is logged, the deck shows the theme's background colour, and the rest of the configuration is used

#### Scenario: Background on a page
- **WHEN** a page in the configuration has its own `background`
- **THEN** it has no effect and the log says that only the top-level `background` is supported

### Requirement: Deck-Wide Layer
The background MUST be one layer for the whole deck, under every page, the dock and the page indicator:

- It does not move, change or restart when the page changes; pages slide over it
- Cards are drawn over it with their own transparency
- It never receives pointer input: swipes and taps over the background behave as they do without one

#### Scenario: Page swipe
- **WHEN** the user swipes from one page to the next while a moving background is shown
- **THEN** the pages slide and the background stays in place and keeps moving

#### Scenario: Translucent card
- **WHEN** the theme's card fill is translucent and a background is shown
- **THEN** the background is visible through the cards

#### Scenario: Swipe that starts on the background
- **WHEN** a horizontal swipe starts where no card is
- **THEN** the page changes as it does without a background

### Requirement: Shader Backgrounds
A `shader` background MUST be an SkSL program selected by `name`:

- The product ships four built-in shaders, `plasma`, `aurora`, `gradient` and `julia`, available regardless of the
  user's folders
- A folder `backgrounds/<name>/` next to the executable that holds a manifest and the entry shader is a user shader,
  selected by `<name>` exactly like a built-in one (see background-manifest, "Background Folder"). A user shader with
  the name of a built-in one takes precedence over it
- A name MUST consist of letters, digits, `-` and `_`. Any other name is rejected before a file is touched, so a name
  cannot refer to a file outside the `backgrounds` folder
- A shader defines `half4 main(float2 fragCoord)`, where `fragCoord` is in pixels of the render surface with the
  origin at the top left
- Shader folders are re-read when the configuration is reloaded
- The shader's result is shown opaque over the theme's background colour: where it returns transparency, the theme
  colour shows

#### Scenario: Built-in shaders without a folder
- **WHEN** the `backgrounds` folder does not exist
- **THEN** each of the four built-in shaders can be selected

#### Scenario: User shader
- **WHEN** `backgrounds/waves/` holds `manifest.json` and `waves.sksl` and the configuration selects the shader `waves`
- **THEN** the deck shows that shader's output

#### Scenario: User shader replaces a built-in one
- **WHEN** `backgrounds/plasma/` is a valid background folder and the configuration selects `plasma`
- **THEN** the user's folder is used

#### Scenario: Name that leaves the folder
- **WHEN** the configuration selects the shader `../themes/x`
- **THEN** no file is read, a warning is logged and the deck shows the theme's background colour

#### Scenario: Edited user shader
- **WHEN** a user shader file is edited and the configuration file is saved
- **THEN** the deck shows the edited shader

### Requirement: Shader Inputs
The host MUST set the following inputs on a shader, each only when the shader declares a uniform of that name and type:

- `iTime` (`float`): seconds, multiplied by the background's speed (see "Speed And Time")
- `iResolution` (`float2`): the size of the render surface in pixels
- `iColor0`, `iColor1`, `iColor2`, `iColor3` (`float4`): the palette (see "Palette"), as red, green, blue and alpha
  between 0 and 1, not premultiplied

A shader that declares none of them is valid. A configuration key MUST NOT be able to set one of these inputs.

#### Scenario: Shader without inputs
- **WHEN** a user shader declares no uniforms and returns one colour
- **THEN** the deck shows that colour as a still background

#### Scenario: Resolution follows the surface
- **WHEN** a shader is rendered at half the panel's resolution
- **THEN** `iResolution` is the halved size, and a shader that divides `fragCoord` by it covers the whole panel

#### Scenario: Configuration tries to set an input
- **WHEN** the configuration's background has `"iTime": 5`
- **THEN** a warning is logged and `iTime` keeps the host's value

### Requirement: Shader Parameters
Every uniform a shader declares besides the host's inputs MUST be a parameter that the configuration can set by name:

- Supported types are `float`, `float2`, `float3` and `float4` (and the aliases `vec2`, `vec3`, `vec4`, `half`,
  `half2`, `half3`, `half4`)
- A declaration MAY start with `layout(...)`, as shaders written for Skia do; the declaration is read as if it did not.
  `layout(color)` marks a three- or four-component parameter as a colour, as the manifest's `kind` `color` does; the
  manifest's `kind` wins when both are given
- A declaration MAY give a default in a trailing comment on its line, `uniform float density;   // default: 3.0`.
  The default is a number, comma-separated numbers matching the type, or a `#RRGGBB` / `#AARRGGBB` colour for a
  three- or four-component type. A parameter without a default is 0
- A key of the `background` object that is not one of the setting's own keys (`type`, `name`, `path`, `speed`,
  `scale`, `colors`) sets the parameter of that name: a number for `float`, an array of numbers of the right length
  for a vector, or a colour string for a three- or four-component type
- A value of the wrong form, a number that is not finite, a key that names no parameter, a default that cannot be
  read and a uniform of an unsupported type each log one warning; the parameter keeps its default (or 0)
- A value outside the range the manifest gives the parameter is handled as background-manifest, "Parameter Ranges"
  says
- What the parameters mean (labels, descriptions, ranges) is described by the background's manifest, never by the
  shader's text

#### Scenario: Parameter set from the configuration
- **WHEN** a shader declares `uniform float angle;   // default: 30` and the configuration's background has `"angle": 75`
- **THEN** the shader runs with `angle` at 75

#### Scenario: Default applies
- **WHEN** the configuration does not mention `angle`
- **THEN** the shader runs with `angle` at 30

#### Scenario: Colour parameter
- **WHEN** a shader declares `uniform float4 glow;` and the configuration's background has `"glow": "#ff8800"`
- **THEN** the shader receives `glow` as (1, 0.533, 0, 1)

#### Scenario: Layout prefix
- **WHEN** a shader declares `layout(color) uniform half4 tint;   // default: #ff8800`
- **THEN** `tint` is a parameter with that default, and a reader of the background sees it as a colour

#### Scenario: Key without a parameter
- **WHEN** the configuration's background has `"densty": 3` and the shader declares no uniform of that name
- **THEN** a warning names the key and the background is shown with its other values

#### Scenario: Wrong form
- **WHEN** the configuration gives `"angle": "steep"` for a `float` parameter
- **THEN** a warning is logged and `angle` keeps its default

### Requirement: Speed And Time
A shader background's time MUST be controlled by the host:

- `speed` multiplies time: 1 is the shader's own pace, 2 twice as fast. It defaults to 1 and MUST be between 0 and 10;
  a value outside that logs a warning and 1 is used
- A background is moving when it is a shader, its speed is above 0 and the shader declares `iTime`. Every other
  background is still
- Time advances only while frames are produced: after the deck was hidden or the display was off, motion continues
  from where it stopped
- The value passed as `iTime` MUST stay precise enough for smooth motion however long the host has run. It wraps to 0
  after 14400 seconds of scaled time; a shader that is not periodic over that span shows one discontinuity there
- Time restarts at 0 only when the background itself changes (its kind, shader, shader text, parameters, speed, scale
  or palette). A configuration reload that leaves the background as it was MUST NOT restart or interrupt it

#### Scenario: Speed
- **WHEN** `speed` is 2
- **THEN** the background moves twice as fast as with `speed` 1

#### Scenario: Speed zero
- **WHEN** `speed` is 0
- **THEN** the background is drawn once and is still (see "When Frames Are Produced")

#### Scenario: Unrelated configuration change
- **WHEN** a widget's setting is edited and the file is saved while a moving background is shown
- **THEN** the background keeps moving without a jump

#### Scenario: Long uptime
- **WHEN** a moving background has been shown for three days
- **THEN** it moves as smoothly as in its first minute

#### Scenario: Speed out of range
- **WHEN** `speed` is -1
- **THEN** a warning is logged and the background moves at speed 1

### Requirement: Palette
Every shader background MUST receive a palette of four opaque colours, taken from the first of these that applies:

1. `background.colors` in the configuration: an array of one to four colours (`#RRGGBB` or `#AARRGGBB`)
2. The active theme's background palette (see theme, "Background Palette")
3. A palette derived from the active theme's own colours (see theme, "Background Palette")

- Fewer than four colours repeat the last one; more than four use the first four with a logged warning
- A `colors` value with an invalid colour, or that is not an array, is dropped as a whole with a logged warning
- Transparency in a palette colour is ignored
- The built-in shaders draw only with the palette, so they follow the theme. A user shader MAY ignore it

#### Scenario: Built-in follows the theme
- **WHEN** the theme is changed from `default-dark` to `default-light` while `plasma` is shown without `colors`
- **THEN** `plasma` is drawn in the light theme's palette

#### Scenario: Colours from the configuration
- **WHEN** the background has `"colors": ["#003300", "#00aa55"]`
- **THEN** the shader receives those two colours as `iColor0` and `iColor1`, and `#00aa55` again as `iColor2` and `iColor3`

#### Scenario: Invalid colour
- **WHEN** the background has `"colors": ["#003300", "green"]`
- **THEN** a warning is logged and the theme's palette is used

### Requirement: Image Backgrounds
An `image` background MUST show a picture file of the user's:

- `path` is absolute or relative to the application folder; environment variables in it are expanded
- PNG, JPEG, BMP and WebP files are supported, and the first frame of a GIF; a photo's stored orientation is applied
- The picture is scaled to cover the whole panel, keeping its proportions, centred; what overflows is cropped
- An image is a still background
- The picture is loaded without blocking the deck: until it is ready the theme's background colour shows
- Keys other than `type` and `path` log a warning and are ignored

#### Scenario: Wallpaper
- **WHEN** the configuration has `"background": { "type": "image", "path": "%USERPROFILE%/Pictures/wall.jpg" }`
- **THEN** the picture fills the panel behind the cards

#### Scenario: Picture wider than the panel
- **WHEN** a 3840x2160 picture is shown on the 1100x3840 panel
- **THEN** it is scaled to the panel's height, centred, and its left and right parts are cropped

#### Scenario: Relative path
- **WHEN** `path` is `wall.png` and that file is next to the executable
- **THEN** that file is shown

### Requirement: Frame Pacing And Render Scale
A moving background MUST be drawn on the GPU at a bounded rate and resolution:

- It is redrawn about 30 times per second while frames are produced, from its own frame source; the widgets' frame
  source and refresh timers are not driven by it and it is not driven by them
- `scale` is the resolution it is rendered at, as a fraction of the panel's physical resolution in each dimension; the
  result is stretched smoothly over the panel. It defaults to 0.5 and MUST be between 0.1 and 1; a value outside that
  logs a warning and 0.5 is used
- The user never states a resolution: `scale` is relative, and the same configuration works on any display
- When a background is applied, the log states its kind, its name or path, the render surface's size in pixels, the
  scale and whether it is moving

#### Scenario: Default scale
- **WHEN** a moving background is shown on the 1100x3840 panel without `scale`
- **THEN** it is rendered at 550x1920, covers the whole panel, and the log says so

#### Scenario: Full resolution
- **WHEN** `scale` is 1
- **THEN** it is rendered at 1100x3840

#### Scenario: Frame rate
- **WHEN** a moving background has been shown for one minute
- **THEN** about 1800 frames were produced

#### Scenario: Widgets are not repainted
- **WHEN** a moving background is shown under a page whose widgets are not animating
- **THEN** no widget is repainted by the background's frames and the widgets' frame source is not running

### Requirement: When Frames Are Produced
The host MUST produce background frames only while they can be seen and are needed:

- A still background is drawn once (and again when the panel's size or the background changes); it has no frame
  source and creates no GPU rendering context
- A moving background produces no frames while the deck is hidden, and none while the display is switched off
- When the deck is hidden long enough for its page to be released, the background's surface is released with it
- Without a background nothing of this exists: no surface, no frame source
- Each start and stop is logged once with its reason; a stop also logs how many frames were produced and over how
  long. Nothing is logged while nothing changes

#### Scenario: Image
- **WHEN** an image background is shown and no widget animates
- **THEN** the host's CPU and GPU use are not measurably above those of the same page without a background

#### Scenario: Deck hidden
- **WHEN** the deck's display is disconnected while a moving background is shown
- **THEN** no further frames are produced, and the log says the frames stopped and why

#### Scenario: Display switched off
- **WHEN** Windows switches the display off after its idle time while a moving background is shown
- **THEN** no further frames are produced until the display is on again, when the motion continues

#### Scenario: Back from sleep
- **WHEN** the PC wakes from sleep and the deck is shown again
- **THEN** the background is shown and moving again

### Requirement: Background Failures
A background that cannot be shown MUST leave the deck working with the theme's background colour:

- The cases: an unknown shader name, a shader file that cannot be read, SkSL that does not compile, a shader that
  needs an input the host cannot give (a child shader), an image that is missing or cannot be decoded, a failure
  while drawing, and a GPU surface that cannot be created
- Each logs one warning that names the background and the cause; for SkSL that does not compile, the compiler's
  message is included
- Widgets, the pager and the dock are unaffected
- A background that failed is not tried again until the background's configuration, its file or the theme changes

#### Scenario: Shader does not compile
- **WHEN** a user shader has a syntax error
- **THEN** the log has a warning with the compiler's message, the deck shows the theme's background colour and every
  widget is shown

#### Scenario: Unknown shader
- **WHEN** the configuration selects the shader `nebula` and neither a built-in nor a user file has that name
- **THEN** a warning names `nebula` and the deck shows the theme's background colour

#### Scenario: Missing image
- **WHEN** the image's `path` names a file that does not exist
- **THEN** a warning names the path and the deck shows the theme's background colour

#### Scenario: Fixed shader
- **WHEN** the user corrects the shader file and saves the configuration
- **THEN** the background is shown

### Requirement: Background In Snapshots
`--snapshot` MUST draw the configured background behind the page, with the engine alone and without a GPU:

- A shader is drawn at a fixed time (`iTime` of 10) with the configuration's parameters and scale, and the palette of
  the theme the snapshot uses, so the same configuration gives the same picture every time
- An image is drawn scaled to cover the snapshot's size
- A background that fails is handled as in "Background Failures": the snapshot is written with the theme's background
  colour

#### Scenario: Snapshot with a shader
- **WHEN** the host is run with `--snapshot out.png` and the configuration selects `plasma`
- **THEN** the PNG shows `plasma` behind the cards, and a second run writes the same background

#### Scenario: Snapshot with a named theme
- **WHEN** the host is run with `--snapshot out.png --theme default-light` and the background has no `colors`
- **THEN** the background is drawn in the light theme's palette

#### Scenario: Snapshot without a background
- **WHEN** the configuration has no `background`
- **THEN** the snapshot is the same as before this capability existed

### Requirement: Background Cost
A background MUST stay within a measured budget on the owner's panel (1100x3840), compared with the same page without
a background:

- No background: no difference
- A still background: no difference in CPU and GPU use once it is drawn
- A moving built-in background at the default scale: at most 5% of one core, 2% of the GPU's 3D engine, 60 MB of
  private memory and 60 MB of dedicated video memory more
- The measurements are recorded in `docs/perf/backgrounds.md` with the method of `docs/perf/nexus-baseline.md`

#### Scenario: Moving background at the default scale
- **WHEN** `plasma` is shown at the default scale on the owner's usual page
- **THEN** the host's CPU, GPU and memory use are within the budget above the same page without a background

#### Scenario: Background removed
- **WHEN** `background` is removed from the configuration while the host runs
- **THEN** CPU and GPU use return to the level of a page without a background
