## MODIFIED Requirements

### Requirement: Reading Catalog
A provider MUST describe every reading it offers. A description MUST contain the reading's path and kind, a short
default label, and a full name; it MAY contain the name of the device the reading belongs to, a range (minimum and
maximum), a warning value and a critical value, a unit text for plain numbers, a default display unit and a number of
decimals.

- The kinds are: plain number, percentage, temperature, text and on/off; a temperature is always reported in degrees
  Celsius
- The warning and critical values say from where on the reading deserves attention; they are in the same unit as the
  value, and a reading for which a high value is not a fault declares none
- The description of a reading MUST be available to a widget together with the value, without I/O
- The catalog of a provider MUST be obtainable without any widget being subscribed to it
- A provider built before the warning and critical values existed MUST load and run unchanged; its readings have none

#### Scenario: Default label comes from the catalog
- **WHEN** a widget shows `system:cpu/core/2/load` and the user has set no label
- **THEN** the widget can show the label the provider declared for that reading

#### Scenario: Catalog without subscribers
- **WHEN** the catalog of a registered provider is requested while no widget uses it
- **THEN** its readings are listed and the provider does no sampling

#### Scenario: Reading declares its levels
- **WHEN** a provider describes a temperature with a warning value of 80 and a critical value of 90
- **THEN** a widget that shows the reading can read both values from the description

#### Scenario: Reading declares no levels
- **WHEN** a widget reads the description of `system:cpu/load`
- **THEN** it has no warning value and no critical value
