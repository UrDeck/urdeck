## Why

A provider that talks to a service of the user's (Home Assistant first, then MQTT, GitHub, Prometheus) needs an address
and a credential, and today it can receive neither: `providers.<id>` in `urdeck-config.json` keeps unknown keys "for the
provider", but `IDataProvider.Start` hands nothing over, and a token written into that plain-text file leaks the first
time the file is pasted into an issue. This is GitHub issue #109, the groundwork the Home Assistant provider
(`homeassistant-provider`, #110) waits for. The decisions come from `docs/handoff/2026-10-26-home-assistant.md`.

## What Changes

- **Settings reach the provider.** A provider declares a settings type; the engine binds `providers.<id>` to it and hands
  it over when the provider starts. A change to one provider's settings restarts that provider only. **BREAKING** for
  the provider contract (allowed before release): starting a provider now carries its settings.
- **Secrets are declared, never written.** A setting marked secret is never read from or written to the configuration
  file and never appears in the log. Its value comes from the environment variable `URDECK_SECRET_<PROVIDER>_<SETTING>`
  or from Windows Credential Manager (`UrDeck/<provider>/<setting>`), and a provider receives only its own.
- **A token typed into the file by hand is moved** into Credential Manager on the next start and removed from the file,
  with a log line.
- **The settings window gets a bare Providers section**, generated from the declarations (text, password, number,
  on/off) with an optional "Test connection", the first piece of the configuration editor. The deck never shows a secret.
- **Action limits per provider.** `actionTimeoutMs` (default 10 s, today's fixed limit) and `actionWarnMs` (default 2 s;
  a slower action logs a warning) on every provider. An action now starts a provider that is not running, so a tile that
  only acts (a scene button) works.
- **Uninstall deletes UrDeck's secrets.**

Not in this change: the Home Assistant provider; signing in with OAuth (a backlog item, using a loopback client id);
a full configuration editor; secrets on Linux beyond the environment (the Linux paths run on sample data).

## Capabilities

### New Capabilities

- `provider-secrets`: how a secret setting is declared, where its value is kept and looked up, who receives it, how it
  is entered, logged, moved out of the configuration file and deleted.

### Modified Capabilities

- `data-providers`: providers declare settings and receive them at start; a settings change restarts only that provider.
- `provider-actions`: the action time limit and a warning threshold become provider settings; an action starts a provider
  that is not running.
- `settings-window`: a Providers section generated from the providers' declared settings.
- `app-updates`: uninstall removes UrDeck's secrets.
- `urdeck-cli`: `catalog export` lists each provider's settings (never a secret's value).

## Impact

- `sdk/UrDeck.Sdk`: `ProviderSettings` moves here from the engine (keeps `intervalMs`, gains `actionTimeoutMs` and
  `actionWarnMs`); a typed provider base or a `Start` that takes settings; `[Secret]` and `SecretValue`; an optional
  connection-test hook. Breaking; the package version moves one major-before-1.0 step with a note in the csproj comment.
- `sdk/UrDeck.Analyzer`: whatever checks it makes on provider classes follow the new start signature.
- `src/UrDeck.Engine`: settings binding, the secret store (environment, Credential Manager on Windows), per-provider
  restart on its own settings change, the hand-typed token move, action timeout and warning per provider, starting a
  provider for an action.
- `src/UrDeck.Host`: the Providers section of the settings window; the Velopack uninstall hook deletes `UrDeck/*`
  credentials.
- `providers/*`: System, Weather and Media follow the new start signature (they declare no settings beyond the base).
- `tools/UrDeck.Cli`: `catalog export` includes each provider's settings, so the site documents them.
- Site: the generated reference lists provider settings; the privacy page says where secrets are kept, that they go only
  to the service they belong to and that uninstall removes them; the configuration page explains secrets and the
  environment override.
- Security: secrets are tied to the Windows user on this PC (Credential Manager), visible and deletable in Control Panel,
  and shared by every copy of UrDeck the user runs. They do not protect against malicious code running as the same user,
  and the privacy page says so.
- Tests: an in-memory store for the logic, a fake provider for binding and restart, one Windows-only Credential Manager
  round trip under a test prefix.
- Closes #109.
