## ADDED Requirements

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
