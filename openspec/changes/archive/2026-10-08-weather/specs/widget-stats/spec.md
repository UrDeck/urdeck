## ADDED Requirements

### Requirement: Stats Attribution
When the description of a reading shown in any position carries an attribution, the widget MUST draw it with the SDK's
attribution component, once for all positions, and MUST reserve the space for it at the bottom of the content
rectangle. A widget that shows no reading with an attribution MUST be laid out exactly as before.

#### Scenario: Weather reading in a stats card
- **WHEN** a 1x1 stats widget has the slot `{ "reading": "weather:Zurich/current/temperature" }`
- **THEN** the reading is drawn with the shorter credit `Open-Meteo.com` or the full text, whichever fits, below it

#### Scenario: Two readings, one credit
- **WHEN** a 4x2 stats widget shows two weather readings
- **THEN** the credit is drawn once

#### Scenario: Reading without attribution
- **WHEN** a stats widget shows only `system:` readings
- **THEN** no credit is drawn and no space is reserved, and the card is identical to before this requirement existed

#### Scenario: Credit is theme text
- **WHEN** the active theme changes
- **THEN** the credit follows the theme's label size and muted colour
