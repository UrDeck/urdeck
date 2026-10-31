# Handoff: Home Assistant (provider settings and secrets, the provider, the control tile)

**For:** fresh sessions that write the proposals for three changes, in order: (A) `provider-settings-and-secrets` (#109),
(B) `homeassistant-provider` (#110), (C) `control-tile` (#119). Run `/opsx:propose` for one change per session, starting
with A; each PR goes in before the next proposal. **Date:** 2026-10-26.
**Read first:** `openspec/specs/data-providers/spec.md`, `openspec/specs/provider-actions/spec.md`,
`openspec/specs/icon-sets/spec.md`, `sdk/UrDeck.Sdk/Data/` (the provider contract), `src/UrDeck.Engine/Data/ReadingHub*.cs`
and the weather provider (`providers/UrDeck.Providers.Weather`, the pattern catalog and the `IWeatherHttp` test seam).

Nothing here is built. It is what the exploration decided with the owner, so the proposals do not start from zero. The
owner agreed to every recommendation below; open questions are marked as such.

## Where this comes from

An `/opsx:explore` session on issues #110 (provider) and #119 (control tile), probed read-only against the owner's
Home Assistant 2026.10.3 (1,234 entities, imperial unit system). Numbers quoted below come from that instance. Never
commit its entity ids or names: fixtures use made-up entities.

Principles that shaped it: "we don't collect your data" (urdeck.app is a static site with no endpoints and must never
be part of a flow); fixtures over real services in CI (CI is already slow; a real Home Assistant runs only opt-in,
locally); no backwards compatibility before release (breaking SDK changes are fine).

## A. `provider-settings-and-secrets` (#109), its own PR

Today `ProviderSettings` (`src/UrDeck.Engine/Config/ConfigStore.cs`) keeps unknown keys "for the provider", but nothing
hands them over: `IDataProvider.Start(sink)` receives no settings. A provides the hand-off, mirroring `Widget<TConfig>`.

- **SDK.** `ProviderSettings` moves to the SDK (keeps `intervalMs`, gains `actionTimeoutMs`, default 10000, the current
  fixed limit, and `actionWarnMs`, default 2000: a slower action logs a warning). A provider declares a settings type
  (`DataProvider<TSettings>` or `Start(sink, settings)`; pick in the design). `[Secret]` marks a property of type
  `SecretValue`: it prints `•••`, has no JSON converter (never read from or written to the config) and gives its value
  only through an explicit `Reveal()`.
- **Store.** `ISecretStore` in the engine, keyed by (provider id, setting name). Read order: the environment variable
  `URDECK_SECRET_<PROVIDER>_<SETTING>` (upper case, `.` and `-` become `_`; only this direction is ever needed), then
  Windows Credential Manager, a generic credential with target `UrDeck/<provider>/<setting>` (e.g.
  `UrDeck/homeassistant/token`), `CRED_PERSIST_LOCAL_MACHINE`. P/Invoke `CredReadW`/`CredWriteW`/`CredDeleteW`/
  `CredEnumerateW`, `[SupportedOSPlatform("windows")]`. Linux, the CLI's sample paths and the preview read the
  environment only. A blob holds at most 2,560 bytes; a larger secret fails with a clear message.
- **Shared by every copy on the PC** (installed, portable, the dev build). The credential's `UserName` holds the URL
  the token was saved for, so a copy configured for another address logs "token was saved for <url>; set it again in
  Settings" instead of failing on a 401. Two copies against two instances use the environment override.
- **Scoping.** The engine fills only the `[Secret]` properties the provider's own settings type declares; providers
  never get the store. (A malicious in-process plugin could call `CredRead` itself; that is plugin trust, which is
  signing's and the marketplace's problem, and the privacy page says so honestly.)
- **Lifecycle.** A change to a provider's own settings (including its secret) restarts that provider only; an
  unrelated save restarts nothing. With required settings missing, the provider's readings are Unavailable with a
  pointer ("set up Home Assistant in Settings") and nothing connects.
- **Settings window.** A deliberately bare "Providers" section generated from the declarations (text, password, int,
  bool), the seed of the later config editor (item 11), not a one-off. An optional provider hook validates
  (`ValidateAsync(settings)` → "Connected · HA 2026.10.3 · 1,234 entities") for a "Test connection" button. The deck
  never shows a secret.
- **Uninstall** deletes every `UrDeck/*` credential (Velopack's uninstall hook, next to the Run value). The privacy page
  says where secrets live, that they go only to the service they belong to, and that uninstall removes them.
- **Log.** One line per secret at start, never the value: `Secret homeassistant/token: from Credential Manager` /
  `from URDECK_SECRET_HOMEASSISTANT_TOKEN` / `missing; set it in Settings`.
- **Docs.** Provider settings appear in the catalog export and the site's generated reference (the build already fails
  on an undocumented key).
- **Tests.** An in-memory store for the logic; a fake provider proves binding, secret fill and restart; one
  `[WindowsFact]` Credential Manager round trip under an injectable `UrDeckTest/` prefix, so the uninstall-cleanup test
  can never touch real `UrDeck/*` credentials.
- **Open (pick in the design):** a token typed into the JSON by hand. Recommended: move it into Credential Manager on
  the next start, rewrite the config without it, log the move (NuGet-style); never for a config whose `version` is newer
  than the host's, which is never written.

Later, not in A: "Sign in with Home Assistant" (OAuth). Home Assistant accepts a **loopback client id**
(`client_id=http://127.0.0.1:<port>/`, redirect on loopback, verified against the owner's instance: no page fetch, so
no urdeck.app). The refresh token goes into the same store, with the client id kept beside it. Backlog item.

## B. `homeassistant-provider` (#110)

One instance only (multiple instances are rare; not designed for). Provider id `homeassistant`; reading ids
`homeassistant:<entity_id>` (one segment, e.g. `sensor.office_temperature`), attributes as sub-paths
(`light.desk/brightness`, as Percent). Settings: `url`, `[Secret] token` (a long-lived access token).

**Readings over the WebSocket, driven by demand.** Verified on the instance: `subscribe_entities` with `entity_ids`
returns the full current state of exactly those entities in its first event (`a`), then diffs (`c`, removals `r`) on
the same subscription. So startup and reconnect are one path, with no REST query. An unknown id is silently left out.
Unfiltered, the house sends ~16 changes/s (a presence radar at ~3/s); filtered, only what is shown.

```
  no settings ─▶ NotConfigured (readings say how to set it up)
  demand = ∅ ─▶ Idle, no socket (closed after 30 s with no demand; a page flip back re-demands)
  demand ≠ ∅ ─▶ Connecting ─auth_invalid─▶ Rejected ("token rejected; fix it in Settings"), no retries until settings change
                  │ auth_ok
                  ▼
               Subscribed: initial 'a' + 'c' diffs → PublishBatch; SetDemand change → re-subscribe (250 ms debounce); ping every 30 s
                  │ close / no pong in 10 s
                  ▼
               Backoff: sink.Interrupted → readings Stale; 1, 2, 5, 10, 30, 60 s cap, ±20 % jitter, reset on auth_ok; every attempt logged with its number
```

States: HA `unavailable` → Unavailable ("unavailable in Home Assistant"); `unknown` → Pending (an HA restart may flash
tiles; accepted); a missing or removed entity → Unavailable with "did you mean …?" from the entity list (one
`get_states`, cached until reconnect).

**Kinds and units.** Each entity's `unit_of_measurement` is authoritative; the system unit setting is not (the instance
has 23 temperature sensors in °F and one in °C). The Windows region decides display, as for weather.

| HA | UrDeck |
|---|---|
| `temperature` °F/°C | Temperature (convert to °C) |
| `speed` mph, km/h, m/s | Speed (to km/h) |
| precipitation in, mm | Depth (`DepthUnit` from the entity) |
| `battery`, `humidity`, `%` | Percent (0–100) |
| `timestamp` | Time |
| binary_sensor, switch, light, input_boolean | OnOff |
| any other unit (W, V, kWh, dBm, objects) | Number, `Unit` = HA's text |
| no unit | Text |

Label = `friendly_name`; a widget's `label` overrides it (stats already has null = catalog label, text = that text,
"" = none).

**Actions over REST**, independent of the socket: `POST /api/services/<domain>/<service>` with `entity_id` (a 400 for a
bad service took 21 ms cold on the LAN). Id `homeassistant:<entity_id>/<service>`, domain taken from the entity id.
Every service is allowed. `toggle` is the natural-action alias per domain (scene → `turn_on`, button → `press`, script
→ `turn_on`, media_player → `media_play_pause`, others → `toggle`); literal service names work too. The single argument
maps to the service's main field (`light/turn_on` → `brightness_pct`, `number/set_value` → `value`,
`select/select_option` → `option`, `fan/set_percentage`, `cover/set_cover_position`, `media_player/volume_set` 0–100 →
`volume_level` 0–1, `climate/set_temperature` from °C to HA's unit). REST errors are terse ("400: Bad Request"), so the
provider adds context (401 → token rejected, timeout → no answer, 400 → unknown service or unsupported by the entity).

**SDK and hub additions, in this PR:**
- `IReadingSink.Interrupted(reason)`: a push provider cannot mark readings Stale today (`Unavailable` drops the last
  value; Stale comes only from a failed sample, `ReadingHub.Sampling.cs` ~613). The hub applies the failed-sample rule to
  every wanted path; the next publish restores Ok. MQTT, push and Prometheus will reuse it.
- Runtime descriptor refinement (`sink.Describe(path, descriptor)`) for a concrete path under a pattern; a second
  call re-describes and repaints. This is BACKLOG item 3's "catalog that changes while running".
- `ReadingDescriptor.Action`: the reading's natural action (a path under the same provider), so a generic widget knows
  what a tap does. `ReadingDescriptor.Icon`: a canonical icon key.
- `IReadingBrowser.BrowseAsync`: concrete descriptors for what a provider's patterns cover (HA: one `get_states`, built
  by the same mapping as the refinement). Consumers: "did you mean", the CLI, later the editor.

**Icons.** Map domain + device_class (+ well-known `mdi:` names) to the new `home/` keys (C adds the files):
light, outlet, switch, fan, lock, door, window, blinds, garage, motion, leak, thermometer, droplet, bolt, battery,
scene, script, button, tv, speaker, entity (generic); media_player uses `media/`. 709 of the instance's entities carry
an `mdi:` icon (filled style, clashes with outline themes); those not mapped fall back to the domain's key.

**CLI.** `urdeck-cli readings browse <provider> [--search] [--domain]` for any `IReadingBrowser`; the token comes from
the environment, and on Windows also from Credential Manager (used, never printed).

**Sample and demo.** `sample.json`: concrete fictional entities for the sample page and the site, an `{entity}`
fallback, and new `icon`/`action` fields in the sample format. (Dropped while writing the proposal: `--demo` taps flipping
sample values. Samples stay a pure function of the moment and perform no actions, as the specs already say.)

**Tests.** Internal seams `IHomeAssistantSocket` (JSON messages) and `IHomeAssistantRest`, scripted fakes,
`FakeTimeProvider` for debounce, grace, ping and backoff; fixtures hand-written in the captured shapes. Opt-in only,
never in CI: `[HomeAssistantFact]` (skipped unless `URDECK_HA_E2E=1` plus a URL and token) and
`scripts/Start-HomeAssistantDemo.ps1` (Docker, the `demo` integration, scripted onboarding and token).

**Site.** A Home Assistant page: setup, finding an entity id (HA: Settings ▸ Entities ▸ the entity ▸ ⚙ ▸ Entity ID),
`readings browse`, stats examples.

## C. `control-tile` (#119)

Widget `urdeck.widgets.control`, provider-agnostic (nothing in it knows Home Assistant).

- Config: `reading` (state, optional), `action`, `label`, `icon`, `style`. `action`, `label` and `icon` follow one
  rule: absent → from the descriptor (`Action`, `Label`, `Icon`), text → that, `""` → none. No action → display-only,
  `CanTap` false, so the host shows no press.
- Styles: `switch` (default; 1x1 and dock: icon, label, state tinted with the theme's on/off colours) and `dimmer`
  (2x1: an on/off button at the left end and a brightness bar set by **tap-to-position**; at 1x1 it falls back to
  `switch`). An unknown style is the default, as in stats. More styles later.
- Horizontal drag is the page swipe (`GestureRecognizer`), so no drag slider now; `IDragTarget` is a backlog item.
- Pending after a tap: taps ignored until the reading changes, or 1 s after a successful result with no change (a
  scene), or a failed result / timeout shows a failed look for ~2 s (the hub logs it). On the bar, a new position while
  pending replaces the next one to send (latest wins).
- Icons: the `home/` group in the built-in `outline` set (Lucide), added to the canonical key lists. One icon per
  entity, tinted by state; state-dependent icons (lock open/closed) are a backlog item.
- Goldens: switch on, off, pending, failed; dimmer at a few levels. Site widget page; a sample page entry.

## Decided while writing the proposals (2026-10-27)

The three changes are in `openspec/changes/` (`provider-settings-and-secrets`, `homeassistant-provider`, `control-tile`).
Where they go beyond or against the text above:

- **An action starts its provider** (A, `provider-actions` "Order, Limits And Lifetime"). The spec said the hub never
  starts a provider for an action, so a tile with an action and no reading (a scene button) would fail.
- **A token typed into the JSON is moved** to Credential Manager with a log line (A), as recommended.
- **The dimmer's level is explicit** (C): `level` is a reading id, and its action comes from that reading's description;
  the Home Assistant provider names `light.x/turn_on` as the natural action of `light.x/brightness` (B). Deriving
  `/brightness` from `reading` would have put a Home Assistant convention into the generic widget.
- **No `--demo` toggling** (B), see above.
- **On/off colours** come from the theme's accent and muted text; no new theme values (C).

## Backlog changes made with this handoff

Shared pages replaced by page templates; added `IDragTarget`, state-dependent icons, Home Assistant sign-in and the
editor's reading browser (see `docs/BACKLOG.md` items 3, 8, 11 and 15).
