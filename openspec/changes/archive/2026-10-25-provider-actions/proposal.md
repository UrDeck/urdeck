## Why

Readings flow one way: a provider publishes, the hub carries it, a widget draws it. A widget that must make something
happen in a provider (skip a track, toggle a Home Assistant switch, mute a microphone) has no path to do so, and the
widgets that need one are next: now playing (`now-playing`), the Home Assistant toggle tile and audio. This is GitHub
issue #108, and it is SDK groundwork that is cheaper to settle once, before the first consumer fixes its shape.

## What Changes

- **An action path from widget to provider.** A provider may opt in, beside `IDataProvider`, to being asked to do
  something: it lists its actions and performs one on request. A widget asks through a new host service and gets a
  result (done, or failed with a message). Providers that do not opt in are untouched.
- **Actions are addressed like readings.** An action id is `provider:path` (`media:control/next`,
  `homeassistant:switch.desk_lamp/toggle`), so a widget never names a provider type and the existing id parsing is
  reused. It may carry one optional value as its argument (a brightness, a volume).
- **Off the UI thread, ordered, bounded.** The tap that asks returns at once. The hub runs the action on the thread
  pool, one at a time per provider and in the order asked, with a time limit and a short queue, so a slow or gone
  provider never blocks a tap and "play" then "pause" cannot overtake each other.
- **A result that is also a log line.** Every action ends as done or failed with a reason, and the host logs one line for
  each. A widget that wants to react to the result may await it; most will just watch the next reading.
- **Failures are values.** An unknown provider, a provider that is not running, a provider that offers no actions, a
  timeout, a full queue, a throwing provider and a cancelled action all end as a failed result. None of them throws into
  a widget or affects another provider.
- **A catalog of actions** the provider lists, readable through the hub, for the editor (item 11) and for documentation.

Deliberately not in this change: optimistic state in the SDK (a widget that wants it keeps its own; see `design.md`);
folding `Launcher` into actions (it must run on the UI thread so that Windows lets the launched application come to the
front; it stays as it is); provider settings and secrets (the Home Assistant change brings them); a progress or
cancellation handle for the widget; actions that start a stopped provider; any widget or provider that uses actions
(the first is `now-playing`).

## Capabilities

### New Capabilities

- `provider-actions`: how a provider offers actions, how an action is addressed and invoked, where and in what order it
  runs, the limits, the result, and what the host logs.

### Modified Capabilities

- `widget-sdk`: adds the action service to the host services a widget receives.

## Impact

- `sdk/UrDeck.Sdk`: `IActionProvider`, `ActionDescriptor` and `ActionResult` (namespace `UrDeck.Sdk.Data`), `IActionInvoker`
  with a null implementation, and `IWidgetHost.Actions` as a default member. All additive, so `AssemblyVersion` stays and
  the package version moves one minor (note in the csproj comment). `UrDeck.Analyzer` is not touched.
- `src/UrDeck.Engine`: `ReadingHub` implements the invoker over its provider runtimes (per-provider queue, time limit,
  log lines); `WidgetServices` hands it to the widget; `SampleProvider` offers no actions, so `--demo` performs none.
- `src/UrDeck.Preview`: its host takes the default (no actions); nothing else changes.
- `src/UrDeck.Host`: nothing, apart from passing the hub through the existing services wiring.
- Concurrency: a provider's `InvokeAsync` can run while its `SampleAsync` runs. The contract says so, and the doc comment
  tells provider authors to guard shared state.
- Network and disk: none. Security: an action is performed by a provider the user installed, on behalf of a widget the
  user placed; an imported page must not bring a widget that acts on a provider silently (noted for item 15, like launch
  targets).
- Tests: the hub with a fake provider (done, failed, throws, hangs, slow behind another, queue full, stopped, unknown,
  no actions, plugin reload), the widget side with fake services.
- Docs: `CONTRIBUTING.md` (actions for provider authors), the site's author documentation if a page for providers
  exists then, `docs/BACKLOG.md` (items 3 and 8) and `docs/ROADMAP.md`. Closes #108.
