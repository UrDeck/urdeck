## 1. SDK

- [ ] 1.1 `IReadingSink.Interrupted(reason)` and `IReadingSink.Describe(path, descriptor)` as default members
- [ ] 1.2 `ReadingDescriptor.Action` and `ReadingDescriptor.Icon`; `IReadingBrowser.BrowseAsync`
- [ ] 1.3 Doc comments that state the rules of the data-providers deltas; one minor version with the csproj note; `SdkContractTests`

## 2. Engine

- [ ] 2.1 `ReadingHub`: apply an interruption with the failed-sample rule, idempotently, logged once per change of state
- [ ] 2.2 Refined descriptions per concrete path under a pattern: kept while subscribed or listed, returned by `Describe(id)`, replaced on a second call, redraw of the readings' widgets; ignored and logged once for a path no pattern matches
- [ ] 2.3 `Browse(providerId)`: starts the provider like an action, lingers it, returns descriptions or a message
- [ ] 2.4 `SampleFile` reads `action` and `icon`; samples still offer no actions
- [ ] 2.5 Hub tests with fake providers: interruption (with and without a last value, twice, then publish), refinement (match, no match, replace, redraw, dropped when unsubscribed), browsing (offered, not offered, failing), sample action and icon

## 3. The provider

- [ ] 3.1 Project `providers/UrDeck.Providers.HomeAssistant` (SDK only), settings (`url` required, `token` secret with `url` as context), added to the host's `UrDeckPlugin` list
- [ ] 3.2 Internal seams `IHomeAssistantSocket` and `IHomeAssistantRest` with thin real implementations (`ClientWebSocket`, one `HttpClient`)
- [ ] 3.3 The connection state machine: auth, demand-filtered `subscribe_entities` with a 250 ms debounce, 30 s idle close, ping every 30 s with a 10 s answer, backoff 1/2/5/10/30/60 s with jitter, rejected token stop, `TimeProvider` injected; log lines per the spec
- [ ] 3.4 Messages: first event and diffs to batched publications; removals; attribute paths; `unavailable`, `unknown`, missing entity with "did you mean" (one listing per connection)
- [ ] 3.5 The kind and unit mapping, conversions, ranges, labels, and the description refinement (again on a unit or device-class change)
- [ ] 3.6 Natural action and icon mapping (domain, device class, well-known `mdi:` names, fallbacks)
- [ ] 3.7 Actions over REST: path parsing, the `toggle` alias table (lock by last state), argument fields, `climate` conversion from `get_config`, result messages for 200/400/401/timeout; action catalog patterns
- [ ] 3.8 `IReadingBrowser` from one `get_states`; the connection test (version and entity count, or the failure)
- [ ] 3.9 `sample.json` with fictional entities covering every kind, their actions and icons, and an `{entity}` fallback
- [ ] 3.10 Tests with scripted fakes and a fake clock, fixtures hand-written in the captured shapes with fictional entities: every requirement's scenarios, plus debounce, idle close, ping timeout, backoff sequence and reset, jitter bounds

## 4. Opt-in end-to-end

- [ ] 4.1 `[HomeAssistantFact]` (skipped unless `URDECK_HA_E2E=1`, `URDECK_HA_URL` and the token variable are set) with a few checks: connect, browse, read a state, toggle a demo light and see the change
- [ ] 4.2 `scripts/Start-HomeAssistantDemo.ps1`: Docker `homeassistant/home-assistant` with the `demo` integration, scripted onboarding, prints the URL and sets the token variable for the session; documented in `CONTRIBUTING.md`; never run by CI

## 5. Tool

- [ ] 5.1 `urdeck-cli readings browse` with search, domain, url, config and format options, exit codes, values read for at most 5 seconds, secrets from the environment and Credential Manager on Windows
- [ ] 5.2 Tool tests against a fake browsing provider; the catalog export lists `homeassistant`

## 6. Site and docs

- [ ] 6.1 Site: a Home Assistant page (create a long-lived token, the settings, finding an entity id in Home Assistant and with `readings browse`, stats widget examples, units, what it sends); the provider in the generated reference; the privacy page's list of requests
- [ ] 6.2 `README.md`, `docs/BACKLOG.md` (items 3 and 15) and `docs/ROADMAP.md`

## 7. Verify

- [ ] 7.1 `dotnet build urdeck.slnx -c Release` with 0 warnings, `dotnet test urdeck.slnx -c Release`, the engine coverage floor, `dotnet format urdeck.slnx --severity warn`, the Linux test projects, the site build, and `openspec validate --all --strict`
- [ ] 7.2 Manual, against the owner's installation (readings only, or a harmless toggle): stats tiles for a temperature (°F installation, region units), a power reading and a battery; a page flip and back; unplug the network and see stale, then recovery in the log; a typo's suggestion; `readings browse`
- [ ] 7.3 Close #110 from the pull request description
