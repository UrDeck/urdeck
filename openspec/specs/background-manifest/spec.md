# Background Manifest Specification

## Purpose

Defines a shader background as an installable folder with a manifest: its layout, the version 1 manifest, the rule that
a manifest is required, the lint that checks a background and its diagnostic codes, and rendering a background to a
picture. It is the format a person, a command line tool and a settings editor all read.
## Requirements
### Requirement: Background Folder
A shader background MUST be a folder `backgrounds/<name>/` next to the executable, or a built-in one, holding a
`manifest.json` and the entry shader file:

- The folder name is the background's name in the configuration. It follows the name rule of "Shader Backgrounds"
- The entry is `<name>.sksl` in the folder unless the manifest's `entry` names another file. `entry` MUST name a file
  inside the folder: an absolute path or one that leaves the folder is refused
- Other files in the folder (a readme, sample pictures) are never read by the host
- Removing the folder removes the background; copying a folder in adds one
- A loose `backgrounds/<name>.sksl` is not a background. When one exists and the configuration selects its name, the
  warning says that a background is now a folder with a `manifest.json` and names the folder to create
- A user folder with the name of a built-in one takes precedence over it
- Folders are re-read when the configuration is reloaded

#### Scenario: Background installed by copying a folder
- **WHEN** a folder `waves` holding `manifest.json` and `waves.sksl` is copied into `backgrounds/` and the
  configuration selects `waves`
- **THEN** the deck shows that shader

#### Scenario: Entry named in the manifest
- **WHEN** the manifest has `"entry": "main.sksl"` and the folder holds `main.sksl`
- **THEN** that file is the shader

#### Scenario: Entry leaves the folder
- **WHEN** the manifest has `"entry": "../other/x.sksl"`
- **THEN** no file is read, a warning is logged and the deck shows the theme's background colour

#### Scenario: Loose file
- **WHEN** `backgrounds/waves.sksl` exists, there is no folder `backgrounds/waves`, and the configuration selects `waves`
- **THEN** the warning names the folder layout, and the deck shows the theme's background colour

#### Scenario: Built-in replaced
- **WHEN** a valid folder `backgrounds/plasma` exists and the configuration selects `plasma`
- **THEN** the user's folder is used

### Requirement: Manifest Required
A background MUST have a readable `manifest.json` of a supported version, and fails to load without one:

- `manifest` is an integer, the version of the format; this version is `1`. A missing, non-integer or unsupported
  value fails the background, and the warning names the value
- A file that is not valid JSON, or whose top level is not an object, fails the background
- A failed background leaves the deck as "Background Failures" describes: one warning naming the background and the
  cause, the theme's background colour shown, widgets unaffected
- Nothing else in the manifest fails the background. A problem in any other field is a lint diagnostic and the
  background is shown, using what could be read
- The four built-in backgrounds have manifests and are loaded as any other

#### Scenario: Manifest missing
- **WHEN** the folder `backgrounds/waves` holds `waves.sksl` and no `manifest.json`
- **THEN** a warning says the manifest is missing, and the deck shows the theme's background colour

#### Scenario: Not JSON
- **WHEN** `manifest.json` has a syntax error
- **THEN** a warning gives the position of the error and the background does not load

#### Scenario: Unsupported version
- **WHEN** the manifest has `"manifest": 2`
- **THEN** a warning says this version of the product reads version 1, and the background does not load

#### Scenario: A problem that is not the version
- **WHEN** the manifest has no `description` and a parameter with an unknown `kind`
- **THEN** the background is shown, and the log has one lint warning for each problem

### Requirement: Manifest Content
The version 1 manifest MAY hold the following; every key but `manifest` is optional:

- `$schema`: a string for editors; ignored
- `title` and `description`: text for a person
- `entry`: see "Background Folder"
- `author`, `license`, `version` and `tags` (an array of strings): text for a person and for a store; never used to
  decide what runs
- `parameters`: an object keyed by the name of a uniform the shader declares, each value an object with `label`,
  `description`, `kind`, `min`, `max`, `step`, `unit` and `hidden`
- `samples`: an array of objects with `type` (`image`, or `video`, which is accepted and not used), `file`, `order`,
  `caption` and `settings` (an object)

The meaning of the parameter keys:

- `kind` is `number` (the default) or `color`; other values are reserved for later versions (a `vector` is the likely
  first) and are a lint warning until then. `color` MUST be given only for a parameter with three or four
  components; it tells an editor to offer a colour, and does not change how the configuration sets the parameter
- `min`, `max` and `step` are numbers; `min` and `max` apply to each component of a vector
- `hidden` true keeps the parameter out of an editor. It MUST NOT stop the configuration from setting it
- The shader is the source of truth for which parameters exist, their types and their defaults. The manifest MUST
  NOT change them: a default in the shader's comment stays the default, whatever the manifest says
- A sample's `file` MUST name a file inside the folder, with a relative path that does not leave it
- A key the format does not define, a `kind` or sample `type` that is not defined, and a value of the wrong JSON type
  each log one lint warning and are otherwise ignored

#### Scenario: Annotation
- **WHEN** the manifest gives `parameters.intensity` a `label`, a `description`, `min` 0 and `max` 3
- **THEN** those are what a reader of the manifest gets for `intensity`, and the parameter's type and default are
  still the shader's

#### Scenario: Hidden parameter
- **WHEN** the manifest marks `seed` as `hidden` and the configuration has `"seed": 7`
- **THEN** the shader runs with `seed` at 7

#### Scenario: Parameter the shader does not have
- **WHEN** the manifest annotates `densty` and the shader declares no uniform of that name
- **THEN** a lint diagnostic names `densty` and the background is shown

#### Scenario: Colour kind on a number
- **WHEN** the manifest gives `kind` `color` to a `float` parameter
- **THEN** a lint diagnostic says a colour needs three or four components, and the kind is ignored

#### Scenario: Unknown key
- **WHEN** the manifest has a key `"rating": 5`
- **THEN** a lint warning names it and the background is shown

#### Scenario: Reserved sample type
- **WHEN** a sample has `"type": "video"`
- **THEN** it is accepted, and the host never plays it

### Requirement: Host Keys Are Not Parameters
The names of the background's own configuration keys (`type`, `name`, `path`, `speed`, `scale`, `colors`) and of the host
inputs (`iTime`, `iResolution`, `iColor0` to `iColor3`) MUST NOT be parameters:

- A shader uniform of one of those names is not a parameter, and a manifest annotation of one is a lint error
- The host keys are described by the product, not by any manifest

#### Scenario: Annotation of a host key
- **WHEN** a manifest has `parameters.speed`
- **THEN** a lint error says that `speed` belongs to the product

### Requirement: Parameter Ranges
A parameter that the manifest gives a `min` or a `max` MUST be kept in range when the configuration sets it:

- A configured value outside the range logs one warning naming the parameter and the range, and the parameter keeps its
  default, as `speed` and `scale` do
- A vector is checked per component
- A parameter without `min` and `max` is not checked
- A default (in the shader) outside the range is a lint warning

#### Scenario: Out of range
- **WHEN** the manifest gives `intensity` `max` 3 and the configuration has `"intensity": 9`
- **THEN** a warning is logged and `intensity` keeps its default

#### Scenario: In range
- **WHEN** the configuration has `"intensity": 2`
- **THEN** the shader runs with `intensity` at 2

#### Scenario: Default out of range
- **WHEN** the shader's default for `intensity` is 5 and the manifest's `max` is 3
- **THEN** a lint warning says so

### Requirement: Background Lint
A background folder MUST be checkable without showing it, and the host MUST report the same findings when it loads one:

- A check returns diagnostics, each with a stable code `SHADER` and three digits, a severity (`error`, `warning` or
  `info`), a message and the file it concerns. A code keeps its meaning in later versions
- The check covers at least: the folder's name; the manifest's version, JSON and keys; the entry's existence and path;
  whether the shader compiles; a `uniform shader`; uniforms of a type that cannot be set; a parameter annotated but not
  declared, and one declared but not annotated (unless hidden); a missing `title`, `description` or `samples`; a
  parameter without a description; a default outside its range; a sample file that is missing or leaves the folder; a
  host key used as a parameter; a `kind` that does not fit
- A folder whose name does not follow the name rule of "Shader Backgrounds" is an `error` (`SHADER012`): the
  configuration could never select it. The rest of the folder is still checked
- A path in the manifest (the `entry`, a sample's `file`) that is rooted or leaves the folder is refused on every
  platform, whichever of `/` and `\` it is written with
- A diagnostic is an `error` when the background cannot work or the manifest breaks a rule of this specification
  (the cases of "Manifest Required" and "Host Keys Are Not Parameters", a shader that does not compile, a missing
  entry, a sample file that is missing, a folder name that is not a valid name). It is a `warning` when the background
  works but could be better described, and `info` for a remark
- When the host loads a background it logs each diagnostic once per load as one line with its code and the folder.
  An `error` stops the background only where this specification says the background fails; every other diagnostic,
  errors included, leaves it shown
- The four built-in backgrounds MUST produce no diagnostic of any severity. A test enforces it

#### Scenario: Clean background
- **WHEN** a folder has a manifest with a title, a description, every parameter described and a sample
- **THEN** the check returns no diagnostics

#### Scenario: Diagnostic at load
- **WHEN** a background whose parameters lack descriptions is loaded
- **THEN** the log has a warning with a `SHADER` code and the folder for each, and the background is shown

#### Scenario: Declared and not annotated
- **WHEN** the shader declares `uniform float density;` and the manifest does not mention `density`
- **THEN** a warning names `density`

#### Scenario: Hidden is not reported
- **WHEN** a declared parameter is marked `hidden` and has no description
- **THEN** no diagnostic is reported for the missing description

#### Scenario: Built-ins
- **WHEN** the check runs on `plasma`, `aurora`, `gradient` and `julia`
- **THEN** each returns no diagnostics

#### Scenario: Folder name
- **WHEN** a folder named `my waves` with an otherwise clean background is checked
- **THEN** an error with code `SHADER012` names the folder and says which characters a name may have

#### Scenario: Backslash traversal on Linux
- **WHEN** a manifest with `"entry": "..\\other\\x.sksl"` is checked on Linux
- **THEN** an error says the entry must stay inside the folder, as on Windows

### Requirement: Rendering A Background
A background MUST be renderable to a picture without a window or a GPU, so tools can make samples:

- The caller gives the background (with its configuration keys), a theme, a size and a time; the result is a bitmap
  of that size with the theme's background colour behind the background
- The background can be given as a folder that was read from any path, not only by a name looked up in the product's
  `backgrounds` directory; a folder given this way is never replaced by a built-in of the same name
- The default time is the snapshot time, so the same input gives the same picture on one machine
- The background's `scale` has no effect on the picture: the size given is the size rendered
- A background that cannot be built returns no picture and the reason, and does not throw for the cases of
  "Background Failures"
- A picture in which every pixel is still the theme's background colour is returned with a `warning` diagnostic
  (`SHADER070`) saying the shader drew nothing at that time. It is a warning because a shader can be blank at one
  instant. The host never reports it: it does not render samples

#### Scenario: Sample picture
- **WHEN** a background is rendered at 600x400 at the snapshot time
- **THEN** the result is a 600x400 picture of that background in the theme's palette

#### Scenario: Failing background
- **WHEN** a background whose shader does not compile is rendered
- **THEN** no picture is returned and the reason holds the compiler's message

#### Scenario: Folder from another path
- **WHEN** a folder `plasma` outside the product's `backgrounds` directory is read and rendered
- **THEN** the picture is of that folder's shader, not of the built-in `plasma`

#### Scenario: Shader draws nothing
- **WHEN** a shader that returns a fully transparent colour for every pixel is rendered
- **THEN** the picture is returned with a `SHADER070` warning

#### Scenario: Shader draws something
- **WHEN** a built-in background is rendered
- **THEN** there is no `SHADER070` diagnostic

