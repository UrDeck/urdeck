## Context

See `proposal.md` for the motivation and `specs/` for the required behaviour. What shapes the approach:

- A widget has a parameterless constructor, `Configure(config)` and `Render(context)`. Nothing lets it receive
  anything from the host, and `NeedsRender(now)` has no context argument.
- `WidgetView` owns a `DispatcherQueueTimer` per widget (`UpdateAsync`, then `NeedsRender`, then `Invalidate`), started
  on `Loaded` and stopped on `Unloaded`. A page rebuild disposes every view and creates new widget instances.
- `[RefreshOnEvent(name)]` stores a name that nothing raises. `[RefreshAdaptive]` runs at `minMs`.
- Plugins load into collectible `AssemblyLoadContext`s. Types defined in `UrDeck.Sdk` have one identity across
  plugins; types defined in a plugin do not. `WidgetPluginLoader.LoadAssembly` only looks for `IWidget`.
- `--snapshot` (`SnapshotCommand`) runs with the engine alone, paints through `PageRenderer` and exits.
- The SDK is unpublished with one in-repo consumer; its `AssemblyVersion` is frozen at 0.2.0.0 and moves only on a
  deliberate breaking change.
- An exploration on 2026-10-05 started from roadmap item 3 ("named topics with typed latest values, one provider per
  topic") and changed it. The decisions below record what changed and why, so the earlier direction is not
  rediscovered as an omission.

## Goals / Non-Goals

**Goals:**

- A provider is a plugin, and the first-party one uses the same contract a community author would.
- Any widget can show any reading: a stats card works for a CPU core today and a Home Assistant sensor later.
- A deck with no data widgets pays nothing: no provider instance, no thread, no timer.
- Nothing a provider does can block the UI thread or another provider.
- The stats widget's config and code are the base of the performance widget, not a throwaway.

**Non-Goals:**

- Typed topics (`Topic<NowPlaying>`): a compile-time contract shared between separately built plugins. Rich data is
  modelled as a group of readings (`media/title`, `media/artist`, `media/state`). A provider and its widgets that ship
  in one assembly already share types.
- Image values. They arrive with the first consumer (album art).
- Provider-defined settings and secrets (a Home Assistant URL and token, a weather location). The `providers` section
  holds only the engine's `intervalMs` for now; unknown properties are preserved so those settings can live there.
- A per-card sampling rate. The engine already samples at one rate per provider, so adding "fastest subscriber wins"
  later does not change the contract.
- Elevated sensors and the helper process; the gauge ring and the 4x2 and 4x4 compositions; history buffers.
- Backing off when the PC is busy or on battery, and giving `[RefreshAdaptive]` its meaning. The engine owns every
  provider's timer, so that is one place to change later.
- A catalog that changes while the provider runs (hot-plugged hardware, new Home Assistant entities).
- A logger for plugins. The engine logs provider failures itself.
- Killing a hung in-process provider. That needs the out-of-process helper.
- More than 64 logical processors (processor groups) in the system provider.

## Decisions

### 1. Self-describing readings instead of typed topics

A reading's value is an SDK type holding a number, a text or an on/off state, and its meaning comes from a catalog
entry, not from a C# type the consumer compiles against.

Alternative considered: named topics with a typed payload per topic (roadmap item 3). It makes cross-plugin type
identity the central problem, ties each widget to one provider, and gives an editor nothing to list. Every consumer on
the roadmap before media is a number with a unit, and Home Assistant, the motivating community case, already models
everything as self-describing states.

### 2. A provider owns a namespace and a catalog, not one topic

A hardware source updates a whole device at once and returns many values, so "one provider per topic" is the wrong
granularity. A provider owns the ids under its provider id, lists them in a catalog and is told which are wanted.

Ids are `<provider id>:<path>`, compared with `OrdinalIgnoreCase`. The provider id is the collision domain: the loader
rejects a second provider with the same id (the first by file name order wins, so the result is deterministic).

### 3. SDK surface

In `sdk/UrDeck.Sdk/Data` (namespace `UrDeck.Sdk.Data`). Names are indicative; the specs fix the behaviour.

```
 ReadingValue        readonly struct: Number(double) | Text(string) | OnOff(bool)
 ReadingKind         enum: Number, Percent, Temperature, Text, OnOff
 ReadingState        enum: Ok, Pending, Stale, Unavailable
 Reading             readonly record struct: State, Value?, Reason?   (what a widget reads)
 ReadingDescriptor   sealed record: Path, Kind, Label, Name, Device?, Min?, Max?, Unit?,
                                    DisplayUnit?, Decimals?            (a catalog entry)
 DisplayUnit         enum: Celsius, Fahrenheit                         (grows with kinds)

 [DataProvider(id, name, DefaultIntervalMs = 1000, MinIntervalMs = 250)]
 IDataProvider
     IReadOnlyList<ReadingDescriptor> Describe()
     void Start(IReadingSink sink)
     void SetDemand(IReadOnlyCollection<string> paths)
     Task SampleAsync(CancellationToken ct)
     void Shutdown()
 IReadingSink                                    (given to the provider; thread-safe)
     void Publish(string path, ReadingValue value)
     void Unavailable(string path, string reason)

 IReadingSource                                  (given to the widget through IWidgetHost)
     Reading Read(string id)
     ReadingDescriptor? Describe(string id)
     bool RegionUsesFahrenheit                   (level 3 of the unit rule)
 IWidgetHost
     IReadingSource Readings

 ReadingFormatter.Format(Reading, ReadingDescriptor?, ReadingFormatOptions, IReadingSource) -> ReadingText
 ReadingText          Value, Unit, UnitPlacement, WidestValue, IsCurrent
```

- Member names avoid words that are keywords in other .NET languages (analyzer CA1716 flags `Get` and `Stop`), so the
  contract needs no analyzer suppression: a widget reads with `Read(id)` and a provider ends with `Shutdown()`.
- One contract serves polled and pushed providers. A polled provider publishes inside `SampleAsync`; a pushed one
  publishes from its own callbacks between samples and may leave `SampleAsync` empty.
- "Published together" is delimited by the engine, not the provider: everything published during one `SampleAsync`
  call is committed when the call returns. A publication outside a sample is committed on its own.
- `IDataProvider` is an interface with no base class. A provider has little shared behaviour, and an interface keeps
  the loader's check the same as for `IWidget`.
- `IWidget` gains `void Attach(IWidgetHost host) { }` and `IReadOnlyCollection<string> Subscriptions => []` as default
  interface members; `Widget<TConfig>` stores the host and exposes `protected IReadingSource Readings`, which is a
  null object (everything unavailable) until attached. This is the host-services hook the roadmap parked, in its
  smallest form; a logger joins `IWidgetHost` when a plugin needs one.

Alternative considered: readings on `WidgetRenderContext`. Rejected because `NeedsRender` needs them and has no
context, and adding a parameter there breaks every widget for no gain.

Alternative considered: `[Subscribes("system.cpu")]` on the widget class. Rejected because the reading depends on the
instance's config (the core a card points at).

### 4. `[RefreshOnData]` replaces `[RefreshOnEvent]`, and readings are independent of the timer attribute

A reading change triggers the repaint check for any widget that declared the reading, whatever its refresh attribute.
`[RefreshOnData]` only means "no timer". `RefreshStrategy.OnEvent` becomes `OnData` and `WidgetDescriptor.RefreshEvent`
is removed. URDECK003 and URDECK004 keep their ids and name the new attribute.

Removing a public attribute is breaking, so `AssemblyVersion` moves to 0.3.0.0 (package `Version` 0.4.0), the csproj
comment records it and the frozen-version test is updated deliberately. The Clock is rebuilt with the solution and
needs no source change.

### 5. One hub in the engine

`src/UrDeck.Engine/Data` gains a provider registry (filled by the loader, like `WidgetRegistry`) and a `ReadingHub`:

```
 UI thread                         ReadingHub                         thread pool
 ─────────                         ──────────                         ───────────
 WidgetView shown
   Subscribe(ids) ───────────────▶ ref-count per id
                                   first id of a provider ──────────▶ create, Describe(), Start(sink),
                                                                      SetDemand(paths), run loop
                                   store: ConcurrentDictionary        loop: SampleAsync ─▶ sink.Publish…
                                          <id, Reading>   ◀── commit batch when the call returns
   ReadingsChanged(ids) ◀───────── raised once per batch (on the sampling thread)
   DispatcherQueue.TryEnqueue
     views using an id: NeedsRender? ▶ Invalidate
   Render: Readings.Read(id) ─────▶ dictionary read, no lock
 WidgetView disposed
   Unsubscribe ──────────────────▶ last id of a provider: stop after 5 s linger
```

- `Reading` is immutable and a store entry is replaced whole, so a paint reads a consistent value without a lock.
- `ReadingsChanged` carries only ids whose `Reading` actually changed (value or state), so an idle machine whose
  values do not move raises nothing. The widget's `NeedsRender` then filters on the formatted text.
- The hub raises the event on the sampling thread; marshalling to the UI thread is the host's job, as with
  `PluginsChanged` and `ConfigChanged`. The engine stays free of WinUI.
- The hub takes a `TimeProvider`, so tests drive intervals, the linger and timeouts without sleeping.
- The hub implements `IReadingSource`; one small `IWidgetHost` wraps it and is attached to every widget where widgets
  are created (`WidgetRegistry.CreateWidget`), which the live host and `--snapshot` both use.

Each provider gets its own loop (`PeriodicTimer` on the thread pool). A loop awaits its own `SampleAsync`, which is
what "a tick is skipped while the previous sample runs" means in practice.

Alternative considered: one shared sampler thread for all providers. Rejected because one slow provider would delay
the rest.

### 6. Lifetime, linger and plugin reload

- A provider is instantiated on the first subscription to one of its readings or on a catalog request, never at load.
- The 5-second linger exists because a config reload, a resize and a theme change each rebuild the page, which
  unsubscribes and resubscribes everything within milliseconds. Without it every rebuild would restart every provider.
- The store keeps a reading's last value after its last unsubscribe. A resubscribed reading starts as pending with
  that value, so a card that returns shows the old number dimmed, not a dash, until the next sample.
- Plugin reload: `WidgetPluginLoader.ReloadPlugins` asks the hub to stop and release every provider and drop their
  store entries and catalogs before `UnloadAll`, with no linger. The page rebuild that follows `PluginsChanged`
  resubscribes against the new registry. Catalog entries and values are SDK types, but they are dropped anyway so
  that nothing allocated by an unloaded plugin stays reachable.

### 7. States come from events, not from clocks

Stale is "the last sample failed or timed out, or the provider skipped this reading three samples in a row", not "the
value is older than N seconds". This needs no watchdog timer, and it is correct for pushed providers, whose values
may legitimately not change for hours.

Failure handling per provider: catch, log with the provider id, mark its subscribed readings stale (or unavailable
when they have no value), and retry with a delay that doubles from the interval up to 60 seconds. A sample is given
`max(5 s, 5 × interval)` before its token is cancelled and it counts as failed. If it still does not return, the loop
stays parked on it: the hub never starts a second sample of a provider, so a hung provider costs one blocked thread
pool thread until the plugin is reloaded. That limit is documented for provider authors.

State changes are logged once per transition per reading (`Warn` for unavailable and stale, `Info` for recovery).

### 8. Sampling rate

The interval is `providers.<id>.intervalMs` when present, else the provider's `DefaultIntervalMs`, clamped up to its
`MinIntervalMs`. `UrDeckConfig` gains `Providers` (`Dictionary<string, ProviderSettings>`, where `ProviderSettings`
has `IntervalMs` and `[JsonExtensionData]`). The provider never sees the setting. On `ConfigChanged` the host passes
the section to the hub, which changes the period of running loops.

After `Start` the hub samples at once and again after 250 ms, then at the interval. The second early sample exists for
providers that compute a rate from two samples (CPU load): a card fills in after a quarter of a second, and
`--snapshot` does not have to wait a full interval.

### 9. Formatting, units and the dash

`ReadingFormatter` is pure SDK code, so community widgets format like first-party ones.

| Kind | Canonical value | Shown as |
|---|---|---|
| Percent | 0 to 100 | `37` with a raised `%` |
| Temperature | degrees Celsius | `41` with a raised `°`, converted when the display unit is Fahrenheit |
| Number | as published | the number with the descriptor's `Unit` on the baseline |
| Text | text | as it is |
| OnOff | boolean | `On` or `Off` |

- Decimals: the widget's option, else the descriptor's `Decimals`, else 0.
- `WidestValue` comes from the descriptor's `Max` (or `Min`, whichever is longer as text) at the chosen decimals, and
  is the value itself when there is no range. This is what keeps the readout's size stable.
- Display unit: the widget's option, else the descriptor's `DisplayUnit`, else the region
  (`IReadingSource.RegionUsesFahrenheit`, which the engine derives from `RegionInfo.CurrentRegion.IsMetric`). No
  reading in this change is a temperature; the rule is implemented and unit-tested now because `DisplayUnit` is part
  of the catalog entry, which is public API.
- The dash is one SDK constant (an en dash). "Not current" is drawn in `Theme.TextMuted` by a `Readout.Draw` overload
  that takes a `ReadingText`, so no theme format change is needed and no widget picks the colour.

Alternative considered: the provider formats (publishes `"37 %"`). Rejected: the unit preference, the stable width
and a later gauge all need the number.

Alternative considered: one global temperature unit. Rejected by the owner: hardware temperatures are read in Celsius
even where weather is read in Fahrenheit, so the default has to come from the reading.

### 10. The system provider

`providers/UrDeck.Providers.System` (new top-level folder, GPL, references only `UrDeck.Sdk`). The host's build step
that copies the Clock into `plugins/` becomes a list of first-party plugin projects.

- CPU load: `NtQuerySystemInformation(SystemProcessorPerformanceInformation)`, which returns idle, kernel and user
  times for every logical processor in one call with no handle and no allocation beyond a reused buffer. Load is
  `1 - Δidle / Δ(kernel + user)` between two samples; the first sample only sets the baseline and publishes nothing.
- Memory load: `GlobalMemoryStatusEx` (`dwMemoryLoad`).
- The CPU name for `Device`: the `ProcessorNameString` registry value, read once at `Start`.
- "Core" means a logical processor, numbered from 1 in both the path and the label, matching how the owner describes
  the cards ("Core 1" to "Core 4").
- `SetDemand` records which of the two groups (CPU, memory) are wanted; `SampleAsync` skips a group nobody wants.

Alternative considered: `System.Diagnostics.PerformanceCounter` or PDH (`% Processor Utility`, the number Task Manager
shows). They load the performance counter infrastructure, cost more memory and a first-call delay. The time-based
load can read lower than Task Manager on CPUs that change frequency; task 8.3 compares the two on the owner's machine
and the choice is revisited only if the difference is visible there.

**Outcome of task 8.3 (2026-10-05, decided by the owner): keep the time-based load.** Measured on the owner's machine
(a CPU that boosts) with half the logical processors fully busy: `system:cpu/load` read 53%, matching `% Processor Time`,
while `% Processor Utility` (the number Task Manager has shown since Windows 8.1) read 84%. At idle they were within a few
points. The cause is that utility weighs each core's busy time by its frequency against the base frequency, so a boosting
core counts for more than 100% of base; the time-based figure is simply the share of time cores are busy. The cards
therefore read lower than Task Manager under load, by an amount that depends on how far the CPU boosts. To revisit:
replace the `NtQuerySystemInformation` sampling in `providers/UrDeck.Providers.System` with the PDH counter
`\Processor Information(_Total)\% Processor Utility` (and `(<group>,<n>)` per core), and measure its memory and
first-sample cost on the panel first; nothing outside the provider changes, because the ids and the catalog stay the same.
Details and the measurements are in `docs/perf/data-providers.md`.

This provider is the future facade for all hardware readings: ids such as `system:cpu/temperature` will be added to
its catalog by the elevated-sensors change and report unavailable until the helper is enabled, so a page config does
not change when the user opts in.

### 11. The stats widget

`widgets/UrDeck.Widgets.Stats`, type id `urdeck.widgets.stats`, `[RefreshOnData]`, sizes 1x1 and 2x2.

- `StatsConfig.Slots` is a list of `StatsSlot` (`Reading`, `Label`, `Unit`, `Decimals`, plus `[JsonExtensionData]`).
  `Label` is a nullable string, which gives the three settled states: `null` (use the catalog label), a text, or
  empty (hidden).
- There is no `style` property yet. Only one presentation exists; the next change adds `style` with `text` as the
  value a missing property means, so configs written now stay valid.
- `Subscriptions` is the reading of the first slot, or `system:cpu/load` when there is none.
- `Render` formats the reading and calls the readout overload from decision 9 in `ContentRect`. `NeedsRender`
  compares the `ReadingText` and the label with what was last drawn.
- The roadmap listed "single stat" and "performance" as two widgets and noted they may be one. They are one: a 1x1
  card is the one-slot composition of the same widget.

### 12. Host and snapshot

- `WidgetView` reads `Subscriptions` when it is created (the widget is already configured), subscribes on `Loaded`
  and unsubscribes on `Unloaded` and in `Dispose`, next to the existing timer start and stop. A widget with
  `isVisible` false gets no view today, so it holds no subscription.
- `MainWindow` owns the id-to-views map for the shown page, handles `ReadingsChanged` by enqueueing one callback on
  the dispatcher, and there calls the same guarded `NeedsRender` then `Invalidate` path the timer uses.
- `SnapshotCommand` subscribes the page's widgets, waits until no subscribed reading is pending or 2 seconds have
  passed, paints, and disposes the hub. With the early second sample (decision 8) the wait is about 250 ms for the
  system provider and zero for a page without data widgets.

### 13. Licence boundary

Providers reference only `UrDeck.Sdk`, like widgets. `PLUGIN-EXCEPTION.md` names the provider types in its definition
of the SDK Interface and says "widgets and data providers" where it says "widgets". The README licence map and the
`AGENTS.md` layout gain the `providers/` folder (GPL).

## Risks / Trade-offs

- **A reading that is really a structure is awkward as several readings** (now playing with art and position) →
  Accepted until a real consumer exists; a provider and widgets in one assembly can share types today, and typed
  topics remain possible as an addition.
- **A hung provider holds a thread pool thread until reload** → Its readings go stale, the log names it, and nothing
  else is affected. Real containment comes with the out-of-process helper.
- **Time-based CPU load differs from Task Manager's utility number** → Measured in task 8.3; the alternative is named
  in decision 10.
- **The linger keeps a provider sampling for 5 seconds after its last card is gone** → Five samples of a cheap call;
  it removes a restart on every page rebuild.
- **`ReadingKind` and `DisplayUnit` will grow** (power, frequency, data size) → Adding enum members and formatter rows
  is not breaking; a widget shows an unknown kind as a plain number.
- **The contract is frozen with one provider that never fails, pushes or needs settings** → The engine tests use fake
  providers for push, failure, timeout and unavailable readings. The SDK is unpublished, so the weather and helper
  changes can still adjust it.
- **A reading change repaints a full card** (a 1x1 card each second per changed core) → `NeedsRender` skips unchanged
  text; the cost of four core cards is measured in task 8.4 and recorded.
- **Snapshot output now depends on the machine's live values** → Tests paint widgets against a fake reading source,
  not through `--snapshot`.

## Migration Plan

One pull request from `feat/data-providers`. There is no data migration: an existing `urdeck-config.json` has no
`providers` section and no stats widget, and behaves as before. Third-party plugins do not exist yet; any built
against SDK 0.2.0.0 must be rebuilt. Rollback is reverting the squash commit.

## Open Questions

- The exact stale wording and log level for each transition: settled while implementing, within decision 7.
- Whether the en dash is present in every bundled theme font; if one lacks it, the constant becomes a hyphen-minus.
