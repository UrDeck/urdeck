## MODIFIED Requirements

### Requirement: Configuration Management
The host MUST manage configuration via `urdeck-config.json`:

- Located next to the executable; created with defaults if it does not exist
- Saves configuration on exit, and when it remembers a display or updates the remembered one (see below)
- Hot-reloads configuration on file change (1s debounce), retrying when the file is briefly locked and ignoring its own saves
- On a parse error keeps running with the last good configuration and logs a warning
- Preserves widget-specific settings (extra JSON properties on a widget object) across load/save
- `activePage` is the index of the page shown on startup. It is not updated while the host runs, and a reload does not
  move the user to it (see page-navigation)
- `pager.indicator` is the page indicator's mode: `always`, `fade`, `off` or `auto`; absent means `auto`
- `dock` is a list of widget objects shown in the dock (see dock, "Dock Configuration"); absent or empty means no
  dock. Widget-specific settings on its entries are preserved across load/save like those of a page's widgets
- `display` is the remembered display (see display-targeting): an object with `name` (for the reader, never used for
  matching), `model`, `path` and `selector` (the selector it was chosen with; absent when it was not chosen by a
  selector). The host writes it; it is absent from a default configuration until a display has been chosen. A user
  removes it to make the host choose again. Properties of it the host does not know are preserved across load and save
- `motion` is the user's motion level: `full`, `reduced` or `off` (see motion). Absent means "follow Windows' animation
  setting"; it is absent from a default configuration. Another value logs a warning and is treated as absent

#### Scenario: Config file does not exist
- **WHEN** the host starts with no `urdeck-config.json`
- **THEN** the host creates a default config with one empty page and runs

#### Scenario: Config edited while running
- **WHEN** the user changes a widget setting (e.g. `format` to `"12h"`) or `monitorName` and saves
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
- **THEN** the dock shows the two shortcuts, and after a save both objects are in the file with their settings

#### Scenario: Dock entry in the older shape
- **WHEN** the file has a `dock` entry without a `typeId` (the unused `type`, `command`, `url` shape)
- **THEN** the file loads, the entry's slot is empty, a warning is logged, and the entry's properties are still in the
  file after a save

#### Scenario: Display remembered while running
- **WHEN** the host chooses a display for a configuration without `display`
- **THEN** the file gains the `display` object while the host runs, every other property of the file is unchanged, and
  the host does not reload the configuration because of its own save

#### Scenario: Older configuration
- **WHEN** a configuration written before this change, with `monitorName` and no `display`, is loaded
- **THEN** it loads without a warning and the deck is on the display the selector chooses

#### Scenario: Motion value
- **WHEN** the file has `"motion": "off"`
- **THEN** the effective motion level is `off` and the value is still in the file after a save

#### Scenario: Unknown motion value
- **WHEN** the file has `"motion": "slow"`
- **THEN** a warning is logged and the deck follows Windows' animation setting
