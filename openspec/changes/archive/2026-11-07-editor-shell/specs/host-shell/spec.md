## MODIFIED Requirements

### Requirement: Configuration Management
The host MUST manage configuration via `urdeck-config.json`:

- Located in the data folder (see data-folder); created on first run as "First Run" there says
- Every change goes through the configuration document (see config-document), which writes the file half a second
  after the last change. The host does not write the file on exit unless a change is still pending
- Reloads the configuration when the file changes on disk (1s debounce), retrying when the file is briefly locked; a
  file event whose content equals what the host last wrote or read is its own write and is ignored
- On a parse error keeps running with the last good configuration and logs a warning
- Preserves widget-specific settings (extra JSON properties on a widget object) and every key it does not know across
  load and write
- `version` is the format version of the file, an integer. This change defines version 1, and the host writes
  `"version": 1` whenever it writes the file. A file without `version` is version 1. A value that is not a positive
  integer logs a warning and the file is read as version 1
- A file whose `version` is lower than the host's is migrated: each migration from one version to the next runs in
  order on the file's content before it is read. Before the host first writes a migrated file, it copies the original
  to `urdeck-config.v<old version>.json` in the same folder, replacing an earlier copy of that name. The log names
  each migration that ran. Version 1 has no migrations
- A file whose `version` is higher than the host's is read as well as possible, a warning says that it was written by
  a newer UrDeck, and the host never writes it while it runs (it does not remember a display), so settings the host
  does not know are not lost
- Each page has an `id`, unique in the file, that never changes when the page is renamed or moved; the host gives a
  page without one an id and writes it (see page-navigation)
- `startPage` is the `id` of the page shown on startup; absent or naming no page means the first page. It is not
  updated while the host runs, and a reload does not move the user to it (see page-navigation). The older `activePage`
  key is not read; it is kept in the file as an unknown key
- `pager.indicator` is the page indicator's mode: `always`, `fade`, `off` or `auto`; absent means `auto`
- `dock` is a list of widget objects shown in the dock (see dock, "Dock Configuration"); absent or empty means no
  dock. Widget-specific settings on its entries are preserved across load and write like those of a page's widgets
- `display` is the remembered display (see display-targeting): an object with `name` (for the reader, never used for
  matching), `model`, `path` and `selector` (the selector it was chosen with; absent when it was not chosen by a
  selector). The host writes it; it is absent from a default configuration until a display has been chosen. A user
  removes it to make the host choose again. Properties of it the host does not know are preserved across load and write
- `motion` is the user's motion level: `full`, `reduced` or `off` (see motion). Absent means "follow Windows' animation
  setting"; it is absent from a default configuration. Another value logs a warning and is treated as absent

#### Scenario: Config file does not exist
- **WHEN** the host starts with no `urdeck-config.json` in the data folder
- **THEN** the host creates it as data-folder, "First Run", says and runs

#### Scenario: Config edited while running
- **WHEN** the user changes a widget setting (e.g. `format` to `"12h"`) or `monitorName` in the file and saves
- **THEN** the host reloads, re-targets the monitor if needed, and rebuilds the page

#### Scenario: Config edited on another page
- **WHEN** the user is on the second page and saves an edit to a widget setting
- **THEN** the host reloads and rebuilds the page, and the second page is still shown

#### Scenario: Invalid config edit
- **WHEN** the file is saved with invalid JSON
- **THEN** the host logs a warning and keeps the previous configuration

#### Scenario: Indicator mode set
- **WHEN** the file has `"pager": { "indicator": "always" }`
- **THEN** the indicator is always visible in a reserved band

#### Scenario: Dock entries
- **WHEN** the file has a `dock` list with two shortcut objects
- **THEN** the dock shows the two shortcuts, and after a write both objects are in the file with their settings

#### Scenario: Dock entry in the older shape
- **WHEN** the file has a `dock` entry without a `typeId` (the unused `type`, `command`, `url` shape)
- **THEN** the file loads, the entry's slot is empty, a warning is logged, and the entry's properties are still in the
  file after a write

#### Scenario: Display remembered while running
- **WHEN** the host chooses a display for a configuration without `display`
- **THEN** the file gains the `display` object while the host runs, every other property of the file keeps its value,
  and the host does not reload the configuration because of its own write

#### Scenario: Older configuration
- **WHEN** a configuration written before this change, with `monitorName` and no `display`, is loaded
- **THEN** it loads without a warning and the deck is on the display the selector chooses

#### Scenario: Motion value
- **WHEN** the file has `"motion": "off"`
- **THEN** the effective motion level is `off` and the value is still in the file after a write

#### Scenario: Unknown motion value
- **WHEN** the file has `"motion": "slow"`
- **THEN** a warning is logged and the deck follows Windows' animation setting

#### Scenario: File without a version
- **WHEN** a configuration without `version` is loaded and the host writes it
- **THEN** it loads without a warning and the written file has `"version": 1`

#### Scenario: Migration from an older version
- **WHEN** a host whose current version is N loads a file with `"version": N-1` and writes it
- **THEN** the migration from N-1 to N ran, the log names it, `urdeck-config.v<N-1>.json` holds the original content,
  and the written file has `"version": N`

#### Scenario: File from a newer UrDeck
- **WHEN** the file has `"version": 2` and an unknown top-level key, and the host (version 1) runs and quits
- **THEN** a warning says the file was written by a newer UrDeck, the deck shows what the host can read, and the file
  is byte for byte unchanged after the host ends

#### Scenario: Quit writes nothing
- **WHEN** the host runs with a configuration that has comments and nothing changes before it quits
- **THEN** the file is byte for byte unchanged, comments included

#### Scenario: Start page by id
- **WHEN** the file has two pages with ids `p-main` and `p-home` and `"startPage": "p-home"`
- **THEN** the deck starts on the page `p-home`

#### Scenario: Pages without ids
- **WHEN** the file's pages have no `id`
- **THEN** the host gives each a unique id and the file has them after the host's next write
