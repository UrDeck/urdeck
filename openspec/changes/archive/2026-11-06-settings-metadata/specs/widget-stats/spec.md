## MODIFIED Requirements

### Requirement: Stats Widget Identity
UrDeck MUST ship a widget with the type id `urdeck.widgets.stats` that supports the sizes 1x1, 2x1, 2x2, 4x2 and 4x4.
It shows one reading at 1x1, 2x1 and 2x2, up to five at 4x2 and up to seven at 4x4 (see "Stats Compositions"). It MUST
reference only the widget SDK, MUST NOT reference any provider's assembly, and MUST be refreshed by reading changes
only (no timer).

#### Scenario: Shows a reading from any provider
- **WHEN** the widget is configured with the id of a reading from a provider other than `system`
- **THEN** it shows that reading with the same layout and formatting rules

#### Scenario: No timer
- **WHEN** only stats widgets are placed
- **THEN** the host schedules no per-widget refresh timer for them

#### Scenario: Larger sizes are offered
- **WHEN** the registry lists the sizes of `urdeck.widgets.stats`
- **THEN** they are 1x1, 2x1, 2x2, 4x2 and 4x4

#### Scenario: The sample page's 2x1 cards
- **WHEN** the sample page, which has two Stats widgets at 2x1, is drawn
- **THEN** each shows one reading, exactly as before the size was declared
