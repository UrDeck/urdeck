# provider-secrets Specification

## Purpose
Defines how a data provider declares a secret setting (a token, a password), where its value is kept and looked up, which
provider receives it, and how it is entered, logged, moved out of the configuration file and deleted, so that no secret
is ever stored in `urdeck-config.json` or written to the log.
## Requirements
### Requirement: Secret Settings Are Declared
A provider MUST declare each secret as a property of its settings type marked secret. A secret setting is never read from
nor written to the configuration file by the configuration store, and its value is never written to the log, in any
form (not its text, not its length).

- The value type of a secret prints as a fixed mask and gives its text only through an explicit call
- The engine fills a provider's secret properties before it starts the provider, and only the properties that the
  provider's own settings type declares, under that provider's id. No provider can ask for another provider's secret
  through the SDK

#### Scenario: Logging a settings object
- **WHEN** a provider writes its settings object, which holds a token, to the log
- **THEN** the log shows the mask in place of the token

#### Scenario: Saving the configuration
- **WHEN** the configuration is saved while a provider with a stored token is running
- **THEN** the file contains no token

### Requirement: Where A Secret Is Kept
A secret MUST be identified by its provider id and setting name, and looked up in this order:

1. The environment variable `URDECK_SECRET_<PROVIDER>_<SETTING>`: the provider id and the setting name in upper case,
   joined by `_`, with `.` and `-` replaced by `_` (`homeassistant`, `token` → `URDECK_SECRET_HOMEASSISTANT_TOKEN`)
2. On Windows, the Credential Manager generic credential with target `UrDeck/<provider>/<setting>`
   (`UrDeck/homeassistant/token`), stored for the current user on this PC and not roamed

The first found wins. Where Credential Manager is not available (Linux, or a Windows policy that blocks it) only the
environment is read, and the unavailability is logged once. Every copy of UrDeck the user runs on the PC (installed,
portable, a development build) shares the stored secrets. A value larger than Credential Manager accepts is refused when
it is saved, with a message saying so.

#### Scenario: Environment wins
- **WHEN** `URDECK_SECRET_HOMEASSISTANT_TOKEN` is set and Credential Manager also holds `UrDeck/homeassistant/token`
- **THEN** the provider receives the environment's value

#### Scenario: Stored in Credential Manager
- **WHEN** the user saves a token for Home Assistant in the settings window
- **THEN** Control Panel's Credential Manager lists a generic credential `UrDeck/homeassistant/token`, and the
  configuration file is unchanged apart from non-secret settings saved with it

### Requirement: Secret Context
A provider MAY name a non-secret setting as the context of a secret (for Home Assistant, its address); the host MUST then
keep the context with the secret. When the secret is
saved, the context's value is stored with it. When a copy starts with a stored secret whose context differs from its own
setting, the provider still receives the secret and the log says that it was saved for the other context, naming both
values, so a failure that follows is explained.

#### Scenario: Two copies, two addresses
- **WHEN** a token was saved for `https://ha.home` and another copy is configured for `https://ha.cabin`
- **THEN** the second copy's log says the token was saved for `https://ha.home`, and setting the environment variable for
  that copy separates them

### Requirement: A Secret Typed Into The File Is Moved
When the configuration holds a text value under a key that a loaded provider declares secret, the host MUST, at start or
on reload, store the value in Credential Manager, remove the key from the configuration file and log
`Secret <provider>/<setting>: moved from urdeck-config.json to Credential Manager`. The provider receives the value either
way.

- A configuration whose `version` is newer than the host's is never written: the value is used for this run, nothing is
  moved, and the log says so
- Where there is no store (Linux), the value is used, nothing is moved, and the log says the secret is kept in plain text
- An environment variable for the same secret wins as usual; the value in the file is still moved

#### Scenario: Pasted token
- **WHEN** a user writes `"token": "abc…"` under `providers.homeassistant` and starts UrDeck on Windows
- **THEN** the provider connects with that token, Credential Manager holds `UrDeck/homeassistant/token`, the file no longer
  has the key, and the log has the move line

### Requirement: Secrets In The Log
At each start of a provider that declares secrets, the host MUST log one line per secret saying where it came from or that
it is missing: `Secret <provider>/<setting>: from Credential Manager`, `from <environment variable name>` or
`missing; set it in Settings`.

#### Scenario: Missing token
- **WHEN** the Home Assistant provider starts and no token is stored or set
- **THEN** the log says `Secret homeassistant/token: missing; set it in Settings`

### Requirement: Removing A Secret
A secret MUST be removable from the settings window, which deletes its credential and restarts the provider without it.
Uninstalling UrDeck deletes every credential whose target starts with `UrDeck/` (see app-updates, "Uninstall").

#### Scenario: Remove from Settings
- **WHEN** the user removes the Home Assistant token in the settings window
- **THEN** `UrDeck/homeassistant/token` is gone from Credential Manager and the provider's readings report the missing
  setting

### Requirement: Secrets Set By Sign-In
A secret setting MAY be declared as set by sign-in (see provider-sign-in). It MUST then be stored only from a sign-in's
result or from its own provider's renewal, never typed into the Providers section; the environment variable still
overrides it as for any secret, and a value typed into the configuration file is moved as for any secret. The account
name of the sign-in MUST be stored with the secret in the same Credential Manager entry (as the credential's user name),
never in the configuration file or the log, and is deleted with it.

#### Scenario: Stored with its account
- **WHEN** a Steam sign-in is approved for the account `example_gamer`
- **THEN** Credential Manager holds `UrDeck/steam/refreshToken` with the user name `example_gamer`, and
  `urdeck-config.json` holds neither

#### Scenario: From the environment
- **WHEN** `URDECK_SECRET_STEAM_REFRESHTOKEN` is set
- **THEN** the provider receives it, and the Providers section says Steam is connected from that variable, with no account
  name

### Requirement: A Provider Renews Its Own Secret
A running provider MUST be able to store a new value of a secret its own settings type declares as set by sign-in, for
example a rotated refresh token. The new value replaces the stored one and the account name is kept. Storing it MUST NOT
restart the provider, and it is not a change to the configuration. A request for any other secret, or for another
provider's, is refused and logged as a provider fault. When the secret comes from the environment variable, or no store is
available, nothing is written, the provider is told so, and the log says once that the renewed secret is kept for this
run only. A value larger than Credential Manager accepts is refused with a log line.

#### Scenario: Token rotated
- **WHEN** the running Steam provider stores a new refresh token
- **THEN** Credential Manager holds the new value under the same target and account, the provider keeps running, and the
  log says `Secret steam/refreshToken: renewed`

#### Scenario: Not its own secret
- **WHEN** a provider asks to store `homeassistant/token`
- **THEN** nothing is stored and the log names the provider

