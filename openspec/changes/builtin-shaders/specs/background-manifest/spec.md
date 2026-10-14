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
- Every built-in background MUST produce no diagnostic of any severity. A test enforces it for each of the sixteen

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
- **WHEN** the check runs on each of the sixteen built-in backgrounds
- **THEN** each returns no diagnostics

#### Scenario: Folder name
- **WHEN** a folder named `my waves` with an otherwise clean background is checked
- **THEN** an error with code `SHADER012` names the folder and says which characters a name may have

#### Scenario: Backslash traversal on Linux
- **WHEN** a manifest with `"entry": "..\\other\\x.sksl"` is checked on Linux
- **THEN** an error says the entry must stay inside the folder, as on Windows

## ADDED Requirements

### Requirement: Built-In Pairing Data
The shipped pairing data MUST agree between themes and backgrounds: for every built-in background and every shipped or
drafted theme, if the background's `pairsWith.themes` names the theme then the theme's `meta.pairsWith.backgrounds`
names the background, and the other way round, so that every listed pair of shipped data is mutual. A name of a theme
or a background that does not exist in the product is a failure of the check.

- Every built-in background of the twelve new ones names at least two themes, and every drafted theme names between
  two and seven backgrounds
- The check reads the drafted themes' files until they are built-in themes, and the built-in themes after
- User themes and user backgrounds are not held to this

#### Scenario: Symmetric data
- **WHEN** the check reads the twelve new manifests and the eight drafted themes
- **THEN** every listed pair is named by both sides

#### Scenario: One side missing
- **WHEN** a manifest names `carbon` and `carbon` does not list that background
- **THEN** the check fails and names the pair

#### Scenario: A name that does not exist
- **WHEN** a manifest names the theme `carbonn`
- **THEN** the check fails and names `carbonn`
