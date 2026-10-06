## Why

Widgets can only show data they fetch themselves, each on its own timer, so several cards that want the same value
would each poll for it and nothing can show a system reading at all. Roadmap item 3 is the next step toward the
owner's first page (per-core cards, then the performance widget), and it fixes public SDK API, so it is settled now
while the SDK is unpublished and has one in-repo consumer.

## What Changes

- **Providers become a second plugin kind.** A provider is a class in a plugin assembly that references only
  `UrDeck.Sdk`. It owns an id (`system`), publishes a catalog of the readings it offers, and supplies their values.
  First-party and community providers use the same contract.
- **Readings are self-describing.** A reading has an id (`system:cpu/core/2/load`), a value (number, text or on/off)
  and catalog metadata: kind, default label, full name, device name, optional range, optional default display unit.
  Any widget can show any reading without knowing its provider.
- **The engine owns demand and scheduling.** A widget declares the reading ids it uses, computed from its config. The
  host subscribes while the widget is shown and unsubscribes when it is not. A provider is created on its first
  subscription and stopped after its last, so a deck with no data widgets runs no provider code.
- **Sampling runs off the UI thread**, one loop per provider, at a rate the provider declares and the user can
  override per provider in `urdeck-config.json`. Values are published in batches and read at paint time without
  locks or I/O. A provider that throws or hangs affects only its own readings.
- **Uniform states and formatting.** A reading is ok, pending, stale or unavailable, and every widget shows these the
  same way (a dash, or the last value dimmed). One SDK formatter turns a reading into value text and unit, and picks
  the display unit from the widget's setting, then the reading's default, then the user's region.
- **SDK contract for widgets** (**BREAKING**): `[RefreshOnEvent]` is removed and `[RefreshOnData]` replaces it;
  `IWidget` gains the reading declaration and a minimal host-services hook. The SDK assembly version moves to
  0.3.0.0; plugins must be rebuilt.
- **System provider** (first-party, unelevated): total CPU load, load per logical processor, memory load.
- **Stats widget** (first-party): one reading per card at 1x1 and 2x2, drawn with the existing readout. Its config is
  a list of slots, so the larger compositions of the performance widget extend it without a config change.
- `PLUGIN-EXCEPTION.md` names providers as plugins.

Deliberately not in this change: the gauge ring and the 4x2 and 4x4 compositions (next change); sensors that need
elevation and the opt-in helper (the change after); provider-defined settings and secrets; a per-card sampling rate;
image values; typed topics; backing off when the PC is busy; history; a logger for plugins; SDK version enforcement
and public API tracking.

## Capabilities

### New Capabilities

- `data-providers`: the provider plugin kind, readings and their catalog, ids, demand-driven lifetime, sampling and
  rate settings, reading states, fault containment, and the shared formatting and unit rules.
- `provider-system`: the first-party `system` provider and the readings it offers without elevation.
- `widget-stats`: the first-party stats widget, its slot configuration and how it shows a reading and its states.

### Modified Capabilities

- `widget-sdk`: `[RefreshOnData]` replaces `[RefreshOnEvent]`; the widget contract gains the reading declaration and
  the host-services hook; the widget descriptor no longer carries an event name.
- `host-shell`: the host subscribes shown widgets to their readings and repaints them when a reading changes; the
  configuration gains a `providers` section; `--snapshot` samples readings before it paints.
- `components`: the readout can draw a formatted reading, including its pending, stale and unavailable states.

## Impact

- `sdk/UrDeck.Sdk`: new `Data` types (reading, descriptor, provider contract, formatter), `IWidget`,
  `Widget<TConfig>`, the refresh attributes, `Readout`; `AssemblyVersion` 0.3.0.0, package `Version` 0.4.0.
- `sdk/UrDeck.Analyzer`: URDECK003 and URDECK004 name the new attribute.
- `src/UrDeck.Engine`: the plugin loader and a provider registry, a new reading hub (store, subscriptions, sampling),
  `UrDeckConfig`, `WidgetDescriptor`, widget creation.
- `src/UrDeck.Host`: `WidgetView`, `MainWindow`, `SnapshotCommand`, and the build step that copies first-party plugins.
- New projects: `providers/UrDeck.Providers.System` and `widgets/UrDeck.Widgets.Stats` (both GPL, referencing only the
  SDK), added to `urdeck.slnx`.
- Tests: engine tests for the hub, loader, formatter and stats widget; analyzer tests; the frozen SDK version test.
- Docs: `PLUGIN-EXCEPTION.md`, `README.md` (layout and licence map), `AGENTS.md`, `CONTRIBUTING.md` (authoring a
  provider, using readings in a widget), `docs/ROADMAP.md`, a perf note under `docs/perf/`.
- No new package dependency. The system provider calls Windows APIs directly.
