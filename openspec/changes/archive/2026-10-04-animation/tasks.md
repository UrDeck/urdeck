## 1. SDK

- [x] 1.1 Add `bool IsAnimating => false` to `IWidget` (with a doc comment in the style of `NeedsRender`) and a virtual `IsAnimating` to `Widget<TConfig>`
- [x] 1.2 Set the SDK package `Version` to 0.3.0 and note the addition in the csproj comment; leave `AssemblyVersion` at 0.2.0.0
- [x] 1.3 Add an `IsAnimating_DefaultsToFalse` test to `SdkContractTests`; the frozen-version test stays unchanged and passes

## 2. Host frame clock

- [x] 2.1 Add `FrameClock` to `src/UrDeck.Host`: a set of views, one `DispatcherQueueTimer` at 1/30 s that is started on the first `Add` and stopped when the set becomes empty, a tick that invalidates a copy of the set, and one log line on start and on stop
- [x] 2.2 In `WidgetView`, read `IsAnimating` after each paint through a guarded helper (an exception is logged and counts as `false`) and add or remove the view from the frame clock; remove it in `Dispose`
- [x] 2.3 Create the frame clock in `MainWindow`, pass it to each `WidgetView`, and stop it when the window closes
- [x] 2.4 Confirm a frame tick never calls `UpdateAsync` or `NeedsRender`, and that the refresh timer path is otherwise unchanged

## 3. Clock: split-flap style

- [x] 3.1 Add `Style` to `ClockConfig` (`"simple"` default); treat any value other than `"flap"` as `simple`
- [x] 3.2 Branch `ClockWidget.Render` on the style, leaving the `simple` path as it is
- [x] 3.3 Add the flap layout: four equal tiles and a colon fitted to the content rectangle, scaled by `FontSize`, the date line below as today, a blank first tile and `AM`/`PM` as text in the 12-hour format, at all four supported sizes
- [x] 3.4 Draw a tile at rest from theme values only (fill, radius, hinge, digit), per design decision 6
- [x] 3.5 Add the flip state (last shown characters, characters flipped from, flip start time) and the rules of design decision 4, including rest on the first paint, after `OnConfigured`, and when the render time is outside the flip
- [x] 3.6 Draw a tile mid-flip (clipped halves, the scaled and shaded flap), per design decision 5
- [x] 3.7 Override `IsAnimating` to return whether a flip is in progress; it is always `false` in the `simple` style

## 4. Tests

- [x] 4.1 `ClockWidgetTests`: the flap style paints at rest on the first paint and `IsAnimating` is `false`
- [x] 4.2 `ClockWidgetTests`: painting in the next minute starts a flip (`IsAnimating` is `true`, the frame differs from the resting frame), and painting past the duration is at rest with `IsAnimating` `false`
- [x] 4.3 `ClockWidgetTests`: only changed tiles move (`10:41` to `10:42` leaves the first three tiles' pixels unchanged mid-flip), and a render time before the flip start draws rest
- [x] 4.4 `ClockWidgetTests`: the 12-hour format with a single-digit hour, an unknown style falling back to `simple`, and the `simple` style never reporting `IsAnimating`
- [x] 4.5 `ClockWidgetTests`: the flap style draws without clipping at each supported size

## 5. Verify

- [x] 5.1 `dotnet build urdeck.slnx -c Release` with 0 warnings, `dotnet test urdeck.slnx -c Release`, the engine coverage floor, `dotnet format urdeck.slnx --severity warn`, and `openspec validate --all --strict`
- [x] 5.2 `UrDeck.Host.exe --snapshot` with a flap-style Clock in the dark and the glass theme: tiles at rest, nothing clipped
- [x] 5.3 On the panel with a flap-style Clock: `urdeck.log` shows the frame clock starting at a minute change and stopping about half a second later, and never running in between
- [x] 5.4 On the panel: CPU and GPU are 0% between flips (the method of `docs/perf/render-host-baseline.md`), and the CPU of one flip is recorded in a short note under `docs/perf/`
- [x] 5.5 Owner looks at the panel in the dark and the glass theme: the flip reads as a flap and is smooth, and the tile opacity, shading and duration are tuned to the owner's choice
- [x] 5.6 A `simple`-style Clock looks and behaves as before, and replacing the Clock plugin while a flip is running reloads cleanly (the old plugin context is collected)

## 6. Documentation

- [x] 6.1 Document `IsAnimating` where widget authoring is described (`CONTRIBUTING.md`, and the README if it lists the widget contract), including "draw from `context.Time`" and "first paint at rest"
- [x] 6.2 Document the Clock's `style` setting where its configuration is described
- [x] 6.3 Update `docs/ROADMAP.md`: "Current state" (`render-path` merged as #11, the glass theme as #12, `animation` done, next is item 3), item 14's status, the Clock row in item 7, and the items this change parked (wake requests, animation time, transition helper, motion level, busy backoff, surfaces) as things the weather change revisits
