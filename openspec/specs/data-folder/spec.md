# data-folder Specification

## Purpose
Defines where the user's files live: a per-user data folder by default, or the application folder in portable mode.
This keeps the application folder free to be replaced by an installer or an updater without touching the user's page.
## Requirements
### Requirement: Data Folder Location
The host MUST keep the user's files in one data folder, chosen once at startup before anything is read or logged:

- **Portable mode:** when a file named `urdeck.portable` is in the application folder (the folder of
  `UrDeck.Host.exe`), the data folder is the application folder. The marker's content is ignored
- **Per-user mode:** otherwise the data folder is `UrDeck` in the user's roaming application data folder
  (`%APPDATA%\UrDeck`)
- The choice is made once per process. Adding or removing the marker takes effect at the next start
- Every command of the host uses the same data folder: the deck, `--snapshot`, `--quit` and `--demo`
- The first line the host writes to the log names the data folder and the mode, as
  `Data folder: <path> (per-user)` or `Data folder: <path> (portable: urdeck.portable next to the executable)`
- When the per-user data folder cannot be created or written, the host logs the reason to the system's debug output,
  shows no deck and exits with code 1

#### Scenario: Installed copy
- **WHEN** the host starts from a folder without `urdeck.portable`
- **THEN** its configuration, log, themes, backgrounds, user plugins and cache are in `%APPDATA%\UrDeck`

#### Scenario: Portable copy
- **WHEN** the host starts from a folder that holds `urdeck.portable`
- **THEN** its configuration, log, themes, backgrounds, plugins and cache are in that folder, and nothing is written
  to `%APPDATA%\UrDeck`

#### Scenario: Log names the folder
- **WHEN** the host starts in per-user mode
- **THEN** the first line of this run in `%APPDATA%\UrDeck\urdeck.log` is `Data folder: <that folder> (per-user)`

#### Scenario: Marker added while running
- **WHEN** `urdeck.portable` is created next to the executable while the host runs in per-user mode
- **THEN** the host keeps using `%APPDATA%\UrDeck` until it is started again

### Requirement: Data Folder Contents
The data folder MUST hold everything the user owns or the host writes while it runs, and the application folder MUST
hold only what an install or an update puts there:

| In the data folder | In the application folder |
|---|---|
| `urdeck-config.json` and its migration backups | `UrDeck.Host.exe`, the runtime and dependencies |
| `urdeck.log`, `urdeck.log.1` | `plugins/` with the first-party plugins |
| `themes/` (user themes) | `urdeck-config.default.json` (the sample page) |
| `backgrounds/` (user backgrounds) | `urdeck.ico` |
| `plugins/` (plugins the user adds) | `urdeck.portable` (portable copies only) |
| `cache/` (fetched site icons) | |

- In per-user mode the host never writes to the application folder
- The plugin shadow copies stay in the system temp folder (see host-shell, "Plugin Directory Setup")
- Uninstalling or replacing the application folder leaves the data folder as it was

#### Scenario: Update replaces the application folder
- **WHEN** the application folder of a per-user copy is deleted and replaced by a newer published folder
- **THEN** the next start shows the same page, theme, backgrounds and user plugins as before, and the log continues
  in the same file

#### Scenario: Application folder is read-only
- **WHEN** the application folder of a per-user copy cannot be written by the user
- **THEN** the host starts, runs, saves its configuration and logs without an error

### Requirement: First Run
When the data folder has no `urdeck-config.json`, the host MUST create a configuration the user can find and edit:

- The data folder is created if it does not exist
- When the application folder holds `urdeck-config.default.json`, it is copied to the data folder as
  `urdeck-config.json`; otherwise a default configuration with one empty page is written
- The log says that the configuration was created and where: `Configuration created at <path> from the sample page`
  or `... with an empty page`
- An existing `urdeck-config.json` is never replaced, whatever its content
- `--snapshot` and `--demo` do not create or write the configuration: with no file they draw the sample page from
  `urdeck-config.default.json` in memory, or an empty page when that file is missing

#### Scenario: First start of an installed copy
- **WHEN** a per-user copy starts and `%APPDATA%\UrDeck` does not exist
- **THEN** the folder exists afterwards, `urdeck-config.json` in it equals the shipped `urdeck-config.default.json`,
  the deck shows the sample page, and the log names the file

#### Scenario: Sample missing
- **WHEN** a copy starts with no configuration and no `urdeck-config.default.json`
- **THEN** a configuration with one empty page is written and the log says so

#### Scenario: Snapshot before the first run
- **WHEN** `--snapshot out.png` is run by a per-user copy and `%APPDATA%\UrDeck\urdeck-config.json` does not exist
- **THEN** the PNG shows the sample page and no `urdeck-config.json` exists afterwards

#### Scenario: Existing configuration kept
- **WHEN** the data folder already holds a `urdeck-config.json` with one page and the application folder's sample has
  several pages
- **THEN** the host loads the existing file and does not change it

### Requirement: Opening The Data Folder
The user MUST be able to reach the data folder without knowing where it is:

- The tray menu's "Open data folder" entry (see tray-icon, "Tray Menu") opens the data folder in File Explorer
- It opens the folder the running host resolved, in either mode
- When the folder cannot be opened, a warning is logged and the host keeps running

#### Scenario: Open from the tray
- **WHEN** the user chooses "Open data folder" in a per-user copy
- **THEN** File Explorer shows `%APPDATA%\UrDeck` with `urdeck-config.json` in it

