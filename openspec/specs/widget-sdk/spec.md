# Widget SDK Specification

## Purpose

Defines the contract between widgets and the host: the widget base classes and attributes, configuration, plugin loading and refresh policies.
## Requirements
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

### Requirement: WidgetRenderContext
Each widget MUST receive a `WidgetRenderContext` during `Render` containing:

- `SKCanvas Canvas` — SkiaSharp canvas for 2D drawing, sized to the widget's card, already showing the host-drawn card
  and clipped to its shape
- `DateTime Time` — Current timestamp at render time
- `Size PixelSize` — The card's pixel dimensions (width, height); the widget's drawing area starts at `(0, 0)`
- `Theme Theme` — The active theme, with every size already in pixels for this surface (see the `theme` capability)
- `SKRect ContentRect` — The card's area inset by the theme's padding; where content normally goes
- `WidgetConfig Config` — The widget's configuration (concrete type)
- `CancellationToken CancellationToken` — For cooperative cancellation

The colour-only `ThemeColors` type no longer exists. This is a deliberate breaking change to the SDK: the SDK assembly
version changes with it, and widgets built against the earlier SDK must be rebuilt.

#### Scenario: Context is populated at render time
- **WHEN** the runtime calls `Render()`
- **THEN** `WidgetRenderContext` is fully populated with current time, card pixel size, theme, content rectangle and
  config

#### Scenario: Content rectangle follows the theme
- **WHEN** the theme's padding resolves to 20 pixels and the card is 550 by 275 pixels
- **THEN** `ContentRect` is the rectangle from `(20, 20)` to `(530, 255)`

### Requirement: Widget Configuration Model
Widget configuration data MUST be serializable to/from JSON. The `WidgetConfig` base class MUST expose:

- `string WidgetTypeId` (JSON `typeId`) — The widget type identifier (the `[Widget]` `Id`) used to locate the correct plugin
- `int Col` — Grid column position (0-3, relative to 4-column grid)
- `int Row` — Grid row position (0-N)
- `int Width` — Width in grid columns (1-4)
- `int Height` — Height in grid rows (1-N)
- `Dictionary<string, string> Parameters` — Free-form key-value parameters
- `bool IsVisible` — Visibility flag
- Widget-specific settings as additional properties on the widget's JSON object, preserved through `[JsonExtensionData]` and converted to the concrete config type (by the engine through its `ToConcrete` extension) when the widget is created; properties absent from the JSON keep the concrete type's defaults

#### Scenario: Config serializes to JSON
- **WHEN** `WidgetConfig` is serialized with `System.Text.Json`
- **THEN** the output contains all public properties as camelCase JSON keys

#### Scenario: Config deserializes with default values
- **WHEN** `WidgetConfig` is deserialized with `System.Text.Json`
- **THEN** properties not present in JSON retain their default values

#### Scenario: Widget-specific settings round-trip
- **WHEN** a widget object contains `"format": "12h"` and is loaded, converted to `ClockConfig`, and the config is saved
- **THEN** the concrete config has `Format == "12h"` and the saved file still contains `format`

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

### Requirement: Plugin Isolation and Hot-Reload
The plugin loader MUST load each plugin DLL into its own collectible `AssemblyLoadContext` from a shadow copy:

- The original DLL is never locked and can be overwritten or deleted while loaded
- `UrDeck.Sdk`, SkiaSharp and framework assemblies resolve from the default context so widget contract types are shared with the host
- On reload, previous contexts are unloaded and the registry is rebuilt; a corrupt DLL is skipped without affecting the others
- Unloaded contexts SHOULD become collectible; the loader logs a warning if one is still alive after 4 seconds

#### Scenario: Plugin uses the host's contract types
- **WHEN** a plugin is loaded in its own context
- **THEN** its widget types are assignable to the host's `IWidget`

#### Scenario: Plugin rebuilt while running
- **WHEN** the plugin DLL is replaced and a reload occurs
- **THEN** the new version is registered and the old one is unloaded

### Requirement: Render Skipping
A widget MAY tell the host that nothing visible changed since the last paint. `IWidget` exposes `bool NeedsRender(DateTime now)` as a default interface method that returns `true`; `Widget<TConfig>` exposes it as a virtual member. The host MUST call it after each refresh and MUST skip the repaint when it returns `false`.

#### Scenario: Default behavior
- **WHEN** a widget does not override `NeedsRender`
- **THEN** it is repainted on every refresh

#### Scenario: Widget reports no change
- **WHEN** `NeedsRender` returns `false` after a refresh
- **THEN** the host does not repaint that widget until it returns `true` or the widget is placed again

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

### Requirement: Animation Frames
A widget MAY tell the host that it is in the middle of an animation. `IWidget` exposes `bool IsAnimating` as a default
interface member that returns `false`; `Widget<TConfig>` exposes it as a virtual member. While it returns `true` the
host repaints the widget at the host's frame rate (see host-shell, "Animation Frames"); the widget does not choose the
rate.

- `IsAnimating` MUST be cheap and MUST NOT have side effects; the host reads it after each paint of the widget
- A widget MUST compute what it draws from `WidgetRenderContext.Time`, never from a count of frames, so the motion
  looks the same at any frame rate and when frames are dropped
- A widget that has finished animating MUST have drawn its resting state in the paint after which `IsAnimating` first
  returns `false`
- A widget MUST draw its resting state on its first paint and on the first paint after `Configure`
- Animation frames MUST NOT trigger `UpdateAsync`; data refresh stays on the widget's refresh policy
- The member is additive: a widget built against the SDK before this member existed loads and runs unchanged

#### Scenario: Default behavior
- **WHEN** a widget does not override `IsAnimating`
- **THEN** it returns `false` and the widget is painted only by its refresh policy

#### Scenario: Widget animates and stops
- **WHEN** a widget returns `true` from `IsAnimating` after a paint, and `false` after a later paint
- **THEN** it is repainted at the host's frame rate in between, and not again after the paint that returned `false`
  until its refresh policy repaints it

#### Scenario: First paint is at rest
- **WHEN** a widget that can animate is painted for the first time
- **THEN** it draws its resting state and `IsAnimating` returns `false`

#### Scenario: Widget built before the member existed
- **WHEN** a plugin compiled against the SDK without `IsAnimating` is loaded
- **THEN** its widgets are registered and painted as before

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

### Requirement: Tap Input
A widget MAY accept taps by implementing the SDK's tap interface in addition to `IWidget`. The interface has two
members:

- "can tap": given a point in the widget's own pixels, whether a tap there would do something. The default answers
  yes for every point. The host asks it when the pointer goes down, to decide whether to show press feedback, and
  again before delivering the tap. It MUST be cheap and free of side effects
- "on tap": given the point, performs the action. It is called on the UI thread when the tap is recognised, and MUST
  return quickly; it MAY call the host's launch service

Further rules:

- The interface is the declaration: the host treats a widget that does not implement it as not interested in taps and
  calls nothing on it for input
- The point uses the same space as `WidgetRenderContext.PixelSize`, with `(0, 0)` at the top-left corner of the card
- A widget MUST NOT draw a pressed state for a tap; the host shows it (see widget-input, "Press Feedback")
- After "on tap" the host asks `NeedsRender` and repaints the widget when it returns `true`
- The interface is additive: a widget built against the SDK before it existed loads and runs unchanged, and the SDK
  assembly version does not change
- Other kinds of input (scrolling, a long press) will be offered as further optional interfaces beside this one; a
  widget that implements only the tap interface is never sent them

#### Scenario: Widget opts in
- **WHEN** a widget class implements the tap interface
- **THEN** the host delivers taps on its card to it

#### Scenario: Widget does not opt in
- **WHEN** a widget class does not implement the tap interface
- **THEN** it is created, configured and painted as before and receives no input calls

#### Scenario: Widget declines a point
- **WHEN** a widget answers no to "can tap" for a point
- **THEN** no press feedback is shown and no tap is delivered for that point

#### Scenario: Widget built before the interface existed
- **WHEN** a plugin compiled against the SDK without the tap interface is loaded
- **THEN** its widgets are registered and painted as before

### Requirement: Launch And Icon Services
`IWidgetHost` MUST additionally offer a launch service and an icon service (see launcher and icons), and
`Widget<TConfig>` MUST offer both to the derived class.

- Launching takes a target text and optional arguments and returns whether the launch was started
- The SDK also offers the classification of a target text (valid or not, its kind and a short display name) as a pure
  function that needs no host, so a widget and the host always agree on what a target is
- The icon service takes a source text and a wanted pixel size and returns an image or nothing, and whether an absent
  image is still loading
- Both are default members of `IWidgetHost`: a host that does not provide them launches nothing and has no icons
- A widget that was never attached (for example one created directly in a test) MUST still work: launching reports
  failure and every icon is absent and not loading
- The members are additive and the SDK assembly version does not change

#### Scenario: Attached widget launches
- **WHEN** an attached widget calls the launch service with a valid target inside "on tap"
- **THEN** the target is started and the call returns that it was

#### Scenario: Unattached widget
- **WHEN** a widget created directly in a test asks for an icon and launches a target
- **THEN** the icon is absent, the launch reports failure, and nothing throws

#### Scenario: Community widget uses the services
- **WHEN** a widget that references only the SDK asks for the icon of an application and launches it on tap
- **THEN** it compiles and works without referencing the engine or the host

