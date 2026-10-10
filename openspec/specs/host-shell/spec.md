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
- Never takes focus: touching, clicking or dragging on it MUST NOT make it the foreground window or move keyboard
  focus away from the window that has it, so using the deck does not interrupt a game or any other application. As a
  consequence the window receives no keyboard input and does not close on Escape
- Is not required to have a taskbar button; it is closed from the tray icon (see tray-icon), or by Escape in the
  development override
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

#### Scenario: Closed from the tray
- **WHEN** the user chooses Quit from the tray icon's menu
- **THEN** the host window closes and the process ends

#### Scenario: Development override
- **WHEN** the host is started with `URDECK_ACTIVATABLE=1`
- **THEN** the window can take focus and Escape closes it

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
surface. The host MUST NOT produce frames while no layer changes, and MUST NOT run a render loop while no widget is
animating (see "Animation Frames").

#### Scenario: Still page
- **WHEN** a page is shown and no widget is repainted
- **THEN** no widget surface is redrawn and the host's GPU use is not measurably above zero

#### Scenario: Layer changes
- **WHEN** one widget is repainted
- **THEN** only that widget's layer is updated and the page is composited again

### Requirement: Widget Refresh Scheduling
The host MUST drive each widget's data refresh from its own refresh policy. The only shared loop is the frame source
for animating widgets (see "Animation Frames"), which is independent of the refresh policies:

- `RefreshOnTick` — a per-widget timer on the UI thread at the declared interval; each tick awaits `UpdateAsync` and then repaints
- `RefreshAdaptive` — a timer at `MinMs` (load-based scaling up to `MaxMs` is specified by a later change)
- `RefreshOnData` — no timer; the widget renders once on load and then when one of its readings changes (see "Reading Subscriptions And Repaint")
- Overlapping updates for one widget are skipped, not queued
- After each refresh the host asks `IWidget.NeedsRender(now)` and repaints only when it returns `true`; a widget is always painted when first placed, after a config change and after a page rebuild

#### Scenario: Idle dashboard
- **WHEN** only a 1-second Clock is placed
- **THEN** the only recurring work is that widget's 1s timer, and idle CPU stays negligible

#### Scenario: Unchanged widget skips repaint
- **WHEN** a 1-second Clock ticks again within the same displayed minute
- **THEN** `UpdateAsync` runs but `NeedsRender` returns `false` and the widget is not repainted

#### Scenario: Data-only widget
- **WHEN** a widget with `RefreshOnData` is placed
- **THEN** the host creates no timer for it

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
- `activePage` is the index of the page shown on startup. It is not updated while the host runs, and a reload does not
  move the user to it (see page-navigation)
- `pager.indicator` is the page indicator's mode: `always`, `fade`, `off` or `auto`; absent means `auto`
- `dock` is a list of widget objects shown in the dock (see dock, "Dock Configuration"); absent or empty means no
  dock. Widget-specific settings on its entries are preserved across load/save like those of a page's widgets

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

### Requirement: Animation Frames
The host MUST repaint animating widgets from one shared frame source:

- After each paint of a widget the host reads `IWidget.IsAnimating`; a widget that returns `true` is animating until a
  later paint after which it returns `false`
- While at least one placed widget is animating, the frame source runs at 30 frames per second and repaints each
  animating widget on every frame; widgets that are not animating are not repainted by it
- When no placed widget is animating, the frame source MUST be stopped, not merely idle
- A frame MUST NOT call `UpdateAsync` or `NeedsRender`
- A widget that is removed (page rebuild, plugin reload, config change) stops being animated, and a widget whose
  `IsAnimating` throws is treated as not animating and the failure is logged
- `--snapshot` ignores `IsAnimating` and paints every widget once

#### Scenario: Still page has no frame source
- **WHEN** a page is shown and no widget is animating
- **THEN** the frame source is not running and the only recurring work is the widgets' refresh timers

#### Scenario: One widget animates
- **WHEN** one widget on a page of several reports `IsAnimating`
- **THEN** that widget is repainted about 30 times per second and the other widgets' `Render()` is not called

#### Scenario: Animation ends
- **WHEN** the last animating widget returns `false` from `IsAnimating` after a paint
- **THEN** the frame source stops and CPU and GPU use return to the idle level

#### Scenario: Animating widget is removed
- **WHEN** the page is rebuilt while a widget is animating
- **THEN** the old widget is no longer repainted and the frame source stops unless another widget is animating

### Requirement: Reading Subscriptions And Repaint
The host MUST subscribe each shown widget to the readings it declares (see widget-sdk, "Reading Subscriptions") and
repaint it when they change.

- A widget's readings are subscribed to when the widget is shown and unsubscribed when it is removed (page rebuild,
  plugin reload, config change, window close). A widget that is not shown (`isVisible` false, or on a page that is
  not the shown page) holds no subscription
- When readings change, the host is notified once per batch. On the UI thread it asks `NeedsRender` of each shown
  widget that declared one of the changed readings and repaints those that return `true`
- A reading change MUST NOT call `UpdateAsync`, and MUST NOT repaint widgets that did not declare the reading
- Widgets that declare no readings cause no subscription, no provider and no extra work

#### Scenario: Page with no data widgets
- **WHEN** a page shows only a Clock
- **THEN** no provider is started and the host does no reading-related work

#### Scenario: One reading changes
- **WHEN** a reading used by one widget on a page of several changes
- **THEN** only that widget is asked whether it needs a repaint

#### Scenario: Widget hidden by configuration
- **WHEN** the only widget that uses a provider has `isVisible` set to `false`
- **THEN** the provider is not started

#### Scenario: Widget removed
- **WHEN** a widget that uses a reading is removed from the page
- **THEN** its subscription ends, and the provider stops after the linger period if nothing else uses it

### Requirement: Provider Settings In Configuration
`urdeck-config.json` MAY contain a `providers` object keyed by provider id. Each entry MAY set `intervalMs` (see
data-providers, "Sampling Rate Settings"). The section is optional and absent from a default configuration. Entries
and properties the host does not know MUST be preserved across load and save.

#### Scenario: Section absent
- **WHEN** the configuration has no `providers` section
- **THEN** every provider uses its declared default interval

#### Scenario: Unknown properties survive
- **WHEN** a `providers` entry contains a property the host does not know and the configuration is saved
- **THEN** the property is still in the file

### Requirement: Snapshot Readings
`--snapshot` MUST show readings the way the live page would. Before painting, it subscribes the page's widgets to
their readings and waits until none of them is pending, for at most 2 seconds; it then paints once and stops every
provider before it exits.

#### Scenario: Snapshot of a page with a stats widget
- **WHEN** `--snapshot` is run for a page with a stats widget that shows `system:cpu/load`
- **THEN** the PNG shows a value, not a dash

#### Scenario: Snapshot of a page without data widgets
- **WHEN** `--snapshot` is run for a page with only a Clock
- **THEN** no provider is started and the snapshot is not delayed

