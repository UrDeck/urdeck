## MODIFIED Requirements

### Requirement: Widget Metadata Attributes
Every widget MUST be decorated with metadata attributes that describe its identity, sizing, and refresh strategy. The following attributes are required:

- `[Widget(name, description)]` — Identifies the widget. REQUIRED on all widget classes. The optional `Id` property is the stable type id used as `typeId` in `urdeck-config.json` (falls back to `Name`); authors SHOULD set it (e.g. `Id = "urdeck.widgets.clock"`).
- `[WidgetSize]` — Specifies one or more valid (width, height) grid unit sizes the widget supports. At least one `[WidgetSize]` is REQUIRED.
- Exactly one of `[RefreshOnTick]`, `[RefreshAdaptive]`, or `[RefreshOnData]` — Declares the widget's refresh strategy. REQUIRED.
- `[Category]` — Groups the widget in the editor UI (optional but recommended for discoverability).

A widget implementation that is missing any required attribute or has conflicting attributes MUST NOT be usable at runtime.

`[RefreshOnEvent]` no longer exists. This is a deliberate breaking change to the SDK: the SDK assembly version
changes with it, and widgets built against the earlier SDK must be rebuilt.

#### Scenario: Widget with all required attributes
- **WHEN** a class is decorated with `[Widget]`, at least one `[WidgetSize]`, and exactly one refresh attribute
- **THEN** the plugin loader registers the widget successfully under its `Id`

#### Scenario: Widget without explicit Id
- **WHEN** `[Widget("Clock", "...")]` omits `Id`
- **THEN** the widget is registered under its `Name`

#### Scenario: Widget missing required [Widget] attribute
- **WHEN** a widget class lacks the `[Widget]` attribute
- **THEN** the plugin loader skips the class with a logged reason and the Roslyn analyzer emits a compile-time error (URDECK001)

#### Scenario: Widget with incompatible refresh attributes
- **WHEN** a widget declares both `[RefreshOnTick]` and `[RefreshOnData]`
- **THEN** the Roslyn analyzer emits a compile-time error (URDECK004) and the loader rejects the type at runtime (a count of refresh strategies other than one)

#### Scenario: Widget with no refresh strategy
- **WHEN** a widget declares no refresh attribute
- **THEN** the analyzer emits URDECK003 and the loader rejects the type

#### Scenario: Widget with no WidgetSize
- **WHEN** a widget lacks `[WidgetSize]` entirely
- **THEN** the Roslyn analyzer emits a compile-time error (URDECK002) and the loader rejects the type

#### Scenario: Widget with invalid size
- **WHEN** a `[WidgetSize]` has a width outside 1–4 or a height below 1
- **THEN** the analyzer emits URDECK005

### Requirement: Widget Interface Contract
Every widget MUST implement the non-generic `IWidget` (usually via `Widget<TConfig>`, which also implements `IWidget<TConfig>`), where `TConfig` is a class derived from `WidgetConfig` with a parameterless constructor. `IWidget` MUST expose:

- `string Name`, `string Description`, `string Category` — metadata (defaulted from attributes by `Widget<TConfig>`)
- `Size[] SupportedSizes` — valid grid unit dimensions (defaulted from `[WidgetSize]` attributes)
- `Type ConfigType` — the configuration type
- `WidgetConfig Config` — the current configuration
- `void Configure(WidgetConfig config)` — applies a configuration, which MUST be an instance of `ConfigType`
- `void Attach(IWidgetHost host)` — receives the services the host offers the widget (see "Host Services"); the default does nothing
- `IReadOnlyCollection<string> Subscriptions` — the ids of the readings the widget uses (see "Reading Subscriptions"); empty by default
- `Task UpdateAsync(CancellationToken)` — refreshes data (network, sensors, ...) off the render path; MUST NOT touch the canvas; the default does nothing
- `void Render(WidgetRenderContext context)` — draws the widget synchronously on the UI thread; the canvas is only valid for the duration of the call

`IWidget<TConfig>` additionally exposes a typed `Config` and `TConfig DefaultConfig`.

There is no `RenderAsync`.

#### Scenario: Widget derives from Widget<TConfig>
- **WHEN** a class derives from `Widget<TConfig>` and overrides only `Render`
- **THEN** its Name, Description, Category and SupportedSizes come from its attributes and the widget can be instantiated and configured by the host

#### Scenario: Wrong config type
- **WHEN** `Configure` is called with a config that is not an instance of the widget's `TConfig`
- **THEN** an `ArgumentException` is thrown

#### Scenario: Class is not a concrete widget
- **WHEN** a type in a plugin is abstract, an interface, lacks a public parameterless constructor, or does not implement `IWidget`
- **THEN** the plugin loader ignores it (or logs a reason for widget-like types) and continues

#### Scenario: Widget that uses no readings
- **WHEN** a widget overrides neither `Attach` nor `Subscriptions`
- **THEN** it is created, configured and painted as before, and nothing is subscribed for it

### Requirement: Widget Registry
The runtime MUST maintain a `WidgetRegistry` that:

- Is populated at startup by scanning all loaded plugin assemblies, and rebuilt on plugin hot-reload
- Maps widget type ids (case-insensitive) to `WidgetDescriptor`s (id, metadata, widget type, config type, sizes, refresh strategy and interval)
- Exposes `GetDescriptor(string typeId)` and `GetWidget(string typeId)` (returns the registered `Type`)
- Creates a new configured widget instance per placed widget (`CreateWidget(WidgetConfig)`), returning null for an unknown type

#### Scenario: Registry populates at startup
- **WHEN** the host starts and loads plugins from `plugins/`
- **THEN** all valid widgets are in the registry

#### Scenario: Registry returns widget type
- **WHEN** `GetWidget("urdeck.widgets.clock")` is called
- **THEN** the correct `Type` object for ClockWidget is returned

### Requirement: Refresh Policy System
Each widget declares its refresh strategy. The runtime MUST:

- Support `[RefreshOnTick(interval, unit)]` — schedule a timer that refreshes and renders at fixed intervals (units: milliseconds, seconds, minutes, hours)
- Support `[RefreshAdaptive(minMs, maxMs)]` — schedule a timer at `minMs` (load-based scaling up to `maxMs` is specified by a later change)
- Support `[RefreshOnData]` — no timer; the widget is rendered once on load and then when one of its readings changes
- Never schedule timers for widgets without a timer-based strategy
- Never allow a widget to have zero or multiple refresh strategies (compile-time and runtime error)

A change to one of a widget's readings triggers the repaint check under every strategy (see "Reading Subscriptions"),
so a widget with a timer may also use readings.

#### Scenario: OnTick refresh works
- **WHEN** a widget has `[RefreshOnTick(1, TimeUnit.Seconds)]`
- **THEN** the runtime schedules a timer that refreshes and renders every 1 second

#### Scenario: OnData widget has no timer
- **WHEN** a widget has `[RefreshOnData]`
- **THEN** the widget is rendered once on load, no timer is scheduled for it, and it is repainted when one of its readings changes

#### Scenario: Timer widget that also uses a reading
- **WHEN** a widget has `[RefreshOnTick]` and declares a reading
- **THEN** it is refreshed on its timer and also checked for a repaint when the reading changes

#### Scenario: No refresh strategy is an error
- **WHEN** a widget has no refresh attribute
- **THEN** the Roslyn analyzer emits a compile-time error and the loader rejects the type

## ADDED Requirements

### Requirement: Host Services
The host MUST hand every widget an `IWidgetHost` through `Attach` after the widget is created and before its first
`Configure`. `IWidgetHost` exposes the readings the widget may read (`Readings`). `Widget<TConfig>` stores it and
offers `Readings` to the derived class.

- A widget that was never attached (for example one created directly in a test) MUST still work: `Readings` then
  reports every reading as unavailable
- Reading a value or a description through `Readings` MUST be safe from the UI thread at any time, including inside
  `NeedsRender` and `Render`, and MUST NOT block or perform I/O
- A widget MUST NOT keep `IWidgetHost` or anything obtained from it beyond its own lifetime

#### Scenario: Widget reads during Render
- **WHEN** an attached widget reads one of its declared readings inside `Render`
- **THEN** it receives the latest value and state without waiting

#### Scenario: Unattached widget
- **WHEN** a widget is created directly and painted without `Attach`
- **THEN** its readings are unavailable and nothing throws

### Requirement: Reading Subscriptions
A widget declares the readings it uses through `Subscriptions`, which it computes from its configuration. The host
reads `Subscriptions` after each `Configure` and subscribes on the widget's behalf (see host-shell, "Reading
Subscriptions And Repaint"); a widget never subscribes or unsubscribes itself.

- `Subscriptions` MUST be cheap and free of side effects, and MUST return the same ids until the configuration changes
- A widget MUST only rely on readings it has declared; reading an undeclared id returns whatever is known, which may
  be unavailable
- When a declared reading changes, the host asks `NeedsRender` and repaints the widget when it returns `true`.
  `UpdateAsync` is NOT called for a reading change

#### Scenario: Subscriptions follow the configuration
- **WHEN** a widget's configuration names the reading `system:cpu/core/2/load`
- **THEN** `Subscriptions` contains exactly that id

#### Scenario: Reading changes
- **WHEN** a declared reading changes and `NeedsRender` returns `true`
- **THEN** the widget is repainted and `UpdateAsync` is not called

#### Scenario: Reading changes without a visible difference
- **WHEN** a declared reading changes and `NeedsRender` returns `false`
- **THEN** the widget is not repainted
