## MODIFIED Requirements

### Requirement: Widget Metadata Attributes
Every widget MUST be decorated with metadata attributes that describe its identity, sizing, and refresh strategy. The following attributes are required:

- `[Widget(name, description)]` — Identifies the widget. REQUIRED on all widget classes. The optional `Id` property is the stable type id used as `typeId` in `urdeck-config.json` (falls back to `Name`); authors SHOULD set it (e.g. `Id = "urdeck.widgets.clock"`).
- `[WidgetSize]` — Specifies one or more valid (width, height) grid unit sizes the widget supports. At least one `[WidgetSize]` is REQUIRED. Its optional `Preview` value (default `true`) says whether generated pictures of the widget (previews, the site, the picture tests of default configurations) include that size; `Preview = false` leaves the size out of them and does not affect where the widget may be placed.
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

#### Scenario: A size left out of previews
- **WHEN** a widget declares `[WidgetSize(4, 8, Preview = false)]` and `[WidgetSize(4, 2)]`
- **THEN** the widget can be placed at 4x8 and at 4x2, and generated pictures of the widget show only 4x2

## ADDED Requirements

### Requirement: Default Configuration Draws Something
A widget's default configuration (`DefaultConfig`, the configuration a widget gets when it is added without settings)
MUST draw a meaningful card at every size the widget declares for previews, with sample data: content beyond the empty
card, and every reading it subscribes to available from the providers' samples. It MUST NOT depend on the user's machine
(a file path, an installed application). The default configuration does not change the defaults of a configuration file:
a setting absent from `urdeck-config.json` keeps the configuration type's own default, as before.

For the first-party widgets:

- Clock: the time (its configuration type's defaults)
- Stats: CPU load, GPU load, memory load and GPU temperature
- Weather: a named place with the current conditions and, where the size has room, the forecast
- Shortcut: a web address with a label (drawn with its fallback tile when no site icon is available)

The repository's tests MUST draw every first-party widget at every previewed size with its default configuration and
fail when a card is empty or a subscribed reading is unavailable.

#### Scenario: A new stats card
- **WHEN** a stats widget with its default configuration is drawn at 4x2 with sample data
- **THEN** the card shows CPU, GPU and memory readings

#### Scenario: A configuration file without slots
- **WHEN** `urdeck-config.json` has a stats widget with no `slots`
- **THEN** it draws as it did before this change

#### Scenario: A default that draws nothing
- **WHEN** a first-party widget's default configuration draws an empty card at one of its sizes
- **THEN** a test fails, naming the widget and the size
