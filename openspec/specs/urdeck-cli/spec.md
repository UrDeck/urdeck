# urdeck-cli Specification

## Purpose
Defines the `urdeck-cli` command line tool: checking a background folder or a theme folder, rendering either to a picture and
starting a new one without a deck (`shader` and `theme` commands),
the formats its findings are printed in, and the exit codes a script or a store's CI relies on.
## Requirements
### Requirement: A Portable Tool
`urdeck-cli` MUST be a command line tool separate from the host that runs wherever the product's runtime does:

- It MUST run on Windows and on Linux with the same commands, options, findings and exit codes
- It MUST need no window, no GPU and no network
- It MUST NOT read or write the deck's configuration, log or cache
- `--help` on the tool and on each command lists the options and the exit codes; `--version` prints the version
- Commands about backgrounds are under `shader` and commands about themes are under `theme`, so other subjects can be
  added later without renaming them

#### Scenario: Same result on both platforms
- **WHEN** the same background folder or theme folder is checked on Windows and on Linux
- **THEN** the findings and the exit code are the same

#### Scenario: Help names the exit codes
- **WHEN** `urdeck-cli shader validate --help` or `urdeck-cli theme validate --help` is run
- **THEN** the output lists the options and what exit codes 0, 1 and 2 mean

### Requirement: Exit Codes
Every command MUST end with one of three exit codes, and their meaning MUST NOT change in later versions:

- `0`: the command did its job and found nothing that fails the run
- `1`: the command did its job and there are findings that fail the run: an `error`, or a `warning` when
  `--fail-on-warnings` is given. For `shader render` and `theme render`, also when no picture could be made
- `2`: the command could not do its job: unknown or malformed arguments, a folder that does not exist or cannot be
  read, an output file that cannot be written, an unknown theme or built-in theme. The reason is printed to the error
  stream and nothing is printed to the output stream
- An `info` finding never changes the exit code

#### Scenario: Clean folder
- **WHEN** a folder with no findings is validated
- **THEN** the exit code is 0

#### Scenario: Warnings only
- **WHEN** a folder with warnings and no errors is validated without `--fail-on-warnings`
- **THEN** the warnings are printed and the exit code is 0

#### Scenario: Warnings fail on request
- **WHEN** the same folder is validated with `--fail-on-warnings`
- **THEN** the exit code is 1

#### Scenario: An error
- **WHEN** a folder whose shader does not compile, or a theme folder with no `theme.json`, is validated
- **THEN** the exit code is 1

#### Scenario: Folder does not exist
- **WHEN** `shader validate` or `theme validate` is given a path that does not exist
- **THEN** the error stream names the path, the output stream is empty and the exit code is 2

#### Scenario: Unknown option
- **WHEN** a command is given an option it does not have
- **THEN** the exit code is 2

### Requirement: Validating A Background
`urdeck-cli shader validate <folder>` MUST check a background folder with the lint of "Background Lint" and print every
finding:

- The findings are exactly those the host logs when it loads the same folder, plus those only a tool can meet (a
  folder name that is not a valid name)
- The background's name is the name of the folder; a path such as `.` is resolved first
- It MUST NOT render the background, and MUST NOT validate the manifest against a JSON Schema
- The folder is only read, never changed
- A folder named like a built-in background is checked as the folder it is

#### Scenario: Same findings as the host
- **WHEN** a folder whose parameters lack descriptions is validated
- **THEN** the findings have the codes and messages the host's log has for that folder

#### Scenario: Missing manifest
- **WHEN** a folder holding only a `.sksl` file is validated
- **THEN** one error says the manifest is missing and the exit code is 1

#### Scenario: Current directory
- **WHEN** `shader validate .` is run inside a folder `waves`
- **THEN** the folder is checked as the background `waves`

#### Scenario: A path that leaves the folder, on any platform
- **WHEN** the manifest has `"entry": "..\\other\\x.sksl"` or a sample `file` of that form, on Windows or on Linux
- **THEN** an error says the path must stay inside the folder, and no file outside the folder is read

### Requirement: Validating Many Backgrounds
`urdeck-cli shader validate --all <directory>` MUST check every folder directly inside the directory as a background:

- The folders are checked in the order of their names, and each finding names its background
- The exit code is that of the worst background
- A directory with no folders in it is a failure of the tool (exit code 2), not a clean run
- `--all` and a folder argument MUST NOT be given together

#### Scenario: One bad among good
- **WHEN** a directory holds three clean folders and one whose shader does not compile
- **THEN** the output has the findings of the fourth under its name, and the exit code is 1

#### Scenario: The built-in backgrounds
- **WHEN** `--all` is given the directory that holds the product's built-in backgrounds, with `--fail-on-warnings`
- **THEN** there are no findings and the exit code is 0

#### Scenario: Empty directory
- **WHEN** `--all` is given a directory with no folders
- **THEN** the error stream says no backgrounds were found and the exit code is 2

### Requirement: Output Formats
`shader validate` MUST print its findings to the output stream in the format `--format` names; `text` is the default:

- `text`: one line for each finding, `<path of the file>: <severity> <code>: <message>`, then one summary line with the
  counts of errors, warnings and remarks. A run without findings prints the summary line only
- `markdown`: a heading for each background and a table of its findings (severity, code, file, message), or a line
  saying it is clean
- `json`: one JSON object with `version` (the integer 1), the tool's name and version, `backgrounds` (an array of
  objects with `name`, `folder` and `diagnostics`, each diagnostic with `code`, `severity`, `message` and `file`) and
  `summary` (the counts). `severity` is `error`, `warning` or `info`. A later version adds keys and MUST NOT rename or
  remove these
- `sarif`: a SARIF 2.1.0 log with one run: the tool as the driver, one rule for each code that occurs, one result for
  each finding with the level `error`, `warning` or `note` and the file as its location
- In every format a file is given relative to the working directory with `/` separators, on every platform
- A finding names a file and no line or column
- Output in `json` and `sarif` is the whole output stream: nothing else is printed there, so it can be piped
- The format never changes the exit code

#### Scenario: Text line
- **WHEN** a folder `waves` whose manifest has no description is validated
- **THEN** the output has the line `waves/manifest.json: warning SHADER041: the manifest has no description.` and a
  summary line

#### Scenario: JSON can be parsed
- **WHEN** a folder is validated with `--format json`
- **THEN** the output stream is one JSON object with `version` 1 and the folder's diagnostics

#### Scenario: SARIF
- **WHEN** a folder with one error and one warning is validated with `--format sarif`
- **THEN** the output is a SARIF 2.1.0 log with two results, levels `error` and `warning`, and a rule for each code

#### Scenario: Clean run in JSON
- **WHEN** a clean folder is validated with `--format json`
- **THEN** its `diagnostics` array is empty and the counts are 0

#### Scenario: Separators
- **WHEN** a finding is printed on Windows
- **THEN** its file path uses `/`

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

### Requirement: Validating A Theme
`urdeck-cli theme validate <folder>` MUST check a theme folder with the findings of the `theme-lint` capability and print
every finding:

- The theme's name is the name of the folder; a path such as `.` is resolved first
- The findings are exactly those the deck logs when it loads the same folder, plus those only the tool makes
- It MUST NOT draw the theme. The folder is only read, never changed
- `--background-dir <directory>` adds a directory of background folders to the backgrounds a pairing may name; it may be
  given more than once. The built-in backgrounds are always known
- A folder named like a built-in theme is checked as the folder it is

#### Scenario: Same findings as the deck
- **WHEN** a folder whose `card.shadow.blur` is out of range is validated
- **THEN** the finding has the code `THEME004` and the message the deck's log has for that folder

#### Scenario: Missing settings file
- **WHEN** a folder with no `theme.json` is validated
- **THEN** one error says the file is missing and the exit code is 1

#### Scenario: Current directory
- **WHEN** `theme validate .` is run inside a folder `mine`
- **THEN** the folder is checked as the theme `mine`

#### Scenario: A path that leaves the folder, on any platform
- **WHEN** a theme names a font file as `..\other\x.ttf`, on Windows or on Linux
- **THEN** an error says the path must stay inside the folder, and no file outside the folder is read

#### Scenario: A background of the user's
- **WHEN** a theme pairs with `mine` and `--background-dir` names a directory that holds a background `mine`
- **THEN** there is no finding about the pairing

### Requirement: Validating Many Themes
`urdeck-cli theme validate --all <directory>` MUST check every folder directly inside the directory as a theme, and
`theme validate --builtin` MUST check the built-in themes as the product ships them:

- The folders, or the built-in themes, are checked in the order of their names, and each finding names its theme
- The exit code is that of the worst theme
- A directory with no folders in it is a failure of the tool (exit code 2), not a clean run
- `--all` does not look inside the folders it finds; a folder that holds themes is not a theme
- `--all`, `--builtin` and a folder argument MUST NOT be given together
- `--builtin` reads the themes and their fonts from the product and not from the source tree, so it needs no files on
  disk

#### Scenario: One bad among good
- **WHEN** a directory holds three clean theme folders and one with an unknown key
- **THEN** the output has the findings of the fourth under its name, and the exit code is 0 without
  `--fail-on-warnings` and 1 with it

#### Scenario: The built-in themes
- **WHEN** `theme validate --builtin --fail-on-warnings` is run
- **THEN** there are no findings and the exit code is 0

#### Scenario: Empty directory
- **WHEN** `--all` is given a directory with no folders
- **THEN** the error stream says no themes were found and the exit code is 2

#### Scenario: Not recursive
- **WHEN** `--all` is given a directory whose folders hold no `theme.json` but whose subfolders do
- **THEN** each folder gets a `THEME001` error and the subfolders are not checked

#### Scenario: Options that exclude each other
- **WHEN** `--builtin` and `--all` are given together
- **THEN** the exit code is 2

### Requirement: Output Formats For Themes
`theme validate` MUST print its findings in the formats `shader validate` has, with the same rules for each:

- `text`: one line for each finding, `<path of the file>: <severity> <code>: <message>`, then one summary line with the
  counts. A finding that is about a key names it in the message. A built-in theme's file is printed as
  `builtin:<name>/theme.json`
- `markdown`, `json` and `sarif` as in "Output Formats", except that the JSON object has `themes` where the background
  report has `backgrounds`, each with `name`, `folder` and `diagnostics`, and a diagnostic of a key also has `key`
- The JSON object has `version` (the integer 1); a later version adds keys and MUST NOT rename or remove these
- The output of `shader validate` is not changed by the existence of `theme validate`
- In every format a file is given relative to the working directory with `/` separators, on every platform
- The format never changes the exit code

#### Scenario: Text line
- **WHEN** a folder `mine` whose `card.borderWith` is misspelt is validated
- **THEN** the output has a line `mine/theme.json: warning THEME002: ...` that names `card.borderWidth`, and a summary line

#### Scenario: JSON can be parsed
- **WHEN** a folder is validated with `--format json`
- **THEN** the output stream is one JSON object with `version` 1, `themes` and the folder's diagnostics

#### Scenario: SARIF
- **WHEN** a folder with one error and one warning is validated with `--format sarif`
- **THEN** the output is a SARIF 2.1.0 log with two results, levels `error` and `warning`, and a rule for each code

#### Scenario: Backgrounds unchanged
- **WHEN** a background folder is validated with `shader validate --format json`
- **THEN** the output is as it was before `theme` existed

### Requirement: Rendering A Theme To A File
`urdeck-cli theme render <folder> -o <file>` MUST draw the sample page in the theme and write it as a PNG:

- `--builtin <name>` in place of the folder draws a built-in theme; the folder and `--builtin` MUST NOT be given together
- The sample page is the same one the tool's and the product's picture tests draw (see `preview-rendering`, "The Sample
  Page"): a clock, two stats cards, a weather card and four shortcuts, with the providers' sample readings and a fixed time
- It is drawn by the preview renderer without a background, as the picture tests draw it
- `--page <page|dock>` chooses the page or the dock; the default is `page`
- `--size <width>x<height>` is the picture's size in pixels; the default is 1100x1920
- The same input gives the same picture on one machine; pictures made on different machines are not guaranteed to be
  identical
- When the theme cannot be read (`THEME001`), no file is written, the reason is printed and the exit code is 1
- The theme's findings are printed to the error stream as warnings; they do not stop the picture, and only
  `--fail-on-warnings` makes them change the exit code
- An existing output file is replaced

#### Scenario: A picture
- **WHEN** `theme render mine -o out.png` is run on a valid folder
- **THEN** `out.png` is a 1100x1920 PNG of the sample page in that theme and the exit code is 0

#### Scenario: Same pixels as the tests
- **WHEN** a built-in theme is rendered with the size and page the picture tests use
- **THEN** the picture has the pixels of that test's reference picture, where the platform draws text the same way

#### Scenario: The dock
- **WHEN** `--page dock` is given
- **THEN** the picture shows the dock page

#### Scenario: Unreadable theme
- **WHEN** the folder has no `theme.json`
- **THEN** no file is written and the exit code is 1

#### Scenario: Warnings do not stop the picture
- **WHEN** a theme with one out-of-range value is rendered
- **THEN** the picture is written, the warning is on the error stream and the exit code is 0

### Requirement: Creating A Theme
`urdeck-cli theme init <name>` MUST make a theme folder that loads and checks clean:

- The folder `<name>` is made in the current directory, or in the directory `--output` names, which must exist
- The name is a single folder name of letters, digits, `-` and `_`; anything else is a failure of the tool (exit code 2)
  and nothing is created
- Without `--from`, the folder holds a `theme.json` with the `$schema` line, a `meta` group with the
  name, an empty description for the author to fill in and a `mode` of `dark`, and a `colors` group holding the default
  dark theme's colours
- With `--from <built-in name>`, the folder holds that theme's settings as shipped, with a `meta.name` of the new name,
  and the font files and licence files it names. An unknown built-in name is a failure of the tool (exit code 2)
- `init` MUST NOT overwrite: an existing folder of that name is left as it is, with exit code 2 and the reason
- The output names the folder, the files written and the command to run next

#### Scenario: A starter
- **WHEN** `theme init mine` is run
- **THEN** the folder `mine` holds a `theme.json` that `theme validate mine` reports no error for, and the exit code is 0

#### Scenario: From a built-in theme
- **WHEN** `theme init mine --from carbon` is run
- **THEN** the folder holds Carbon's settings, its fonts and their licences, and `theme validate mine` reports no finding
  that Carbon itself has not

#### Scenario: The folder exists
- **WHEN** `theme init mine` is run and a folder `mine` exists
- **THEN** nothing is written and the exit code is 2

#### Scenario: A name that is not valid
- **WHEN** `theme init "my theme"` is run
- **THEN** nothing is created, the error stream names the rule for names, and the exit code is 2

### Requirement: Printing The Theme Schema
`urdeck-cli theme schema` MUST print the JSON Schema of the theme format that the tool was built with:

- The schema goes to the output stream, or to the file `--output` names, which is replaced when it exists
- It is the schema the product publishes as `docs/schemas/v1/theme.json`, byte for byte
- It needs no network

#### Scenario: Printed
- **WHEN** `theme schema` is run
- **THEN** the output stream is the published schema and the exit code is 0

#### Scenario: To a file
- **WHEN** `theme schema --output theme.schema.json` is run
- **THEN** the file holds the schema and nothing is printed to the output stream

### Requirement: Rendering A Preview
`urdeck-cli preview render -o <file-or-folder>` MUST draw a preview (see `preview-rendering`) and write it as PNG:

- What to draw: `--config <file>` (a `urdeck-config.json`; the sample page when absent) with `--page <N|dock>` (default:
  the configuration's active page), or `--widget <typeId> --widget-size <W>x<H>` for one widget alone (with `--config`, the
  widget's configuration is the first widget of that type in the file, otherwise its default configuration)
- `--theme <name>` (a built-in theme), or `--theme-dir <folder>` (a theme folder); default: the configuration's theme
- `--background <name|none|theme>`: a background by name (built-in, or in a folder given with `--background-dir`), none, or
  the theme's choice; default: the configuration's background
- `--size <W>x<H>` for a page (default 1100x1920); `--width <px>` for a widget alone, the width of the four-cell panel
  (default 1100)
- `--time <local date and time>`: the moment drawn (default: the sample page's moment)
- `--frames N` with `--fps F` (default 30): the output is a folder and N pictures `frame-0000.png` onwards are written to it
- `--motion <full|reduced|off>` (default `full`)
- Readings come from the providers' samples only, and the command makes no network request
- Exit codes are the tool's (`0` written, `1` an input that cannot be used, with the reason, and nothing written, `2` a
  usage error); the theme's findings go to the error stream as warnings, as for `theme render`
- An existing output file, or existing frames in the output folder, are replaced

#### Scenario: A page in a theme
- **WHEN** `preview render --theme ember --background theme -o out.png` is run
- **THEN** `out.png` is the sample page in Ember over Ember's own background, 1100x1920, and the exit code is 0

#### Scenario: A widget alone
- **WHEN** `preview render --widget urdeck.widgets.weather --widget-size 4x4 --theme paper -o w.png` is run
- **THEN** `w.png` is 1100x1100 and shows a weather card in Paper with the sample's forecast

#### Scenario: Frames
- **WHEN** `preview render --theme rigline --background theme --frames 90 -o frames` is run
- **THEN** `frames` holds `frame-0000.png` to `frame-0089.png`, in which the border and the background move

#### Scenario: An unknown widget
- **WHEN** `--widget example.unknown` is given
- **THEN** nothing is written, the reason lists the widget types the tool knows, and the exit code is 1

### Requirement: Exporting The Catalog
`urdeck-cli catalog export [-o <file>]` MUST write, as one JSON document (to the file, or to standard output), what the
product knows about what it ships:

- `version`: `1`
- `themes`: each theme's name, display name, description, author, mode, tags, swatch colours, default background and the
  backgrounds it pairs with
- `backgrounds`: each background's name, title, description, author, tags, parameters and the themes it pairs with
- `pairings`: for each theme, the backgrounds listed for it, each marked when the pairing is named on both sides
- `widgets`: each first-party widget's type id, name, description, category, declared sizes with their preview flag, its
  default configuration as it would appear in `urdeck-config.json`, and its settings: each setting's name in
  `urdeck-config.json`, its type (text, number, whole number, on/off, list, object, or the values it accepts) and its
  default for a widget written without it
- `providers`: each first-party provider's id, name, whether it carries a sample, and its settings beyond the base ones:
  each setting's name in `urdeck-config.json`, its type, label, help text, default, and whether it is required or a
  secret (a secret's environment variable name is listed; no value ever is)
- `findings`: every theme and background finding code with its severity and a one-line summary of what it means

Built-in themes and backgrounds are always listed; `--theme-dir` and `--background-dir` add a user's folders, as for
`theme validate`. Lists are sorted by name and the same input gives the same bytes, so the output can be compared
between runs.

#### Scenario: What the site reads
- **WHEN** `catalog export -o catalog.json` is run
- **THEN** `catalog.json` lists the eleven built-in themes, the sixteen built-in backgrounds, the four first-party widgets
  with their sizes, default configurations and settings, the first-party providers with their settings and every finding
  code

#### Scenario: A widget's settings
- **WHEN** the catalog is exported
- **THEN** the clock's settings include `format` with the values it accepts and its default, and `showDate` as on/off

#### Scenario: Stable output
- **WHEN** `catalog export` is run twice with the same inputs
- **THEN** both outputs are byte for byte the same

#### Scenario: A provider's secret in the export
- **WHEN** the catalog is exported and a first-party provider declares a secret `token`
- **THEN** the provider's settings list `token` as a secret with its environment variable name, and no value

### Requirement: Browsing Readings
`urdeck-cli readings browse <provider> [--search <text>] [--domain <prefix>] [--url <address>] [--config <file>]
[--format text|json]` MUST list the concrete readings a provider can enumerate (see data-providers, "Listing Concrete
Readings"), one per line: the reading id, its kind, its current value with its unit, and its label.

- The provider's settings come from `--config` (default: the configuration in the user's data folder), with `--url`
  overriding an address; secrets from the environment and, on Windows, Credential Manager (see provider-secrets). A
  secret is used, never printed
- `--search` keeps readings whose id or label contains the text (case-insensitive); `--domain` keeps ids that start with
  the prefix (`light` keeps `light.*`)
- Values are read by subscribing for at most 5 seconds; a reading with none by then shows its state (pending)
- A provider that cannot list, is not loaded or cannot start ends with exit code 2 and the reason

#### Scenario: Find the basement temperature
- **WHEN** `readings browse homeassistant --search basement --domain sensor` runs with the token in
  `URDECK_SECRET_HOMEASSISTANT_TOKEN`
- **THEN** the output lists `homeassistant:sensor.basement_temperature` with its kind, value and label, and no token

#### Scenario: Provider without a listing
- **WHEN** `readings browse weather` runs
- **THEN** the exit code is 2 and the message says the provider cannot list its readings

