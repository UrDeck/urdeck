## ADDED Requirements

### Requirement: Provider Settings
A provider MAY declare a settings type. The engine MUST bind the object `providers.<id>` of `urdeck-config.json` to it
and hand it to the provider when it starts; a provider that declares none receives the base settings (`intervalMs`,
`actionTimeoutMs`, `actionWarnMs`).

- Unknown keys are kept in the file and ignored, as today
- A value of the wrong type is logged once, naming the provider and the key, and the property keeps its default
- A property MAY be marked required. While a required setting is missing (absent, empty, or a secret with no value; see
  provider-secrets), the provider is not started and each of its readings is unavailable with a reason that names what
  to set (the provider's own text, or a default naming the provider and the setting)
- Each property carries a label and a help text for the settings window and the generated reference

#### Scenario: Address reaches the provider
- **WHEN** the configuration has `"providers": {"homeassistant": {"url": "https://ha.home"}}`
- **THEN** the Home Assistant provider starts with `url` set to `https://ha.home`

#### Scenario: Required setting missing
- **WHEN** a widget shows a Home Assistant reading and `providers.homeassistant.url` is absent
- **THEN** the provider is not started and the reading is unavailable with a reason that says to set up Home Assistant
  in Settings

### Requirement: A Settings Change Restarts Only Its Provider
On configuration reload, and when a secret is saved or removed, the engine MUST restart a running provider whose settings
(or secrets) changed: it shuts the provider down and starts it with the new settings, its readings keeping their last
values as pending. Providers whose settings did not change are not touched. A change to `intervalMs` alone takes effect
without a restart (see "Sampling Rate Settings").

#### Scenario: New address
- **WHEN** the user changes `providers.homeassistant.url` and saves while the weather provider is also running
- **THEN** the Home Assistant provider is restarted with the new address and the weather provider keeps running untouched

#### Scenario: Unrelated save
- **WHEN** the configuration is saved with only a widget moved
- **THEN** no provider is restarted
