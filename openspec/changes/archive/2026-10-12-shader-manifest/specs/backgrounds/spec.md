## MODIFIED Requirements

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
