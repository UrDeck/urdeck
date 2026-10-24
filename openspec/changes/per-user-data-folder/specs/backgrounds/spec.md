## MODIFIED Requirements

### Requirement: Shader Backgrounds
A `shader` background MUST be an SkSL program selected by `name`:

- The product ships sixteen built-in shaders, available regardless of the user's folders: `plasma`, `aurora`,
  `gradient` and `julia`, and `horizon`, `spectrum`, `contour`, `nebula`, `streams`, `ripple`, `embers`, `plotter`,
  `shapes`, `warpgate`, `equalizer` and `vortex`
- A folder `backgrounds/<name>/` in the data folder (see data-folder) that holds a manifest and the entry shader is a
  user shader, selected by `<name>` exactly like a built-in one (see background-manifest, "Background Folder"). A user
  shader with the name of a built-in one takes precedence over it
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

### Requirement: Image Backgrounds
An `image` background MUST show a picture file of the user's:

- `path` is absolute or relative to the data folder (see data-folder); environment variables in it are expanded
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
- **WHEN** `path` is `wall.png` and that file is in the data folder, next to `urdeck-config.json`
- **THEN** that file is shown
