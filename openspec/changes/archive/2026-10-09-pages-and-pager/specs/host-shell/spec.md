## MODIFIED Requirements

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
  consequence the window receives no keyboard input and does not close on Escape
- Is not required to have a taskbar button; until the tray icon (milestone 2) it is closed by ending the process, or by
  Escape in the development override
- Starts activatable, so that it can be given focus for development, when the environment variable
  `URDECK_ACTIVATABLE` is set to `1`; in that case Escape closes the window

#### Scenario: Content area covers the full monitor
- **WHEN** the host starts on a 1100×3840 target monitor
- **THEN** the window's content area is 1100×3840 physical pixels at the monitor's origin
- **THEN** a mismatch between the content area and the target bounds is logged as a warning

#### Scenario: Window survives display changes
- **WHEN** display settings change, or the machine resumes from sleep
- **THEN** the host re-selects the target monitor and re-covers it, re-checking after 1.5s and again after 5s because
  monitors wake in arbitrary order

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

#### Scenario: Development override
- **WHEN** the host is started with `URDECK_ACTIVATABLE=1`
- **THEN** the window can take focus and Escape closes it

### Requirement: Configuration Management
The host MUST manage configuration via `urdeck-config.json`:

- Located next to the executable; created with defaults if it does not exist
- Saves configuration on exit
- Hot-reloads configuration on file change (1s debounce), retrying when the file is briefly locked and ignoring its own saves
- On a parse error keeps running with the last good configuration and logs a warning
- Preserves widget-specific settings (extra JSON properties on a widget object) across load/save
- `activePage` is the index of the page shown on startup. It is not updated while the host runs, and a reload does not
  move the user to it (see page-navigation)
- `pager.indicator` is the page indicator's mode: `always`, `fade`, `off` or `auto`; absent means `auto`

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

### Requirement: Application Entry Point
The host MUST provide the application entry point with:

- Startup sequence: monitor enumeration, config load, theme selection, plugin scan, target selection, window/layout,
  per-widget timers
- Unhandled UI exceptions logged via `UrDeckLog`
- Diagnostics written to `urdeck.log` next to the executable (no console output)
- `--snapshot out.png [--size WxH] [--theme name] [--page N]`: renders a page off-screen via `PageRenderer` with the
  same layout, card and widget code, including the reserved band and the page indicator as the configuration says,
  writes a PNG (default size = target monitor resolution, default theme = the configured theme, default page = the
  page at `activePage`) and exits with 0 on success, 1 on failure. `--page` is the 0-based index of the page, and an
  index outside the page list is a failure with a logged reason. The indicator shows page N as the current page

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
