## Context

Readings go provider → hub → widget (`data-providers`). Taps go host → widget (`ITapTarget`, UI thread, must return
quickly). Nothing goes from a widget to a provider. GitHub issue #108 asks for that path and names the consumers that wait
for it: the Home Assistant toggle tile, now playing and audio. The hub already owns each provider's lifetime (created on
first demand, stopped after a linger), runs one sampling loop per provider on the thread pool, and parses ids with
`ReadingId`. Widgets receive a per-widget `WidgetServices` (an `IWidgetHost`) with `Readings`, `Launcher`, `Icons` and
`Log`; new members arrive as default interface members so older hosts and doubles keep compiling.

## Goals / Non-Goals

**Goals:** a widget can ask a provider to do something and learn the outcome; a tap never waits for a provider; ordering
is predictable; every failure is a value and a log line; additive SDK surface.

**Non-Goals:** optimistic state, a cancellation or progress handle for widgets, starting a stopped provider, provider
settings and secrets, folding `Launcher` in, any consumer (`now-playing` is the first).

## Decisions

1. **Opt-in interface, `IActionProvider`, beside `IDataProvider`.** Mirrors `ITapTarget`: implementing it is the
   declaration. The alternative, adding members to `IDataProvider` with default bodies, would put actions into every
   provider's mental model and into the analyzer's checks. Weather and System stay unchanged.

2. **One string id, `provider:path`, with the verb inside the path.** Issue #108 writes the address as a reading id "with"
   a verb (`homeassistant:switch.desk_lamp` with `toggle`). Folding the verb into the path
   (`homeassistant:switch.desk_lamp/toggle`) keeps one parser (`ReadingId`), one config shape (a widget setting is a
   string) and lets a provider decide its own grammar. Rejected: a two-part address (id plus verb), which doubles every
   setting and every log line for no gain. A reading and an action may share the front of a path
   (`switch.desk_lamp` reads, `switch.desk_lamp/toggle` acts); that is the provider's convention, not the engine's.

3. **One optional argument of type `ReadingValue`.** It reuses a type widgets and providers already share, and covers
   brightness, volume and "set to on". Rejected: a dictionary of named arguments (a new schema to describe, validate and
   edit, with no consumer yet). A provider that needs two values makes two actions or encodes one in the path. If a
   consumer proves this wrong, an overload is additive.

4. **`Task<ActionResult>` on the widget side, never faulted.** `OnTap` calls it and ignores the task; a widget that wants
   the outcome continues from it on a worker thread. Rejected: a callback parameter (needs a thread story in every
   signature), and `bool` like `Launcher.Launch` (no message, and an action is asynchronous). The task's continuation is
   not on the UI thread; a widget that wants a repaint relies on the next reading, which already marks it dirty.

5. **Per-provider ordered queue, bound of 8, limit of 10 s, independent of the sampling loop.** Order matters for media
   (play then pause) and for toggles. Running actions beside `SampleAsync` (instead of inside the loop) keeps a slow
   sample from delaying a tap and the reverse; the cost is that a provider guards shared state, which the contract says.
   The bound keeps a hung provider from accumulating work, and the busy failure is visible in the log. On a timeout the
   token is cancelled and the hub moves on without waiting for a provider that ignores it, consistent with the sample
   rule ("a sample that never returns holds one thread").
   Rejected: running actions concurrently (reordering), and serialising through the sampling loop (a 60 s backoff would
   delay a tap).

6. **No auto-start.** A provider exists only while something is subscribed (the hub's demand model). An action for a
   stopped provider fails with "not running". A widget that acts on a provider already subscribes to its readings (a
   media widget reads the title; a toggle reads the switch state), so the provider is running whenever the widget is
   shown. Starting a provider for a tap would add a start latency inside a user-visible gesture and a second lifetime
   rule. If a consumer needs it, it is additive.

7. **Validation is the provider's job, the catalog is for discovery.** The hub checks the id parses, the provider is
   loaded and running and implements `IActionProvider`; the provider decides whether the path is one of its actions and
   returns failed with a message when it is not. `DescribeActions()` feeds the editor (item 11) and documentation. This
   avoids a second pattern-matching implementation next to the reading catalog's parameter segments. Revisit if an
   editor needs to reject ids before saving.

8. **Optimistic state stays out of the SDK.** The issue asks whether a widget shows the expected state while the action
   runs or waits for the reading. The answer differs by consumer: media events arrive within tens of milliseconds, so
   waiting is right; a Home Assistant toggle over a network may want to show the new state at once. A widget can do that
   with the result task and its own field; the SDK would add a rule every provider and widget must follow, with no
   consumer to test it against.

9. **`Launcher` stays separate.** It runs on the UI thread so that Windows allows the launched application to take the
   foreground; actions run on the thread pool. Different thread, different result (an immediate bool), different owner
   (the host, not a plugin). Addressing could be unified later if a use case appears.

10. **Demo and sample providers perform nothing.** `SampleProvider` does not implement `IActionProvider`, so `--demo`
    and `preview render` fail every action with "offers no actions". A pretend-success would hide a widget that depends
    on a result.

## Risks / Trade-offs

- A provider author can forget that `InvokeAsync` and `SampleAsync` overlap. The doc comment says so; the new
  `CONTRIBUTING.md` section repeats it.
- A tap on a media widget while its provider is lingering down fails silently from the user's side (a log line).
  Mitigated by decision 6: a shown widget keeps its provider running. A page that is not shown has no taps.
- Ordering is per provider, not global: two providers' actions may complete in any order. Intended.
- A hung provider ignoring cancellation keeps one thread-pool thread until the plugin is reloaded, the same as a hung
  sample.

## Open Questions

- Should `ActionDescriptor` carry the argument's kind (number, text, on/off) so an editor can offer the right input?
  The proposal lists only "takes an argument"; decide with the editor (item 11) or leave for the first consumer that
  takes one.
