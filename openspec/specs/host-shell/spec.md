# Host Shell Specification

## Purpose

Defines the WinUI 3 host application: monitor selection and window placement, one rendering surface per widget, refresh scheduling, plugin discovery and hot-reload, and the application icon.

## Requirements

### Requirement: Monitor Selection
The host MUST choose the target monitor from configuration:

- `monitorName` may be `primary`, `tallest` (tallest portrait monitor), `widest`, `largest` (by pixel area), or a device name suffix such as `DISPLAY1`
- If `monitorName` is unset or matches nothing, the 1-based `monitor` index is used
- If that also fails, the primary monitor is used
- A config change re-runs selection and moves the window without restarting

#### Scenario: Tallest monitor selected
- **WHEN** `monitorName` is `"tallest"` and a 1100×3840 portrait panel is attached
- **THEN** the window covers that panel

### Requirement: Host Window
The host application MUST provide a borderless window that:

- Covers the target monitor's full physical bounds (not the work area) with its content area exactly: no window border
  or frame is visible inside the monitor's bounds
- Cannot be resized or moved by the user
- Is process-wide PerMonitorV2 DPI aware; monitors are enumerated in physical pixels
- Re-applies the exact bounds when the monitor's scaling changes or the window moves to a monitor with different scaling
- Sits in normal Z-order (does not use topmost) and does NOT use window transparency
- Closes on Escape

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

### Requirement: Widget Surfaces
The host MUST give every widget its own drawing surface:

- One surface per widget, positioned at the widget's card rectangle (see grid-layout); there is NO page-level drawing
  surface
- The surface is rendered at the monitor's physical resolution
- The host draws the widget's card on the surface before the widget renders (see widget-card)
- Pixels outside the card's rounded shape are transparent, and a card fill with an alpha below 255 is blended with
  whatever is behind the surface
- Widgets MUST NOT draw outside their assigned surface
- A surface is redrawn only when its own widget is repainted
- A widget whose `Render` throws is drawn as a themed error card and the failure is logged; other widgets are unaffected

#### Scenario: Widget renders to its own surface
- **WHEN** a widget's `Render()` is called
- **THEN** the `Canvas` in `WidgetRenderContext` is that widget's own surface, already showing its card, and
  `PixelSize` is the surface's pixel size

#### Scenario: Widget render throws
- **WHEN** a widget throws inside `Render()`
- **THEN** an error card with the widget name and message is drawn in its place and the exception is logged

#### Scenario: Translucent card
- **WHEN** the active theme's card fill is partly transparent
- **THEN** the window background is visible through the card and around its rounded corners

#### Scenario: One widget repaints
- **WHEN** one widget on a page of several is repainted
- **THEN** the other widgets' `Render()` is not called

### Requirement: GPU Composition
The host window MUST be composited by the GPU from independent layers: the window background and one layer per widget
surface. The host MUST NOT produce frames while no layer changes, and MUST NOT run a continuous render loop.

#### Scenario: Still page
- **WHEN** a page is shown and no widget is repainted
- **THEN** no widget surface is redrawn and the host's GPU use is not measurably above zero

#### Scenario: Layer changes
- **WHEN** one widget is repainted
- **THEN** only that widget's layer is updated and the page is composited again

### Requirement: Widget Refresh Scheduling
The host MUST drive each widget from its own refresh policy, with no global render loop:

- `RefreshOnTick` — a per-widget timer on the UI thread at the declared interval; each tick awaits `UpdateAsync` and then repaints
- `RefreshAdaptive` — a timer at `MinMs` (load-based scaling up to `MaxMs` is specified by a later change)
- `RefreshOnEvent` — the widget renders once on load (event-driven refresh is specified by a later change)
- Overlapping updates for one widget are skipped, not queued
- After each refresh the host asks `IWidget.NeedsRender(now)` and repaints only when it returns `true`; a widget is always painted when first placed, after a config change and after a page rebuild

#### Scenario: Idle dashboard
- **WHEN** only a 1-second Clock is placed
- **THEN** the only recurring work is that widget's 1s timer, and idle CPU stays negligible

#### Scenario: Unchanged widget skips repaint
- **WHEN** a 1-second Clock ticks again within the same displayed minute
- **THEN** `UpdateAsync` runs but `NeedsRender` returns `false` and the widget is not repainted

### Requirement: Application Icon
The host executable MUST embed an application icon so the executable, taskbar and window show it. The current artwork is a placeholder.

#### Scenario: Icon is present
- **WHEN** the built `UrDeck.Host.exe` is inspected
- **THEN** it carries an embedded icon

### Requirement: Plugin Directory Setup
The host MUST discover plugins in the `plugins/` subdirectory of its executable:

- Creates `plugins/` if missing and scans it for `.dll` files on startup
- Loads each assembly into its own collectible `AssemblyLoadContext` from a shadow copy under the system temp folder (`urdeck-shadow/<pid>/`), so the original file is never locked; `UrDeck.Sdk`, SkiaSharp and the framework resolve from the default context
- Uses `FileSystemWatcher` to detect changes and hot-reloads: unloads old contexts, re-registers widgets, and rebuilds the page (`PluginsChanged`)
- Debounces file change events by 500ms
- Logs warnings for assembly load failures and continues loading remaining plugins
- Rejects widget types that fail descriptor validation (see widget-sdk) with a logged reason
- Removes shadow directories left by dead processes at startup

#### Scenario: Plugin loads successfully
- **WHEN** a valid plugin DLL with a widget implementation is in `plugins/`
- **THEN** the widget is registered in the registry

#### Scenario: Plugin fails to load
- **WHEN** a DLL in `plugins/` is invalid or missing dependencies
- **THEN** the host logs a warning to `urdeck.log` and continues

#### Scenario: Plugin replaced while running
- **WHEN** a plugin DLL in `plugins/` is overwritten or deleted while the host runs
- **THEN** the original file is not locked, the plugin is reloaded (or removed) after the debounce, and the page is rebuilt

#### Scenario: Unknown widget type in config
- **WHEN** the config references a `typeId` with no registered widget
- **THEN** its cell stays empty and a warning listing the registered ids is logged

### Requirement: Configuration Management
The host MUST manage configuration via `urdeck-config.json`:

- Located next to the executable; created with defaults if it does not exist
- Saves configuration on exit
- Hot-reloads configuration on file change (1s debounce), retrying when the file is briefly locked and ignoring its own saves
- On a parse error keeps running with the last good configuration and logs a warning
- Preserves widget-specific settings (extra JSON properties on a widget object) across load/save

#### Scenario: Config file does not exist
- **WHEN** the host starts with no `urdeck-config.json`
- **THEN** the host creates a default config with one empty page and runs

#### Scenario: Config edited while running
- **WHEN** the user changes a widget setting (e.g. `format` to `"12h"`) or `monitorName` and saves
- **THEN** the host reloads, re-targets the monitor if needed, and rebuilds the page

#### Scenario: Invalid config edit
- **WHEN** the file is saved with invalid JSON
- **THEN** the host logs a warning and keeps the previous configuration

### Requirement: Application Entry Point
The host MUST provide the application entry point with:

- Startup sequence: monitor enumeration, config load, theme selection, plugin scan, target selection, window/layout,
  per-widget timers
- Unhandled UI exceptions logged via `UrDeckLog`
- Diagnostics written to `urdeck.log` next to the executable (no console output)
- `--snapshot out.png [--size WxH] [--theme name]`: renders the active page off-screen via `PageRenderer` with the same
  layout, card and widget code, writes a PNG (default size = target monitor resolution, default theme = the configured
  theme) and exits with 0 on success, 1 on failure

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

### Requirement: Theme Application
The host MUST apply the selected theme (see the `theme` capability) to everything it draws:

- The theme is selected on startup and again on every configuration reload
- The window background uses the theme's background colour
- Every widget surface receives the same theme, resolved for the target monitor's physical pixels
- When the theme or the monitor's size or scaling changes, the page is rebuilt so that layout, cards and widgets all
  use the new values

#### Scenario: Window background follows the theme
- **WHEN** the active theme changes from `default-dark` to `default-light`
- **THEN** the window background changes to the light theme's background colour

#### Scenario: Display scaling changes
- **WHEN** the target monitor's scaling changes while the host runs
- **THEN** the page is rebuilt and cards keep the same proportions relative to the grid cell
