## MODIFIED Requirements

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

## ADDED Requirements

### Requirement: Theme Backgrounds
The deck MUST show the background that a theme chooses with its `background` group (see theme, "Format 2 Values"),
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
