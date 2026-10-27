## Why

Home Assistant is the most requested source of readings for a desk display: a room's temperature, power use, a battery,
whether the lamp is on, and the switch to change it. It is also the first provider that needs what
`provider-settings-and-secrets` builds (an address and a token), and it exposes three gaps in the reading contract that
MQTT, push and Prometheus will hit next: a pushed provider cannot say its readings went stale, a catalog cannot learn an
entity's unit while running, and nobody can list which readings exist. This is GitHub issue #110; the decisions and the
measurements on a real 1,234-entity installation are in `docs/handoff/2026-10-26-home-assistant.md`.

## What Changes

- **A Home Assistant provider** (`homeassistant`). Readings `homeassistant:<entity_id>` and attribute sub-paths
  (`light.desk/brightness`). States arrive over Home Assistant's WebSocket, subscribed only for the entities on screen,
  so idle cost is nothing and a busy house does not wake the deck. It reconnects with a growing delay, keeps last values
  stale meanwhile, and stops retrying when the token is rejected.
- **Kinds and units from each entity.** Temperature, speed, precipitation, percentages, timestamps and on/off states map
  to UrDeck's kinds (converted to the canonical units, then shown in the Windows region's units, as weather is); any other
  unit passes through as text; the entity's friendly name is the label.
- **Actions over REST**: any service on an entity (`homeassistant:light.desk/turn_on`), `toggle` as each domain's natural
  tap (a scene turns on, a button presses), one argument mapped to the service's main field (brightness, value, option,
  position, volume).
- **The reading contract grows** (all additive): a pushed provider can report an interruption (its readings go stale and
  keep their values); a provider can describe a concrete reading while running (kind, unit, label, range), and describe
  it again; a description can name the reading's natural action and an icon key; a provider can list the concrete
  readings its patterns stand for.
- **`urdeck-cli readings browse <provider>`** lists those readings with their current values, for finding an entity id.
- **A typo is explained**: a missing entity's reason suggests the nearest ids.
- **Sample data** with fictional entities for previews, the site and `--demo`.

Not in this change: the control tile (`control-tile`); signing in with OAuth; more than one Home Assistant; icons that
change with the state; colours and colour temperature of lights (a second argument); an editor's reading picker; taps
that change sample values in `--demo` (samples stay a pure function of the moment, and demo performs no actions).

## Capabilities

### New Capabilities

- `provider-homeassistant`: the Home Assistant provider: settings, readings and their kinds, the connection's life,
  states and reasons, actions, listing, sample data and what it sends where.

### Modified Capabilities

- `data-providers`: interruptions mark readings stale; descriptions refined while running; a description's natural
  action and icon; listing a provider's concrete readings.
- `sample-data`: a sample description may carry an icon key and a natural action.
- `urdeck-cli`: `readings browse`.

## Impact

- `sdk/UrDeck.Sdk`: `IReadingSink.Interrupted(reason)` and `Describe(path, descriptor)` (default members),
  `ReadingDescriptor.Action` and `.Icon`, `IReadingBrowser`. Additive; one minor version.
- `src/UrDeck.Engine`: `ReadingHub` applies interruptions with the failed-sample rule, keeps refined descriptions per
  concrete path (bounded by what is subscribed), repaints on a re-description, and exposes browsing; `SampleFile` reads
  `icon` and `action`.
- `providers/UrDeck.Providers.HomeAssistant` (new, GPL, references only the SDK): the provider, its WebSocket and REST
  clients behind internal seams, the kind mapping, the icon mapping, `sample.json`. Added to the host's `UrDeckPlugin`
  list. Uses `System.Net.WebSockets.ClientWebSocket` and `HttpClient`; no new package.
- `tools/UrDeck.Cli`: `readings browse`; the catalog export lists the new provider.
- Network: only the configured Home Assistant address (one WebSocket while something is shown; one HTTPS request per
  action; one listing request on a missing entity or a browse). Nothing else, nothing to urdeck.app.
- Tests: scripted socket and REST fakes with a fake clock and fictional fixtures; nothing contacts a real Home Assistant
  in CI. Opt-in end-to-end tests and a script that starts a disposable Home Assistant in Docker.
- Site: a Home Assistant page (setup, finding an entity id, examples with the stats widget); the reference lists the
  provider's settings; the privacy page lists what it sends.
- Closes #110.
