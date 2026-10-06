## MODIFIED Requirements

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

## ADDED Requirements

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
