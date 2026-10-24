## MODIFIED Requirements

### Requirement: Plugin Directory Setup
The host MUST discover plugins in two folders: `plugins/` in the application folder (the first-party plugins, which an
install or an update puts there) and `plugins/` in the data folder (plugins the user adds; see data-folder). In
portable mode both are the same folder and it is scanned once:

- Creates the data folder's `plugins/` if missing and scans both folders for `.dll` files on startup, the application
  folder's first, each in file name order
- When both folders register the same widget type id, or both claim the same provider id, the application folder's
  plugin is kept and a warning names the other file. Within one folder the earlier file in name order is kept, with
  the same warning
- Loads each assembly into its own collectible `AssemblyLoadContext` from a shadow copy under the system temp folder (`urdeck-shadow/<pid>/`), so the original file is never locked; `UrDeck.Sdk`, SkiaSharp and the framework resolve from the default context
- Uses `FileSystemWatcher` on both folders to detect changes and hot-reloads: unloads old contexts, re-registers widgets, and rebuilds the page (`PluginsChanged`)
- Debounces file change events by 500ms
- Logs warnings for assembly load failures and continues loading remaining plugins
- Rejects widget types that fail descriptor validation (see widget-sdk) with a logged reason
- Removes shadow directories left by dead processes at startup

#### Scenario: Plugin loads successfully
- **WHEN** a valid plugin DLL with a widget implementation is in the data folder's `plugins/`
- **THEN** the widget is registered in the registry

#### Scenario: First-party plugin
- **WHEN** a per-user copy starts with an empty data folder
- **THEN** the Clock, Stats, Weather and Shortcut widgets and the system and weather providers are registered from the
  application folder's `plugins/`

#### Scenario: User copy of a first-party plugin
- **WHEN** the data folder's `plugins/` holds a DLL that registers `urdeck.widgets.clock`
- **THEN** the application folder's Clock is used and a warning names the user's file

#### Scenario: Plugin fails to load
- **WHEN** a DLL in `plugins/` is invalid or missing dependencies
- **THEN** the host logs a warning to `urdeck.log` and continues

#### Scenario: Plugin replaced while running
- **WHEN** a plugin DLL in either `plugins/` folder is overwritten or deleted while the host runs
- **THEN** the original file is not locked, the plugin is reloaded (or removed) after the debounce, and the page is rebuilt

#### Scenario: Unknown widget type in config
- **WHEN** the config references a `typeId` with no registered widget
- **THEN** its cell stays empty and a warning listing the registered ids is logged

### Requirement: Configuration Management
The host MUST manage configuration via `urdeck-config.json`:

- Located in the data folder (see data-folder); created on first run as "First Run" there says
- Saves configuration on exit, and when it remembers a display or updates the remembered one (see below)
- Hot-reloads configuration on file change (1s debounce), retrying when the file is briefly locked and ignoring its own saves
- On a parse error keeps running with the last good configuration and logs a warning
- Preserves widget-specific settings (extra JSON properties on a widget object) across load/save
- `version` is the format version of the file, an integer. This change defines version 1, and the host writes
  `"version": 1` whenever it saves. A file without `version` is version 1. A value that is not a positive integer logs
  a warning and the file is read as version 1
- A file whose `version` is lower than the host's is migrated: each migration from one version to the next runs in
  order on the file's content before it is read. Before the host first writes a migrated file, it copies the original
  to `urdeck-config.v<old version>.json` in the same folder, replacing an earlier copy of that name. The log names
  each migration that ran. Version 1 has no migrations
- A file whose `version` is higher than the host's is read as well as possible, a warning says that it was written by
  a newer UrDeck, and the host never writes it while it runs (it does not remember a display and does not save on
  exit), so settings the host does not know are not lost
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
- **WHEN** the host starts with no `urdeck-config.json` in the data folder
- **THEN** the host creates it as data-folder, "First Run", says and runs

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

#### Scenario: File without a version
- **WHEN** a configuration without `version` is loaded and the host saves it
- **THEN** it loads without a warning and the saved file has `"version": 1`

#### Scenario: Migration from an older version
- **WHEN** a host whose current version is N loads a file with `"version": N-1` and saves it
- **THEN** the migration from N-1 to N ran, the log names it, `urdeck-config.v<N-1>.json` holds the original content,
  and the saved file has `"version": N`

#### Scenario: File from a newer UrDeck
- **WHEN** the file has `"version": 2` and an unknown top-level key, and the host (version 1) runs and quits
- **THEN** a warning says the file was written by a newer UrDeck, the deck shows what the host can read, and the file
  is byte for byte unchanged after the host ends

### Requirement: Application Entry Point
The host MUST provide the application entry point with:

- Startup sequence: data folder resolution (see data-folder), monitor enumeration, config load, theme selection,
  plugin scan, target selection, window/layout, per-widget timers. When the target is absent (see display-targeting,
  "Absent Display") the sequence stops after target selection, with the tray icon present, and continues when the
  display is connected
- Unhandled UI exceptions logged via `UrDeckLog`
- Diagnostics written to `urdeck.log` in the data folder (no console output). Each connected monitor is logged with
  its name, model, device name, bounds and scaling
- `--snapshot out.png [--size WxH] [--theme name] [--page N]`: renders a page off-screen via `PageRenderer` with the
  same layout, card and widget code, including the reserved band and the page indicator as the configuration says,
  writes a PNG (default size = target monitor resolution, default theme = the configured theme, default page = the
  page at `activePage`) and exits with 0 on success, 1 on failure. `--page` is the 0-based index of the page, and an
  index outside the page list is a failure with a logged reason. The indicator shows page N as the current page.
  `--snapshot` never creates or writes the configuration: it does not remember a display, and with no configuration it
  draws the sample page (see data-folder, "First Run"). When the target monitor is absent and `--size` is not given,
  the size is the primary monitor's resolution and the log says so

#### Scenario: Application starts clean
- **WHEN** the user launches the urdeck host
- **THEN** the window appears on the target monitor with configured widgets
- **THEN** widgets render at their configured refresh rates

#### Scenario: Snapshot
- **WHEN** the host is run with `--snapshot out.png --size 1100x3840`
- **THEN** a 1100×3840 PNG of the active page is written and no window is shown

#### Scenario: Snapshot with a named theme
- **WHEN** the host is run with `--snapshot out.png --theme default-light`
- **THEN** the PNG shows the page under the light theme without the configuration being changed

#### Scenario: Snapshot of another page
- **WHEN** the host is run with `--snapshot out.png --page 1` and the configuration has three pages
- **THEN** the PNG shows the second page and an indicator with the middle mark as the pill

#### Scenario: Snapshot page out of range
- **WHEN** the host is run with `--snapshot out.png --page 5` and the configuration has three pages
- **THEN** no PNG is written, the reason is logged and the exit code is 1

#### Scenario: Snapshot without the target monitor
- **WHEN** the host is run with `--snapshot out.png` and the remembered display is not connected
- **THEN** a PNG at the primary monitor's resolution is written, and the configuration file is not modified

#### Scenario: Started before the display is ready
- **WHEN** the host starts at sign-in before the remembered display is connected
- **THEN** no window is shown, and the deck appears with its widgets when the display is connected

### Requirement: Single Instance
At most one host MUST run per data folder (see data-folder) for one signed-in user:

- A host that starts while another host with the same data folder is running writes one line to the log, shows no
  window and no tray icon, loads no plugin, does not write the configuration, and exits with code 0
- Hosts with different data folders do not exclude each other, so a portable development build can run beside an
  installed copy. Two per-user copies in different application folders share `%APPDATA%\UrDeck` and exclude each other
- `--snapshot` and `--quit` are not hosts in this sense: they run while a host is running and do not prevent one
  from starting
- The folder is compared without regard to case
- When the first host ends by any route, including being terminated, a new one can start at once

#### Scenario: Second copy from the same folder
- **WHEN** a host is running and `UrDeck.Host.exe` in the same folder is started again
- **THEN** the second process exits with code 0, the log has one line saying another copy is running, and there is
  still one tray icon and one deck

#### Scenario: Two installed copies
- **WHEN** a per-user copy is running and another per-user copy in a different application folder is started
- **THEN** the second process exits with code 0 and the log says another copy is running

#### Scenario: Development build beside the installed copy
- **WHEN** the installed copy is running and a host is started from a build output folder that holds `urdeck.portable`
- **THEN** both run, each with the configuration and log of its own data folder

#### Scenario: Snapshot while running
- **WHEN** a host is running and `--snapshot out.png` is run from the same folder
- **THEN** the PNG is written and the running host is unaffected

#### Scenario: After a crash
- **WHEN** the running host is terminated and `UrDeck.Host.exe` is started again
- **THEN** the new host starts normally

### Requirement: Quit Command
`UrDeck.Host.exe --quit` MUST ask the host that runs with the same data folder to close and wait for it:

- The running host closes as it does for the tray's Quit: the window closes, the configuration is saved, the tray icon
  is removed and the process ends
- The command exits with 0 once that host has ended, and with 0 at once when no host with that data folder is running
- It exits with 1 when the host has not ended after 10 seconds, and logs that
- It shows no window, loads no plugin and does not write the configuration
- It only reaches a host of the same signed-in user

#### Scenario: Quit a running host
- **WHEN** a host is running and `--quit` is run from the same folder
- **THEN** the deck and the tray icon disappear, the log shows the same shutdown lines as after the tray's Quit, and
  the command exits with 0

#### Scenario: Nothing running
- **WHEN** `--quit` is run and no host with that data folder is running
- **THEN** the command exits with 0 and starts nothing

#### Scenario: Another data folder's host
- **WHEN** a portable host is running and `--quit` is run by a per-user copy
- **THEN** the portable host keeps running and the command exits with 0
