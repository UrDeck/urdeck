## MODIFIED Requirements

### Requirement: Monitor Selection
The host MUST show the deck on the remembered display (see display-targeting, "Remembered Display"). The selectors in
the configuration make the choice when no display is remembered, or when they were edited since it was remembered:

- `monitorName` may be `primary`, `tallest` (tallest portrait monitor), `widest`, `largest` (by pixel area), or a device name suffix such as `DISPLAY1`
- If `monitorName` is unset or matches nothing, the 1-based `monitor` index is used
- If that also fails, the primary monitor is used
- The selectors are not evaluated again on a display change: once a display is remembered, a display that would now
  match the selector better does not take the deck
- The primary monitor is the fallback only for this choice. A remembered display that is not connected is absent
  (see display-targeting, "Absent Display"); the deck does not move to the primary monitor
- A config change that edits the selectors re-runs selection and moves the window without restarting

#### Scenario: Tallest monitor selected
- **WHEN** `monitorName` is `"tallest"`, no display is remembered and a 1100×3840 portrait panel is attached
- **THEN** the window covers that panel and the panel is remembered

#### Scenario: Selector no longer matches the remembered display
- **WHEN** the panel was remembered from `"tallest"` and is disconnected, leaving a 1080×1920 portrait monitor as the
  tallest
- **THEN** the deck is hidden and the 1080×1920 monitor is not covered

### Requirement: Host Window
The host application MUST provide a borderless window that:

- Covers the target monitor's full physical bounds (not the work area) with its content area exactly: no window border
  or frame is visible inside the monitor's bounds
- Cannot be resized or moved by the user
- Is process-wide PerMonitorV2 DPI aware; monitors are enumerated in physical pixels
- Re-applies the exact bounds when the monitor's scaling changes or the window moves to a monitor with different scaling
- Sits in normal Z-order (does not use topmost) and does NOT use window transparency
- Never takes focus: touching, clicking or dragging on it MUST NOT make it the foreground window or move keyboard
  focus away from the window that has it, so using the deck does not interrupt a game or any other application. As a
  consequence the window receives no keyboard input and does not close on Escape. Showing the window again after its
  display returned MUST NOT take focus either
- Is not required to have a taskbar button; it is closed from the tray icon (see tray-icon), or by Escape in the
  development override
- Starts activatable, so that it can be given focus for development, when the environment variable
  `URDECK_ACTIVATABLE` is set to `1`; in that case Escape closes the window
- Is hidden while its display is absent (see display-targeting, "Absent Display")

#### Scenario: Content area covers the full monitor
- **WHEN** the host starts on a 1100×3840 target monitor
- **THEN** the window's content area is 1100×3840 physical pixels at the monitor's origin
- **THEN** a mismatch between the content area and the target bounds is logged as a warning

#### Scenario: Window survives display changes
- **WHEN** display settings change, or the machine resumes from sleep
- **THEN** the host finds the remembered display again and converges to a verified placement on it (see
  display-targeting, "Verified Placement After Display Changes"), because monitors wake in arbitrary order

#### Scenario: Page returns after sleep
- **WHEN** the machine resumes from sleep, or the graphics device is reset
- **THEN** the window background and every widget are shown again without restarting the host

#### Scenario: Tap leaves focus alone
- **WHEN** another application's window has keyboard focus and the user taps the panel
- **THEN** the other window keeps focus and stays in front of the host window
- **THEN** a fullscreen application is not minimised

#### Scenario: Escape does nothing
- **WHEN** the host window is shown and the user presses Escape on the keyboard
- **THEN** the host keeps running

#### Scenario: Closed from the tray
- **WHEN** the user chooses Quit from the tray icon's menu
- **THEN** the host window closes and the process ends

#### Scenario: Development override
- **WHEN** the host is started with `URDECK_ACTIVATABLE=1`
- **THEN** the window can take focus and Escape closes it

#### Scenario: Display returns while another application has focus
- **WHEN** the deck's display is connected again while another application's window has keyboard focus
- **THEN** the deck is shown and the other window keeps focus

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

### Requirement: Application Entry Point
The host MUST provide the application entry point with:

- Startup sequence: monitor enumeration, config load, theme selection, plugin scan, target selection, window/layout,
  per-widget timers. When the target is absent (see display-targeting, "Absent Display") the sequence stops after
  target selection, with the tray icon present, and continues when the display is connected
- Unhandled UI exceptions logged via `UrDeckLog`
- Diagnostics written to `urdeck.log` next to the executable (no console output). Each connected monitor is logged with
  its name, model, device name, bounds and scaling
- `--snapshot out.png [--size WxH] [--theme name] [--page N]`: renders a page off-screen via `PageRenderer` with the
  same layout, card and widget code, including the reserved band and the page indicator as the configuration says,
  writes a PNG (default size = target monitor resolution, default theme = the configured theme, default page = the
  page at `activePage`) and exits with 0 on success, 1 on failure. `--page` is the 0-based index of the page, and an
  index outside the page list is a failure with a logged reason. The indicator shows page N as the current page.
  `--snapshot` never writes the configuration: it does not remember a display. When the target monitor is absent and
  `--size` is not given, the size is the primary monitor's resolution and the log says so

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
