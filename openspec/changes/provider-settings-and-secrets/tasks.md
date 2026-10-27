## 1. SDK

- [x] 1.1 Move `ProviderSettings` to `UrDeck.Sdk` (keep `intervalMs`, add `actionTimeoutMs` and `actionWarnMs`); add attributes for a setting's label, help text, required flag, a secret's context setting and a provider's missing-setting reason
- [x] 1.2 Add `[Secret]` and `SecretValue` (no JSON converter, masked `ToString()`, explicit `Reveal()`), with SDK tests that it never serializes and never prints
- [x] 1.3 Add the typed settings hand-off (a `DataProvider<TSettings>` base or a `Start(sink, settings)` overload; record the choice in `design.md`) and the optional connection-test hook; update the analyzer's provider checks if any depend on the start signature
- [x] 1.4 Move the package version as the csproj comment's rules say, and update `SdkContractTests`

## 2. Engine: settings

- [x] 2.1 Bind `providers.<id>` to the provider's declared settings type with the widget configuration's JSON options; log a wrong-typed value once and keep the default
- [x] 2.2 Required settings: do not start a provider with one missing; its readings are unavailable with the declared reason or the default one
- [x] 2.3 Restart a running provider whose settings or secrets changed (on reload, and when a secret is saved or removed); leave the others; keep the live `intervalMs` path
- [x] 2.4 Move System, Weather and Media to the new start signature
- [x] 2.5 Hub tests with a fake provider: binding, wrong type, required missing, restart on its own change only, `intervalMs` without restart

## 3. Engine: secrets

- [x] 3.1 `ISecretStore` with the environment lookup (name projection rule) and an in-memory implementation for tests
- [x] 3.2 The Credential Manager store (`CredReadW`, `CredWriteW`, `CredDeleteW`, `CredEnumerateW`; generic, persist local machine; context in `UserName`; size limit with a message; target prefix injectable), Windows only
- [x] 3.3 Fill only the declared `[Secret]` properties of a provider before it starts; log one line per secret (source or missing), never the value; log the context mismatch
- [x] 3.4 Move a secret typed into the configuration file to Credential Manager and remove the key (not for a newer `version`; plain-text note on Linux)
- [x] 3.5 Tests: projection rule, lookup order, scoping (a provider never receives another's secret), masked logging, the move (and its two exceptions), context mismatch line; one `[WindowsFact]` Credential Manager round trip and enumeration under `UrDeckTest/`, cleaned up

## 4. Actions

- [x] 4.1 Per-provider `actionTimeoutMs` (minimum 1,000, logged when raised) and `actionWarnMs` (a warning line naming duration and threshold)
- [x] 4.2 An action starts a provider that is not running and the provider lingers afterwards; a provider that cannot start fails the action with its reason
- [x] 4.3 Update `ReadingHubActionTests` for the configured limit, the warning, starting for an action, and failing when required settings are missing

## 5. Host

- [ ] 5.1 Settings window: the Providers section generated from the declarations (text, password, number, on/off), secret fields that show source and offer save and remove, Save per provider, Test connection with a 10 second limit, an empty state
- [x] 5.2 `ISettingsHost` gains what the section needs (declarations, current values, save, secret save and remove, test) without the window touching the engine directly
- [x] 5.3 Velopack uninstall hook: delete every `UrDeck/*` credential after quitting the deck and removing the Run value
- [x] 5.4 Check the settings window soak (`URDECK_SETTINGS_SOAK=1`) still releases memory with the new section, and update `docs/perf/settings-window.md` if the numbers move

## 6. Tool and site

- [x] 6.1 `catalog export` includes each provider's settings (key, type, label, help, required, secret, default); site reference tables generated from it, and the build check covers them
- [x] 6.2 Site: the configuration page explains provider settings, secrets and the environment override; the privacy page says where secrets live, that they go only to their service, that same-user malware is not defended against, and that uninstall removes them
- [x] 6.3 `CONTRIBUTING.md` (declaring settings and secrets for provider authors), `docs/BACKLOG.md` item 3 and `docs/ROADMAP.md`

## 7. Verify

- [x] 7.1 `dotnet build urdeck.slnx -c Release` with 0 warnings, `dotnet test urdeck.slnx -c Release`, the engine coverage floor, `dotnet format urdeck.slnx --severity warn`, the Linux test projects, the site build, and `openspec validate --all --strict`
- [ ] 7.2 Manual: enter a token in Settings, see the credential in Control Panel, restart and see the log line; paste a token into the file and see it moved; remove it; uninstall an installed copy and see the credential gone
- [ ] 7.3 Close #109 from the pull request description
