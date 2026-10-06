## 1. SDK: readings and the provider contract

- [x] 1.1 Add the reading types in `sdk/UrDeck.Sdk/Data` (`ReadingValue`, `ReadingKind`, `ReadingState`, `Reading`, `ReadingDescriptor`, `DisplayUnit`) per design decision 3, with doc comments
- [x] 1.2 Add `[DataProvider]`, `IDataProvider` and `IReadingSink`
- [x] 1.3 Add `IReadingSource` and `IWidgetHost`, with a null-object reading source that reports every reading as unavailable
- [x] 1.4 Add `Attach` and `Subscriptions` to `IWidget` as default interface members; store the host in `Widget<TConfig>` and expose `protected IReadingSource Readings`
- [x] 1.5 Add `ReadingFormatter`, `ReadingFormatOptions` and `ReadingText`: the kind table, decimals, widest value, the display unit order and the dash constant (design decision 9)
- [x] 1.6 Add the `Readout.Draw` and `Readout.Measure` overloads that take a `ReadingText` and draw a not-current value in `Theme.TextMuted`
- [x] 1.7 Replace `RefreshOnEventAttribute` with `RefreshOnDataAttribute`
- [x] 1.8 Set `AssemblyVersion` to 0.3.0.0 and `Version` to 0.4.0, and record the reason in the csproj comment

## 2. Analyzer

- [x] 2.1 Make URDECK003 and URDECK004 look for `RefreshOnDataAttribute` and name it in their messages
- [x] 2.2 Update `WidgetAnalyzerTests` for the attribute swap

## 3. Engine: loader, registry and configuration

- [x] 3.1 Add a provider descriptor and a provider registry (id, name, type, default and minimum interval), with validation and reject reasons like `WidgetDescriptor.TryCreate`
- [x] 3.2 Extend `WidgetPluginLoader.LoadAssembly` to register providers, reject a duplicate provider id with a warning naming both assemblies, detect a provider built against a foreign SDK, and report "no widgets or providers" for an assembly with neither
- [x] 3.3 Rename `RefreshStrategy.OnEvent` to `OnData` and remove `WidgetDescriptor.RefreshEvent`
- [x] 3.4 Add `Providers` to `UrDeckConfig` (`intervalMs` plus preserved unknown properties); it is omitted from a default configuration
- [x] 3.5 Attach an `IWidgetHost` in `WidgetRegistry.CreateWidget` before `Configure`

## 4. Engine: reading hub

- [x] 4.1 Add `ReadingHub` in `src/UrDeck.Engine/Data`: the store, `IReadingSource`, ref-counted `Subscribe`/`Unsubscribe`, id parsing, and unavailable readings for a malformed id, an unknown provider and an unknown path
- [x] 4.2 Create a provider on first subscription or catalog request, cache its catalog, call `Start` and `SetDemand`, and update the demand when the subscribed set changes
- [x] 4.3 Run one sampling loop per provider on the thread pool using an injected `TimeProvider`: sample at once, after 250 ms, then per interval; never overlap samples of one provider
- [x] 4.4 Commit everything published during a sample as one batch and raise `ReadingsChanged` once with the ids whose value or state changed; commit publications outside a sample on their own
- [x] 4.5 Implement the states of design decision 7: pending on subscribe (keeping a last value), stale after a failed sample or three samples without a report, unavailable with a reason, and one log line per transition
- [x] 4.6 Implement fault containment: catch and log, the sample timeout with cancellation, retry with a doubling delay up to 60 seconds, and never a second concurrent sample
- [x] 4.7 Stop a provider 5 seconds after its last unsubscribe unless it is subscribed to again; keep its last values
- [x] 4.8 Apply `providers.<id>.intervalMs` with the clamp to the provider's minimum and a log line, and change running loops when the settings change
- [x] 4.9 Stop and release every provider and drop its catalog and store entries on plugin reload before the contexts are unloaded, and on dispose

## 5. System provider

- [x] 5.1 Create `providers/UrDeck.Providers.System` (GPL header, references only `UrDeck.Sdk`), add it to `urdeck.slnx`, and turn the host's Clock-only copy step into a list of first-party plugin projects
- [x] 5.2 Implement the catalog: `cpu/load`, one `cpu/core/<n>/load` per logical processor, `memory/load`, with labels, names, the 0 to 100 range and the CPU name as device
- [x] 5.3 Implement CPU sampling with `NtQuerySystemInformation` and a reused buffer; the first sample sets the baseline and publishes nothing
- [x] 5.4 Implement memory sampling with `GlobalMemoryStatusEx`
- [x] 5.5 Honour demand: skip the CPU query when no CPU reading is wanted and the memory query when `memory/load` is not

## 6. Stats widget and host

- [x] 6.1 Create `widgets/UrDeck.Widgets.Stats` with `StatsConfig`, `StatsSlot` and `StatsWidget` (`urdeck.widgets.stats`, `[RefreshOnData]`, 1x1 and 2x2), add it to `urdeck.slnx` and to the host's plugin copy list
- [x] 6.2 Implement `Subscriptions` (first slot, else `system:cpu/load`), `Render` through the formatter and the readout overload, the three label states, and `NeedsRender` comparing what was last drawn
- [x] 6.3 In `WidgetView`, read `Subscriptions`, subscribe on `Loaded`, and unsubscribe on `Unloaded` and in `Dispose`; create no timer for `RefreshOnData`
- [x] 6.4 In `MainWindow`, create the hub, pass provider settings to it on start and on config reload, keep the id-to-views map, and handle `ReadingsChanged` with one dispatcher callback that runs the guarded `NeedsRender` then `Invalidate` path
- [x] 6.5 In `SnapshotCommand`, subscribe the page's widgets, wait until no reading is pending or 2 seconds have passed, paint, and dispose the hub

## 7. Tests

- [x] 7.1 `SdkContractTests`: the new frozen assembly version, `Subscriptions` empty and `Attach` a no-op by default, an unattached widget reading unavailable
- [x] 7.2 Formatter tests: each kind, rounding, decimals, widest value, the dash, not-current values, and the three levels of the display unit order
- [x] 7.3 Component tests: the readout overload keeps its text size across current, stale and dash, and uses the muted colour when not current
- [x] 7.4 Loader tests: a provider is registered, an assembly with a provider and a widget registers both, a duplicate id is rejected, and the "not a plugin" warning
- [x] 7.5 Hub tests with fake providers and a fake `TimeProvider`: no provider instance without a subscription; start on first subscription; demand updates; the two early samples; batching into one change notification; two subscribers sharing a reading
- [x] 7.6 Hub tests: linger and restart avoidance; last value kept and pending on resubscribe; interval default, override, clamp and live change
- [x] 7.7 Hub tests: a throwing provider, a provider that times out, backoff and recovery, a provider that reports unavailable, the stale rule, and release on plugin reload (the plugin context is collected)
- [x] 7.8 Stats widget tests against a fake reading source: the default reading, a per-core slot, the three label states, the dash, the muted stale value, `NeedsRender` after an unchanged rounded value, and extra slots surviving a config round trip
- [x] 7.9 Config tests: the `providers` section round-trips with unknown properties and is absent by default
- [x] 7.10 System provider tests: the catalog lists one core entry per logical processor, values are within 0 to 100 after two samples, an out-of-range core is unavailable, and an unwanted group is not queried

## 8. Verify

- [x] 8.1 `dotnet build urdeck.slnx -c Release` with 0 warnings, `dotnet test urdeck.slnx -c Release`, the engine coverage floor, `dotnet format urdeck.slnx --severity warn`, and `openspec validate --all --strict`
- [x] 8.2 `UrDeck.Host.exe --snapshot` of a page with a Clock and four 1x1 core cards: values, not dashes, and labels `Core 1` to `Core 4`; a page with only a Clock is not delayed
- [x] 8.3 On the owner's machine, compare `system:cpu/load` with Task Manager under idle and under load, and record the difference; revisit design decision 10 only if it is visibly off
- [x] 8.4 On the panel: a page with only a Clock shows no provider in `urdeck.log` and the baseline memory and 0% CPU of `docs/perf/render-host-baseline.md`; with four core cards and a memory card, record private bytes and CPU in `docs/perf/data-providers.md`
- [x] 8.5 On the panel: removing the last stats widget from the config logs the provider stopping about 5 seconds later; setting `providers.system.intervalMs` to 500 changes the rate without a restart; a value below the minimum is logged and clamped
- [x] 8.6 On the panel: replacing the system provider DLL and the stats widget DLL while running reloads cleanly (old plugin contexts are collected, cards come back with values)
- [x] 8.7 Owner looks at the panel in the dark and the glass theme: the cards read well at 1x1 and 2x2, and a card pointing at a reading that does not exist shows the dash acceptably

## 9. Documentation

- [x] 9.1 Update `PLUGIN-EXCEPTION.md` to name data providers and their SDK types
- [x] 9.2 Update `README.md` (layout, licence map, the stats widget and its `slots`, the `providers` section) and the layout in `AGENTS.md`
- [x] 9.3 Update `CONTRIBUTING.md`: using readings in a widget (`Subscriptions`, `Readings`, the formatter, `[RefreshOnData]`), writing a provider (the contract, ids, catalog, demand, threading, the hang limit)
- [x] 9.4 Update `docs/ROADMAP.md`: "Current state", item 3's status, the widget table in item 7, and the parked items this change touched (the host-services hook is done in its minimal form)
