## Context

`ProviderSettings` lives in the engine (`src/UrDeck.Engine/Config/ConfigStore.cs`) with `intervalMs` and an extension-data
bag; `ReadingHub.ApplySettings` reads the interval and nothing reads the rest. `IDataProvider.Start(IReadingSink)` is the
only hand-off. Widgets already have the pattern this change copies: `Widget<TConfig>` with a typed configuration bound
from JSON. The settings window (`SettingsWindow`, talks to the host through `ISettingsHost`) has one page, the monitor
picker. The installer is Velopack; its uninstall hook already removes the Run value. The action path
(`provider-actions`) runs actions per provider with a fixed 10 s limit and refuses to start a stopped provider.

The exploration and the measurements behind it are in `docs/handoff/2026-10-26-home-assistant.md`.

## Goals / Non-Goals

**Goals:** any provider, first-party or not, can declare settings and secrets without new engine or UI code; a secret
never touches the configuration file or the log; non-technical users enter a secret in a window, developers through the
environment; nothing UrDeck runs is involved (no endpoint, no urdeck.app).

**Non-Goals:** OAuth sign-in; per-data-folder secrets; secrets larger than Credential Manager allows; protection against
code running as the same user; a general configuration editor.

## Decisions

1. **Typed settings, mirroring widgets.** `ProviderSettings` moves to the SDK; a provider declares a subclass (for
   example by deriving from `DataProvider<TSettings>`, or through a `Start(sink, settings)` overload; the implementer
   picks whichever keeps the analyzer and the loader simplest, and records it here). The engine binds `providers.<id>`
   with the same JSON options as widget configuration and passes the instance at start. Rejected: handing the raw
   `JsonElement` bag over (every provider re-implements binding and validation, and the settings form cannot be
   generated).

2. **`[Secret]` on a property of type `SecretValue`.** `SecretValue` has no JSON converter (binding skips it and saving
   never writes it), prints `•••` from `ToString()`, and yields its text only through `Reveal()`. A provider that logs its
   settings object cannot leak the token by accident. Rejected: a `string` with an attribute (one `$"{settings}"` leaks it).

3. **The store, keyed by (provider id, setting name).** Lookup order: the environment variable, then Credential Manager.
   - Environment name: `URDECK_SECRET_` + provider id + `_` + setting name, upper case, with `.` and `-` turned into `_`.
     Only this direction is needed: the engine knows each declared secret and computes the name.
   - Credential Manager: a generic credential, target `UrDeck/<provider id>/<setting name>`, persist
     `CRED_PERSIST_LOCAL_MACHINE` (this user on this PC, not roamed), P/Invoke `CredReadW`, `CredWriteW`, `CredDeleteW`,
     `CredEnumerateW`, `[SupportedOSPlatform("windows")]`. Visible and deletable in Control Panel, which a DPAPI file in
     the data folder is not; the protection is the same (tied to the Windows user).
   - The credential's `UserName` field records a context the provider gives when the secret is saved (Home Assistant:
     its URL), so a copy configured for another address can say "the token was saved for <url>" instead of failing with
     an opaque 401. The provider declares which setting is the context (an attribute argument); the engine compares.
   - Shared by every copy on the PC (installed, portable, the dev build). Two copies against two different services use
     the environment override. Rejected: keying by data folder (ugly names in Credential Manager; the dev build would need
     its own token).
   - On Linux and wherever Credential Manager is absent, the store reads the environment only.
   - The blob limit is 2,560 bytes; saving a larger value fails with a message saying so.

4. **Providers never see the store.** The engine fills only the `[Secret]` properties the provider's own settings type
   declares, under that provider's id. Honest limit: plugins run in process with full trust, so a malicious plugin could
   call `CredRead` itself; that is plugin trust (signing, the marketplace), not this change.

5. **A secret typed into the file is moved, not refused.** On start, a string value in the configuration under a key that
   the provider declares secret is written to Credential Manager, removed from the in-memory configuration, and the file is
   saved, with the log line `Secret <provider>/<setting>: moved from urdeck-config.json to Credential Manager`. This is
   NuGet's behaviour with an encrypted password and the friendliest one for someone following a forum post. Not done when
   the configuration's `version` is newer than the host's (that file is never written): the secret is then used from the
   file for this run and the log says it was not moved. On Linux, where there is no store, the value is used and the log
   says it is kept in plain text.

6. **Restart on a settings change, per provider.** On configuration reload the engine compares each provider's bound
   settings (and its secrets' identity, never their values in a log) with the running ones; a provider whose settings
   changed is shut down and started again with the new ones, its readings passing through Pending. `intervalMs` keeps its
   live path (no restart). A secret saved from the settings window triggers the same restart. Rejected: an
   `ApplySettings` callback on every provider (each must handle every transition correctly; a restart is always right).

7. **Missing required settings.** A settings property may be marked required. With one missing, the provider is not
   started; each of its readings is Unavailable with a reason the provider declares ("set up Home Assistant in
   Settings") or a default naming the provider and the setting.

8. **The settings form is generated.** The Providers section lists each loaded provider that declares settings beyond the
   base, with one field per property: text, password (secrets: shows whether a value is stored and where, never the
   value; Save and Remove), number, on/off. Labels and help come from attributes on the property (the doc comment is not
   available at run time). A provider may implement an optional `ValidateAsync(settings, cancellationToken)` returning a
   one-line result shown under a "Test connection" button. The form writes the configuration through the existing config
   store and secrets through the secret store; nothing is applied until Save.

9. **Action limits per provider, and actions start providers.** `actionTimeoutMs` (default 10,000; at least 1,000) and
   `actionWarnMs` (default 2,000) on the base settings. The hub reads them per action. An action for a provider that is
   not running starts it (as a first subscriber would) and the provider lingers afterwards as if its last subscriber had
   left. Rejected: keeping "never starts" (a scene tile with no reading could never act) or having such widgets subscribe
   to a dummy reading.

10. **Uninstall.** The Velopack uninstall hook enumerates `UrDeck/*` credentials and deletes them, after quitting the
    deck and removing the Run value. The prefix is a parameter so the tests use `UrDeckTest/` and can never touch real
    credentials.

11. **Log lines.** At provider start, one line per declared secret: `Secret homeassistant/token: from Credential Manager`
    / `from URDECK_SECRET_HOMEASSISTANT_TOKEN` / `missing; set it in Settings`. Never the value, never its length.

## Risks / Trade-offs

- [Secrets shared across copies surprise someone running two instances] → the context check logs a clear line; the
  environment override separates them.
- [Moving a hand-typed token rewrites the user's file] → only that key is removed, the log says so, and the rest of the
  file is written as the config store always writes it.
- [A provider restart flashes its tiles] → only on that provider's own settings change; readings keep their last value
  through Pending.
- [Breaking the provider contract] → no third-party providers exist yet; first-party ones move in this change.
- [Credential Manager unavailable (policy, roaming quirks)] → the store reports it once and the environment still works.

## Migration Plan

None for users (no secrets exist yet). Plugins built against the old SDK must be rebuilt; the loader reports one that
does not match.
