## MODIFIED Requirements

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
