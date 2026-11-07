## MODIFIED Requirements

### Requirement: Provider Settings
A provider MAY declare a settings type. The engine MUST bind the object `providers.<id>` of `urdeck-config.json` to it
and hand it to the provider when it starts; a provider that declares none receives the base settings (`intervalMs`,
`actionTimeoutMs`, `actionWarnMs`).

- Unknown keys are kept in the file and ignored, as today
- A value of the wrong type is logged once, naming the provider and the key, and the property keeps its default
- A property MAY be marked required. While a required setting is missing (absent, empty, or a secret with no value; see
  provider-secrets), the provider is not started and each of its readings is unavailable with a reason that names what
  to set (the provider's own text, or a default naming the provider and the setting)
- Each setting is declared as settings-metadata, "Declared Settings", describes, with the same kinds and modifiers as a
  widget's settings; a secret is a Secret setting. The label and help text are used by the editor and the generated
  reference

#### Scenario: Address reaches the provider
- **WHEN** the configuration has `"providers": {"homeassistant": {"url": "https://ha.home"}}`
- **THEN** the Home Assistant provider starts with `url` set to `https://ha.home`

#### Scenario: Required setting missing
- **WHEN** a widget shows a Home Assistant reading and `providers.homeassistant.url` is absent
- **THEN** the provider is not started and the reading is unavailable with a reason that says to set up Home Assistant
  in Settings

#### Scenario: Same declaration as widgets
- **WHEN** the schema of the Home Assistant provider's settings is read
- **THEN** `url` is a required text setting and `token` a secret setting whose context is `url`, described the same way
  a widget's settings are
