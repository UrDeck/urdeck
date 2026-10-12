## Purpose

Defines the `urdeck-cli` command line tool: checking a background folder and rendering it to a picture without a deck,
the formats its findings are printed in, and the exit codes a script or a store's CI relies on.

## ADDED Requirements

### Requirement: A Portable Tool
`urdeck-cli` MUST be a command line tool separate from the host that runs wherever the product's runtime does:

- It MUST run on Windows and on Linux with the same commands, options, findings and exit codes
- It MUST need no window, no GPU and no network
- It MUST NOT read or write the deck's configuration, log or cache
- `--help` on the tool and on each command lists the options and the exit codes; `--version` prints the version
- Commands about backgrounds are under `shader`, so other subjects can be added later without renaming them

#### Scenario: Same result on both platforms
- **WHEN** the same background folder is checked on Windows and on Linux
- **THEN** the findings and the exit code are the same

#### Scenario: Help names the exit codes
- **WHEN** `urdeck-cli shader validate --help` is run
- **THEN** the output lists the options and what exit codes 0, 1 and 2 mean

### Requirement: Exit Codes
Every command MUST end with one of three exit codes, and their meaning MUST NOT change in later versions:

- `0`: the command did its job and found nothing that fails the run
- `1`: the command did its job and there are findings that fail the run: an `error`, or a `warning` when
  `--fail-on-warnings` is given. For `shader render`, also when no picture could be made from the background
- `2`: the command could not do its job: unknown or malformed arguments, a folder that does not exist or cannot be
  read, an output file that cannot be written, an unknown theme. The reason is printed to the error stream and
  nothing is printed to the output stream
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
- **WHEN** a folder whose shader does not compile is validated
- **THEN** the exit code is 1

#### Scenario: Folder does not exist
- **WHEN** `shader validate` is given a path that does not exist
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
- **WHEN** `--all` is given the directory that holds the product's four built-in backgrounds, with `--fail-on-warnings`
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
