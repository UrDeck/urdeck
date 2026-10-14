## MODIFIED Requirements

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
