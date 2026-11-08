## MODIFIED Requirements

### Requirement: First Run
When the data folder has no `urdeck-config.json`, the host MUST create a configuration and greet the user in the editor:

- The data folder is created if it does not exist
- When the application folder holds `urdeck-config.default.json`, it is copied to the data folder as
  `urdeck-config.json`; otherwise a default configuration with one empty page is written
- The log says that the configuration was created and where: `Configuration created at <path> from the sample page`
  or `... with an empty page`
- After the deck has started, the editor opens with the welcome (see editor-states, "Welcome On First Run")
- An existing `urdeck-config.json` is never replaced, whatever its content
- `--snapshot` and `--demo` do not create or write the configuration and open no editor: with no file they draw the
  sample page from `urdeck-config.default.json` in memory, or an empty page when that file is missing

#### Scenario: First start of an installed copy
- **WHEN** a per-user copy starts and `%APPDATA%\UrDeck` does not exist
- **THEN** the folder exists afterwards, `urdeck-config.json` in it equals the shipped `urdeck-config.default.json`,
  the deck shows the sample page, the log names the file, and the editor opens with the welcome

#### Scenario: Sample missing
- **WHEN** a copy starts with no configuration and no `urdeck-config.default.json`
- **THEN** a configuration with one empty page is written and the log says so

#### Scenario: Snapshot before the first run
- **WHEN** `--snapshot out.png` is run by a per-user copy and `%APPDATA%\UrDeck\urdeck-config.json` does not exist
- **THEN** the PNG shows the sample page, no `urdeck-config.json` exists afterwards, and no window opens

#### Scenario: Existing configuration kept
- **WHEN** the data folder already holds a `urdeck-config.json` with one page and the application folder's sample has
  several pages
- **THEN** the host loads the existing file, does not change it, and does not open the editor
