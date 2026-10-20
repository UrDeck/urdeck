## MODIFIED Requirements

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

## ADDED Requirements

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
- **WHEN** a folder `mine` whose `card.borderwidth` is misspelt is validated
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
- The sample page is the same one the tool's and the product's picture tests draw: a clock, two stats cards, a weather
  card and four shortcuts, with fixed readings and a fixed time
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
- Without `--from`, the folder holds a `theme.json` with the `$schema` line, `"format": 2`, a `meta` group with the
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
- It is the schema the product publishes as `docs/schemas/v2/theme.json`, byte for byte
- It needs no network

#### Scenario: Printed
- **WHEN** `theme schema` is run
- **THEN** the output stream is the published schema and the exit code is 0

#### Scenario: To a file
- **WHEN** `theme schema --output theme.schema.json` is run
- **THEN** the file holds the schema and nothing is printed to the output stream
