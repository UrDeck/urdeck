## Why

A widget can only repaint on its declared refresh timer, and that timer also drives its data refresh, so nothing on
the page can move smoothly. Motion is a design target (roadmap item 14), and the GPU-composited host it needed has
landed (`render-path`), so the smallest useful animation mechanism can be added now, before more widgets are built.

## What Changes

- **SDK:** a widget can report that it is in the middle of an animation through one optional member, `IsAnimating`
  (default `false`). Existing widgets need no change and keep working unrebuilt; the SDK assembly version stays
  0.2.0.0 because the addition is not breaking.
- **Host:** one shared frame timer at 30 frames per second. It runs only while at least one placed widget reports
  `IsAnimating`, repaints only those widgets, and stops when none animate. A page with nothing moving still costs
  nothing.
- **Data refresh is untouched:** the refresh attributes, `UpdateAsync` and `NeedsRender` keep their own cadence. A
  frame never calls `UpdateAsync`.
- **Clock:** a new `style` setting. `simple` (the default) is today's look; `flap` is a split-flap display whose
  changed digits flip over about half a second when the minute changes. It is the first consumer of the mechanism.
- The requirement that the host never runs a render loop is reworded: a frame source exists only while a widget is
  animating.

Deliberately not in this change (each waits for a widget or a measurement that needs it): a wall-clock wake request,
a monotonic or pinnable animation time, a transition helper, a user motion level, backing off when the PC is busy,
per-widget cost tracking, smaller or GPU-backed surfaces, and Lottie. The continuous-loop case is proven by the
weather widget, not here.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `widget-sdk`: adds the `IsAnimating` member to the widget contract.
- `host-shell`: adds animation frames; "GPU Composition" and "Widget Refresh Scheduling" no longer forbid a frame
  source outright, only one that runs while nothing animates.
- `widget-clock`: adds the `style` setting and the split-flap style; the readout and "no background of its own" rules
  now apply to the `simple` style, and the flap tiles take their look from existing theme values.

## Impact

- `sdk/UrDeck.Sdk`: `IWidget`, `Widget<TConfig>`; package `Version` moves to 0.3.0, `AssemblyVersion` stays.
- `src/UrDeck.Host`: `WidgetView`, `MainWindow`, and a small frame clock.
- `widgets/UrDeck.Widgets.Clock`: `ClockConfig`, `ClockWidget`, and the flap drawing.
- `src/UrDeck.Engine`: none expected. `--snapshot` and `PageRenderer` are unchanged (a widget's first paint is at
  rest).
- Tests: `SdkContractTests`, `ClockWidgetTests`.
- Docs: `docs/ROADMAP.md` (current state, item 14), `README.md`/`CONTRIBUTING.md` where widget authoring is described,
  a perf note for the measured cost of a flip.
- No theme format change, no new dependency.
