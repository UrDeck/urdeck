## Context

Providers publish through `IReadingSink`; the hub (`ReadingHub`) owns lifetimes, demand (`SetDemand`), the catalog with
`{name}` patterns (weather's `{location}`), and reading states. Stale today comes only from a failed or skipped sample
(`ReadingHub.Sampling.cs`, the failed-sample branch and the three-miss rule); `Unavailable` drops the last value. A pushed
provider (media) leaves `SampleAsync` empty, and an empty sample is not a miss. Actions (`provider-actions`) are queued
per provider; the action catalog is only for listing, and the provider validates paths. `provider-settings-and-secrets`
(merged before this) hands a provider typed settings and secrets.

Measured on the owner's Home Assistant 2026.10.3 (read-only, see the handoff): `subscribe_entities` with `entity_ids`
returns the full state of exactly those entities first and diffs afterwards; an unknown id is silently omitted; the whole
house sends ~16 changes per second (a presence radar ~3/s), the unfiltered first event is 310 KiB; REST `POST
/api/services/...` answered a bad request in 21 ms cold; `ping` answers `pong`; 709 of 1,234 entities carry an `mdi:`
icon; temperatures come in °F and one in °C on the same installation.

## Goals / Non-Goals

**Goals:** readings for the entities on screen with no polling and no idle cost; honest states through outages; units
right whatever Home Assistant's unit system; any service callable; a contract other pushed providers reuse.

**Non-Goals:** several Home Assistant instances; OAuth; history or statistics; events and triggers as readings; camera
images (#120); calendars; a widget.

## Decisions

1. **Readings over the WebSocket, actions over REST.** Readings need push and a subscription; actions are one request
   each and must work while the socket is reconnecting or closed. One `HttpClient` for the provider's life (pooled, so a
   tap after the first costs a few milliseconds on a LAN). Rejected: `call_service` over the socket (a tap would depend on
   the socket's state and need an "open it for this" path).

2. **The subscription is the demand.** `SetDemand` maps paths to entity ids (an attribute path demands its entity) and
   re-subscribes with `entity_ids` after a 250 ms debounce, so a page flip that swaps ten tiles costs one re-subscribe.
   The first event of each subscription carries the full state, so start, a demand change and a reconnect are one code
   path with no REST query. Rejected: one unfiltered subscription filtered locally (wakes the deck 16 times a second for
   readings nobody shows).

3. **Connection life.** States: not configured → idle (no socket) → connecting → subscribed → backoff → connecting; and
   rejected (`auth_invalid`, no retries until the settings change, which restarts the provider anyway). The socket opens
   on the first non-empty demand and closes after 30 s of empty demand. Ping every 30 s; no pong within 10 s means the
   connection is dead (a socket half-open after sleep otherwise looks alive for minutes). Backoff 1, 2, 5, 10, 30, 60 s
   (cap), ±20 % jitter, reset after a successful `auth_ok`. On loss: `Interrupted(reason)`, then backoff.
   Time comes from an injected `TimeProvider`.

4. **`IReadingSink.Interrupted(reason)`.** The hub applies the failed-sample rule to every wanted path of the provider:
   stale with the last value, unavailable if none; the reason is logged once per change of state. The next publish of a
   path makes it ok. Default member, so existing sinks compile. Rejected: a per-path `Stale(path)` (a provider would have
   to track what the hub already knows).

5. **Descriptions refined while running.** `IReadingSink.Describe(path, descriptor)` for a concrete path matched by one of
   the provider's patterns. The hub keeps it for that path while the path is wanted (bounded: a provider cannot grow the
   cache with paths nobody asked for, as for publishes today), returns it from `Describe(id)` instead of the pattern's
   substituted description, and marks the readings' widgets for repaint when it changes. Describing again replaces it.
   The static catalog keeps `{entity}` as an honest placeholder (kind text: "an entity's state"). This is BACKLOG item 3's
   "catalog that changes while running", limited to concrete paths under declared patterns.

6. **Kind mapping, per entity, from its own unit.** The entity's `unit_of_measurement` is authoritative; the system unit
   setting is only a default and is not read for this. The table is in the provider-homeassistant spec. Conversions to
   canonical units happen in the provider (°F → °C, mph and m/s → km/h); the formatter converts back for the region.
   Percent readings get 0–100; temperatures get no range unless the entity has `min`/`max`. `friendly_name` is the label,
   the entity id the full name, the device left empty. Numbers parse invariantly; a state that does not parse for its kind
   is unavailable with the state text in the reason.

7. **States and reasons.** `unavailable` → unavailable ("unavailable in Home Assistant"); `unknown` → pending, keeping a
   last value if any (an HA restart may briefly show this; accepted); missing from the first event or removed (`r`) →
   unavailable with "no entity '<id>' in Home Assistant" plus "did you mean …?" for up to three ids at the smallest edit
   distance (from one `get_states` over the socket, cached until reconnect). Attributes: `<entity>/<attribute>`;
   `brightness` (0–255) is published as percent; other numeric attributes as numbers, text as text; a missing attribute
   is unavailable.

8. **Actions.** Path `<entity_id>/<service>`; domain from the entity id; body `{"entity_id": …}` plus the argument's
   field. `toggle` alias: scene, script → `turn_on`; button, input_button → `press`; media_player → `media_play_pause`;
   lock → `lock` when unlocked and `unlock` when locked (known from the last state, failing if unknown); everything else →
   `toggle`. Argument fields in the spec. Every service is allowed (the configuration is trusted like a shortcut target;
   page templates show actions before import). REST status → result: 200 done; 400 failed "Home Assistant refused
   <domain>.<service> on <entity> (unknown service, or the entity does not support it)"; 401 failed "token rejected";
   timeout or network failure → failed with the cause. `climate/set_temperature` converts the canonical °C argument to the
   installation's temperature unit, read once per connection with `get_config`.

9. **Natural action and icon in the description.** `ReadingDescriptor.Action` is a path under the same provider; the HA
   provider sets `<entity>/toggle` for the domains in the alias table, `<entity>/turn_on` for a light's `brightness`
   attribute (a level control's action), and none for sensors. `ReadingDescriptor.Icon` is a
   canonical icon key (`home/…`, `media/…`); the mapping is domain + device_class, then well-known `mdi:` names, then the
   domain's key, then `home/entity`. The keys' files arrive with `control-tile`; until then they resolve to nothing, which
   no current widget asks for.

10. **`IReadingBrowser`.** `BrowseAsync` returns concrete descriptions with no values (values go through the normal
    subscription). The hub exposes it as `Browse(providerId)`, starting the provider for the call as an action does. The
    CLI's `readings browse` uses the engine's public API, the user's configuration (or `--url`) and the secret store, and
    prints the current value by subscribing briefly. The editor will use the same.

11. **Seams and tests.** Internal `IHomeAssistantSocket` (connect, send, receive JSON messages, close) and
    `IHomeAssistantRest` (post a service call, get), each with a thin real implementation over `ClientWebSocket` /
    `HttpClient`, and scripted fakes in the tests. Fixtures are hand-written in the captured message shapes with fictional
    entities. `[HomeAssistantFact]` (skipped unless `URDECK_HA_E2E=1`, `URDECK_HA_URL` and the token's environment
    variable are set) runs a few end-to-end checks locally; `scripts/Start-HomeAssistantDemo.ps1` starts
    `homeassistant/home-assistant` in Docker with the `demo` integration, onboards a user and prints a token. Neither runs
    in CI.

## Risks / Trade-offs

- [A chatty entity on screen (a radar at 3/s) repaints its tile 3 times a second] → acceptable; the hub delivers changes
  per frame, and the frame clock bounds repaints.
- [`unknown` after an HA restart flashes tiles] → accepted by the owner; restarts are rare.
- [Re-description races a paint] → descriptions are immutable records swapped atomically, like readings.
- [REST errors are terse] → the provider's messages add the domain, service and entity.
- [Every service allowed, including locks] → the user writes the configuration; page templates will show actions before
  import (BACKLOG item 15).
- [Home Assistant changes its WebSocket messages] → fixtures pin today's shapes; the opt-in end-to-end tests catch drift.
