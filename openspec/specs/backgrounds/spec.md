# Backgrounds Specification

## Purpose

Defines what the deck shows behind its cards: the `background` setting, generated (shader) and picture backgrounds, the
inputs and parameters of a shader, the colour palette, frame pacing and render scale, when frames are produced, failure
behaviour, the background in snapshots and its cost budget.
## Requirements
### Requirement: Background Setting
The configuration MUST accept an optional top-level `background` object that selects what the deck shows behind its
cards:

- `type` is `shader` (a generated, possibly moving picture; needs `name`), `image` (a picture file; needs `path`) or
  `none` (no background: nothing else is needed)
- A configuration without `background`, or with `background` set to `null`, leaves the choice to the active theme (see
  "Theme Backgrounds"): the theme's default background if it has one, else its still background, else its background
  colour. A theme that defines no background shows its background colour, as it did before themes could choose
- `none` shows the theme's background colour and nothing else: no theme default, no still background and no scrim
- A `background` that is not an object, has an unknown `type`, or lacks the key its type needs logs a warning and
  counts as a background that failed (see "Background Failures"). It MUST NOT make the configuration fail to load
- The background is selected on startup and again on every configuration reload
- A page's own `background` value is not supported: it is ignored, and one log line per reload says so

#### Scenario: No background configured
- **WHEN** the configuration has no `background` and the theme defines no background
- **THEN** the deck shows the theme's background colour and nothing else is created or run for a background

#### Scenario: No background configured, theme has one
- **WHEN** the configuration has no `background` and the theme's default background is the shader `horizon`
- **THEN** the deck shows `horizon` behind its cards

#### Scenario: Background switched off
- **WHEN** the configuration has `"background": { "type": "none" }` and the theme's default is `horizon`
- **THEN** the deck shows the theme's background colour and nothing is run for a background

#### Scenario: Shader selected
- **WHEN** the configuration has `"background": { "type": "shader", "name": "plasma" }`
- **THEN** the deck shows the built-in `plasma` background behind its cards, whatever the theme's default is

#### Scenario: Background changed while running
- **WHEN** `background.name` is changed from `plasma` to `aurora` and the file is saved
- **THEN** the deck shows `aurora` without a restart

#### Scenario: Background removed while running
- **WHEN** the `background` key is deleted from the configuration and saved
- **THEN** the deck shows the theme's choice without a restart

#### Scenario: Unknown type
- **WHEN** the configuration has `"background": { "type": "video", "path": "a.mp4" }`
- **THEN** a warning is logged, the deck shows what a failed background shows, and the rest of the configuration is used

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

- The product ships sixteen built-in shaders, available regardless of the user's folders: `plasma`, `aurora`,
  `gradient` and `julia`, and `horizon`, `spectrum`, `contour`, `nebula`, `streams`, `ripple`, `embers`, `plotter`,
  `shapes`, `warpgate`, `equalizer` and `vortex`
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
- A built-in shader that moves repeats seamlessly across the time wrap: every period in it divides the wrap of
  14,400 seconds, so nothing jumps when the clock wraps

#### Scenario: Built-in shaders without a folder
- **WHEN** the `backgrounds` folder does not exist
- **THEN** each of the sixteen built-in shaders can be selected

#### Scenario: New built-in shader
- **WHEN** the configuration selects `"background": { "type": "shader", "name": "horizon" }`
- **THEN** the deck shows the retro horizon behind its cards, in the theme's palette

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

#### Scenario: Time wrap
- **WHEN** a built-in shader that moves is drawn at time 0 and at time 14,400 seconds with the same parameters
- **THEN** the two pictures are the same to within a rounding tolerance

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
A background that cannot be shown MUST leave the deck working, showing the next thing in the theme's list, and at the
end the theme's background colour:

- The cases: an unknown shader name, a shader file that cannot be read, SkSL that does not compile, a shader that
  needs an input the host cannot give (a child shader), an image that is missing or cannot be decoded, a failure
  while drawing, and a GPU surface that cannot be created
- Each logs one warning that names the background and the cause; for SkSL that does not compile, the compiler's
  message is included
- What is shown instead is the theme's still background if it has one and it can be shown, else the theme's background
  colour. A theme's default background that fails is replaced the same way
- A shader still compiling shows the same until it is ready
- Widgets, the pager and the dock are unaffected
- A background that failed is not tried again until the background's configuration, its file or the theme changes

#### Scenario: Shader does not compile
- **WHEN** a user shader has a syntax error and the theme defines no still background
- **THEN** the log has a warning with the compiler's message, the deck shows the theme's background colour and every
  widget is shown

#### Scenario: Shader does not compile, theme has a still background
- **WHEN** the user's shader does not compile and the theme's still background is a gradient
- **THEN** the log has the warning and the deck shows the gradient

#### Scenario: Unknown shader
- **WHEN** the configuration selects the shader `nebula` and neither a built-in nor a user file has that name
- **THEN** a warning names `nebula` and the deck shows what a failed background shows

#### Scenario: Missing image
- **WHEN** the image's `path` names a file that does not exist
- **THEN** a warning names the path and the deck shows what a failed background shows

#### Scenario: The still background fails too
- **WHEN** the theme's still background is a picture that is missing and the user's shader failed
- **THEN** both are logged and the deck shows the theme's background colour

#### Scenario: Fixed shader
- **WHEN** the user corrects the shader file and saves the configuration
- **THEN** the background is shown

### Requirement: Background In Snapshots
`--snapshot` MUST draw the background the deck would show, with the engine alone and without a GPU:

- A shader is drawn at a fixed time (`iTime` of 10) with the configuration's parameters and scale, and the palette of
  the theme the snapshot uses, so the same configuration gives the same picture every time
- An image is drawn scaled to cover the snapshot's size
- A theme's still background and its scrim are drawn as the deck draws them
- A background that fails is handled as in "Background Failures"

#### Scenario: Snapshot with a shader
- **WHEN** the host is run with `--snapshot out.png` and the configuration selects `plasma`
- **THEN** the PNG shows `plasma` behind the cards, and a second run writes the same background

#### Scenario: Snapshot with a named theme
- **WHEN** the host is run with `--snapshot out.png --theme default-light` and the background has no `colors`
- **THEN** the background is drawn in the light theme's palette

#### Scenario: Snapshot without a background
- **WHEN** the configuration has no `background` and the theme defines none
- **THEN** the snapshot is the same as before this capability existed

#### Scenario: Snapshot of a theme's default
- **WHEN** the configuration has no `background` and the theme's default is the shader `horizon`
- **THEN** the PNG shows `horizon` in the theme's palette, and a second run writes the same picture

### Requirement: Background Cost
A background MUST stay within a measured budget on the owner's panel (1100x3840), compared with the same page without
a background:

- No background: no difference
- A still background: no difference in CPU and GPU use once it is drawn
- A moving built-in background at the default scale: at most 5% of one core, 2% of the GPU's 3D engine, 60 MB of
  private memory and 60 MB of dedicated video memory more. This holds for each of the sixteen built-ins; one that is
  over is shipped with a lighter default, never heavy
- The measurements are recorded in `docs/perf/backgrounds.md` or a note beside it with the method of
  `docs/perf/nexus-baseline.md`

#### Scenario: Moving background at the default scale
- **WHEN** `plasma` is shown at the default scale on the owner's usual page
- **THEN** the host's CPU, GPU and memory use are within the budget above the same page without a background

#### Scenario: A heavy new built-in
- **WHEN** `nebula` or `shapes` is shown at the default scale on the owner's usual page
- **THEN** the host's CPU, GPU and memory use are within the budget above the same page without a background, or its
  default parameters were lightened until they are

### Requirement: Theme Backgrounds
The deck MUST show the background that a theme chooses with its `background` group (see theme, "Theme Value Groups"),
unless the configuration chooses one:

- `default.name` is the name of a shader background that the deck shows when the configuration has no `background`
- `static` is a still background: a `gradient` (linear with an `angle`, or radial with a centre and a radius), a
  `color`, or an `image`, a picture file inside the theme's folder drawn scaled to cover the deck. A picture path that
  leaves the theme's folder is ignored with a logged warning; the limits and formats of "Image Backgrounds" apply
- A theme may define either, both or neither

What is shown is the first of these that applies:

1. The configuration's `background`, `none` included
2. The theme's `default`
3. The theme's `static`
4. The theme's background colour

and a background that fails passes to the next in the list (see "Background Failures"). A shader the theme names uses
the theme's background palette, as any shader does (see "Palette"); a shader the user chooses keeps that palette too,
unless the user gives `colors`. The theme's default and still background are the theme's choice only: they are not shown
when the user's configuration selects another background, or `none`.

- A theme's `default` name that no background has is a failed background, with the warning of "Background Failures"
- The effective background is logged once whenever it changes, with its kind, its name and where the choice came from:
  `configuration`, `theme default`, `theme still background` or `theme colour`
- A still gradient, colour or picture is drawn once, and again when the panel's size, the background or the theme
  changes; it creates no frame source and no GPU rendering context
- The theme's `motion.level` does not affect the background

#### Scenario: Theme default
- **WHEN** the theme's default is `horizon` and the configuration has no `background`
- **THEN** `horizon` is shown in the theme's palette and the log says it came from the theme default

#### Scenario: User choice beats the theme
- **WHEN** the theme's default is `horizon` and the configuration selects `plasma`
- **THEN** `plasma` is shown, in the theme's palette

#### Scenario: Still gradient
- **WHEN** a theme defines no default and its still background is a radial gradient
- **THEN** the gradient is drawn once behind the cards, and no frames are produced

#### Scenario: Still background while a default fails
- **WHEN** a theme's default shader does not compile and the theme has a still colour
- **THEN** the log has the warning and the deck shows the colour

#### Scenario: Theme changed
- **WHEN** the theme is changed from one with a default background to one without any
- **THEN** the background of the first stops and the second theme's background colour is shown, without a restart

#### Scenario: Unrelated change
- **WHEN** the configuration is saved with only a widget's setting changed
- **THEN** the background is not restarted

#### Scenario: Picture outside the theme folder
- **WHEN** a theme's still picture is `../secret.png`
- **THEN** the picture is ignored, one warning says so, and the next thing in the list is shown

#### Scenario: Reduced motion does not stop the background
- **WHEN** a theme sets `motion.level` to `off` and its default is a moving shader
- **THEN** the shader still moves

### Requirement: Background Scrim
The deck MUST draw a theme's `background.scrim`, a colour and an alpha from 0 to 1, between the background and the
cards, so that text keeps its contrast over a bright or busy background.

- It is drawn over the shader, the picture and the still background of whichever of them is shown, whoever chose it
- It is not drawn over the plain background colour, and not when the configuration has `"type": "none"`
- The colour's own transparency multiplies the alpha
- It is a veil over the whole deck and is drawn in the background's surface; it creates no layer of its own and takes
  no pointer input

#### Scenario: Scrim over a shader
- **WHEN** a theme's scrim is black at an alpha of 0.2 and a shader is shown
- **THEN** the shader is drawn 20% darker, in the deck and in a snapshot

#### Scenario: Scrim with no background
- **WHEN** the configuration has `"type": "none"`
- **THEN** no scrim is drawn

#### Scenario: No scrim
- **WHEN** a theme has no scrim
- **THEN** the background is drawn as it is

### Requirement: Reduced Motion Stills The Background
When the user's choice of motion level (the configuration's `motion` value, or Windows' animation setting when it is
absent; see `motion`) is `reduced` or `off`, the deck MUST draw a moving shader background still, exactly as it draws that
background with `speed` 0: once, with no frame source.

- The theme's `motion.level` is not part of this decision: a theme's ceiling never stops the background (see "Theme
  Backgrounds")
- The background is chosen as before (the same candidate, palette, parameters, scale and scrim); only its motion stops
- When the user's choice returns to `full`, the background moves again with time restarted at 0
- A snapshot uses the configuration's `motion` value and ignores Windows' setting, so a snapshot is the same on every
  machine

#### Scenario: Windows animations off
- **WHEN** the configuration has no `motion`, Windows' animation effects are off and the background is a moving shader
- **THEN** the shader is drawn once and no background frames are produced

#### Scenario: User wants motion
- **WHEN** the configuration has `"motion": "full"`, Windows' animation effects are off and the background is a moving
  shader
- **THEN** the shader moves

#### Scenario: Theme ceiling only
- **WHEN** the configuration has `"motion": "full"`, the theme sets `motion.level` to `off` and its default background is
  a moving shader
- **THEN** the shader moves, and the theme's animated border (if any) is still

#### Scenario: Snapshot with reduced motion
- **WHEN** a snapshot is taken with `"motion": "off"` in the configuration and a moving shader background
- **THEN** the background in the picture is the one `speed` 0 draws

