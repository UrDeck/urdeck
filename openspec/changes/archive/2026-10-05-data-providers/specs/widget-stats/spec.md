## Purpose

Defines the first-party stats widget, which shows readings from any provider. It is the single-stat card now and
grows into the performance widget by adding sizes and presentations.

## ADDED Requirements

### Requirement: Stats Widget Identity
UrDeck MUST ship a widget with the type id `urdeck.widgets.stats` that supports the sizes 1x1 and 2x2 and shows one
reading at each of them. It MUST reference only the widget SDK, MUST NOT reference any provider's assembly, and MUST
be refreshed by reading changes only (no timer).

#### Scenario: Shows a reading from any provider
- **WHEN** the widget is configured with the id of a reading from a provider other than `system`
- **THEN** it shows that reading with the same layout and formatting rules

#### Scenario: No timer
- **WHEN** only stats widgets are placed
- **THEN** the host schedules no per-widget refresh timer for them

### Requirement: Slot Configuration
The widget's settings MUST be a list named `slots`. Each slot describes one reading on the card:

- `reading`: the reading id
- `label`: not present (the widget shows the reading's default label from the catalog), a text (shown as given), or
  an empty text (no label is shown and no space is reserved for one)
- `unit`: optional display unit for kinds that have several (`celsius`, `fahrenheit`)
- `decimals`: optional number of decimals

At 1x1 and 2x2 the widget shows the first slot and ignores further slots without removing them from the file. With no
slot configured it shows `system:cpu/load`. Unknown properties in a slot MUST be preserved across load and save.

#### Scenario: Per-core card
- **WHEN** a 1x1 stats widget has the single slot `{ "reading": "system:cpu/core/2/load" }`
- **THEN** it shows that core's load with a raised `%` and the label `Core 2`

#### Scenario: Label override
- **WHEN** the slot sets `"label": "Render thread"`
- **THEN** that text is shown as the label

#### Scenario: Label hidden
- **WHEN** the slot sets `"label": ""`
- **THEN** no label is drawn and the value uses the space

#### Scenario: Default configuration
- **WHEN** a stats widget is placed with no `slots`
- **THEN** it shows the total CPU load

#### Scenario: Extra slots survive
- **WHEN** a 1x1 stats widget has three slots and the configuration is saved
- **THEN** all three slots are still in the file

### Requirement: Stats Presentation
The widget MUST draw its reading with the SDK's readout and the SDK's reading formatter, and take every colour, font
and size from the theme.

- The text size MUST NOT change as the value changes
- An unavailable reading is shown as a dash with its unit and label
- A stale reading, and a pending reading that has a last value, are shown with that value in the theme's muted colour
- The widget MUST NOT draw a card, a title bar or a background of its own

#### Scenario: Value changes width
- **WHEN** a percentage reading goes from `8` to `100`
- **THEN** the text size is unchanged

#### Scenario: Unavailable reading
- **WHEN** the configured reading does not exist
- **THEN** the card shows a dash where the value would be and stays otherwise laid out as usual

#### Scenario: Stale reading
- **WHEN** the reading's provider stops reporting
- **THEN** the last value stays on the card in the muted colour

### Requirement: Stats Repaint
The widget MUST ask to be repainted only when what it would draw has changed: the value text, the unit, the label or
whether the value is current.

#### Scenario: Value unchanged after rounding
- **WHEN** a load goes from 3.2 to 3.4 percent and the widget shows no decimals
- **THEN** the widget is not repainted

#### Scenario: Value becomes stale
- **WHEN** the reading goes from ok to stale with the same value
- **THEN** the widget is repainted once
