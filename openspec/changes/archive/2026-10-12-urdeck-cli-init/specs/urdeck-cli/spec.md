## ADDED Requirements

### Requirement: Creating A Manifest
`urdeck-cli shader init <path>` MUST turn a shader into a background folder with a manifest that loads:

- `<path>` is a `.sksl` file, or a folder that holds `<folder name>.sksl` and no `manifest.json`
- For a file `<name>.sksl` the folder `<name>` is created next to it, or inside the directory `--output` names, and
  the file is moved into it. For a folder nothing is moved
- The name (of the file without its extension, or of the folder) MUST follow the name rule of backgrounds; otherwise
  nothing is created and the exit code is 2
- The manifest written holds: the `$schema` line, `"manifest": 1`, a `title` made from the name, an empty
  `description`, and `parameters` with one entry for each parameter the shader declares, in the shader's order
- Each parameter entry holds a `label` made from the parameter's name and an empty `description`. It holds `kind`
  `color` when the parameter has three or four components and the shader suggests a colour: a `layout(color)`
  declaration, a default written as a colour, or a name that contains `color`, `colour` or `tint`
- The host's inputs and the background's own configuration keys are not parameters and get no entry. A uniform of a
  type that cannot be set gets no entry and is named in the output
- Nothing else is guessed: no `min`, `max`, `step`, `unit`, `author`, `license` or `samples`
- A title or label is the name with `_`, `-` and the steps of a camel-case name turned into spaces and the first
  letter in upper case (`bandWidth` becomes `Band width`)
- The folder is valid as written: `shader validate` on it reports no error that comes from the manifest, and reports
  as warnings what is left to fill in (the descriptions, the samples)
- `init` MUST NOT overwrite: an existing `manifest.json`, an existing folder that already holds the shader file, or a
  folder argument without its shader each leave everything as it was, with exit code 2 and the reason
- The shader is not compiled: a shader with an error still gets its manifest
- The output names the folder and the files written, and the command to run next

#### Scenario: A loose shader
- **WHEN** `shader init waves.sksl` is run where `waves.sksl` declares `uniform float density;`
- **THEN** the folder `waves` holds `waves.sksl` and a `manifest.json` with `"manifest": 1`, the title `Waves` and a
  parameter `density` labelled `Density`, the loose file is gone, and the exit code is 0

#### Scenario: Validate after init
- **WHEN** `shader validate waves` is run right after
- **THEN** there are no errors, and warnings for the missing description, the parameter's description and the samples

#### Scenario: A folder with only the shader
- **WHEN** `shader init waves` is run on a folder holding `waves.sksl` and no manifest
- **THEN** the manifest is written in that folder and nothing is moved

#### Scenario: A colour is recognised
- **WHEN** the shader declares `uniform float3 tint; // default: #ff8800`
- **THEN** the entry for `tint` has `"kind": "color"`

#### Scenario: A vector that is not a colour
- **WHEN** the shader declares `uniform float3 offset; // default: 0, 0, 1`
- **THEN** the entry for `offset` has no `kind`

#### Scenario: Host inputs are skipped
- **WHEN** the shader declares `iTime`, `iResolution` and `iColor0`
- **THEN** the manifest has no entry for any of them

#### Scenario: A manifest exists
- **WHEN** `shader init waves` is run on a folder that has a `manifest.json`
- **THEN** the file is unchanged, the error stream says a manifest exists, and the exit code is 2

#### Scenario: The folder exists
- **WHEN** `shader init waves.sksl` is run and a folder `waves` holding `waves.sksl` exists
- **THEN** nothing is moved or written and the exit code is 2

#### Scenario: A name that is not valid
- **WHEN** `shader init "my waves.sksl"` is run
- **THEN** nothing is created, the error stream names the rule for names, and the exit code is 2

#### Scenario: A shader that does not compile
- **WHEN** the shader has a syntax error
- **THEN** the folder and the manifest are written and the exit code is 0

## MODIFIED Requirements

### Requirement: Rendering A Background To A File
`urdeck-cli shader render <folder> -o <file>` MUST draw one frame of the background in the folder and write it as a PNG:

- `--size <width>x<height>` is the picture's size in pixels; the default is 270x480
- `--time <seconds>` is the shader's time; the default is the snapshot time, so the same input gives the same picture
  on one machine
- `--theme <name>` names a built-in theme whose colours the background uses; the default is the default theme. A name
  that is not a built-in theme is a failure of the tool (exit code 2)
- `--colors <c1,c2,...>` gives the background's colours as the configuration's `colors` does
- `--param <name>=<value>` sets a parameter as the configuration does, and may be repeated; a value the configuration
  would refuse is refused the same way
- The background rendered is the folder given. A name it shares with a built-in background makes no difference
- When the background cannot be built (the cases that fail it in the host), no file is written, the reason is printed
  and the exit code is 1
- A picture in which the shader drew nothing is written, and reported as a warning with its code; the exit code is 1
  only with `--fail-on-warnings`
- `render` reports findings about the picture only; the folder's other warnings are `validate`'s
- An existing output file is replaced
- Pictures rendered on different machines are not guaranteed to be identical

With `--update-manifest` the picture MUST also be recorded in the folder's manifest:

- The output file MUST be inside the background folder; otherwise it is a failure of the tool (exit code 2) and
  nothing is rendered
- The manifest's `samples` gets an entry with `type` `image` and `file` the picture's path relative to the folder,
  with `/` separators. When an entry already names that file it is updated in place and keeps its `caption`, `order`
  and any other key; otherwise the entry is added at the end
- The entry's `settings` is replaced by how this picture was made: `size`, `time`, `theme`, and `colors` and
  `parameters` when they were given
- Everything else in the manifest MUST be kept: every key, known or not, every value as written, and the order of keys
  and of array items. The file is written as indented JSON
- When rewriting would lose something the reader accepts and plain JSON cannot hold (a comment, a trailing comma) or
  the manifest has a key twice, the update is refused before anything is rendered or written: the exit code is 2, and
  the error stream says what would be lost and prints the `samples` entry to paste by hand
- A manifest that cannot be read at all (the failures of "Manifest Required") is not updated; the render fails as it
  does without the option
- When no picture is made the manifest is not changed
- A blank picture is recorded like any other

#### Scenario: Sample picture
- **WHEN** `shader render waves -o sample.png --size 600x400` is run on a valid folder
- **THEN** `sample.png` is a 600x400 PNG of that background and the exit code is 0

#### Scenario: A parameter
- **WHEN** `--param zoom=2` is given for a background that declares `zoom`
- **THEN** the picture is the one the deck would show with `"zoom": 2`

#### Scenario: Shader does not compile
- **WHEN** the folder's shader does not compile
- **THEN** the compiler's message is printed, no file is written and the exit code is 1

#### Scenario: Blank picture
- **WHEN** the shader leaves every pixel as the theme's background colour
- **THEN** the picture is written, a warning with code `SHADER070` is printed and the exit code is 0

#### Scenario: Blank picture fails on request
- **WHEN** the same is run with `--fail-on-warnings`
- **THEN** the exit code is 1

#### Scenario: Folder named like a built-in
- **WHEN** a path whose last part is `plasma` does not exist and is given to `shader render`
- **THEN** nothing is rendered and the exit code is 2

#### Scenario: Unknown theme
- **WHEN** `--theme nosuch` is given
- **THEN** the error stream names the theme and the exit code is 2

#### Scenario: First sample
- **WHEN** `shader render waves -o waves/sample.png --update-manifest` is run on a folder whose manifest has no samples
- **THEN** the picture is written, the manifest's `samples` has one entry with `file` `sample.png` and `settings`
  holding the size, time and theme, and `shader validate` no longer warns that there are no samples

#### Scenario: Sample rendered again
- **WHEN** the same file is rendered again with `--param zoom=2 --update-manifest` and its entry has a `caption`
- **THEN** there is still one entry for the file, its `caption` is unchanged and its `settings` has `zoom` 2 under
  `parameters`

#### Scenario: The rest of the manifest is kept
- **WHEN** a manifest with an unknown key and a description is updated
- **THEN** the unknown key and the description are in the file as before, in the same order

#### Scenario: A manifest with comments
- **WHEN** `--update-manifest` is used on a manifest that has a comment
- **THEN** no picture and no manifest is written, the error stream says the comment would be lost and prints the
  entry to paste, and the exit code is 2

#### Scenario: Output outside the folder
- **WHEN** `shader render waves -o ../sample.png --update-manifest` is run
- **THEN** nothing is rendered and the exit code is 2

#### Scenario: No picture, no update
- **WHEN** the shader does not compile and `--update-manifest` is given
- **THEN** the manifest is unchanged and the exit code is 1
