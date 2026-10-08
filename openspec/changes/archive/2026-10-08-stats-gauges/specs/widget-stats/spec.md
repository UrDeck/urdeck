## MODIFIED Requirements

### Requirement: Stats Widget Identity
UrDeck MUST ship a widget with the type id `urdeck.widgets.stats` that supports the sizes 1x1, 2x2, 4x2 and 4x4. It
shows one reading at 1x1 and 2x2, up to five at 4x2 and up to seven at 4x4 (see "Stats Compositions"). It MUST
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
- **THEN** they are 1x1, 2x2, 4x2 and 4x4

### Requirement: Slot Configuration
The widget's settings MUST be a list named `slots`. Each slot describes one reading on the card:

- `reading`: the reading id
- `label`: not present (the widget shows the reading's default label from the catalog), a text (shown as given), or
  an empty text (no label is shown and no space is reserved for one)
- `unit`: optional display unit for kinds that have several (`celsius`, `fahrenheit`)
- `decimals`: optional number of decimals
- `style`: optional; `plain`, `gauge` (the theme's default gauge style), `ring`, `bar` or `verticalBar`
- `min`, `max`: optional range of the gauge, replacing the range in the reading's description
- `warning`, `critical`: optional level values, replacing those in the reading's description

Slots fill the positions of the widget's composition in order (see "Stats Compositions"). Slots beyond the positions
of the current size are ignored without being removed from the file. With no slot configured the first position shows
`system:cpu/load`. Unknown properties in a slot MUST be preserved across load and save. A `style` the widget does not
know is treated as not present and stays in the file.

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

#### Scenario: Configuration written before gauges existed
- **WHEN** a 1x1 stats widget has the single slot `{ "reading": "system:cpu/load" }` and no `style`
- **THEN** it is drawn as a plain readout, as before

#### Scenario: Unknown style
- **WHEN** a slot sets `"style": "disc"`
- **THEN** the slot is drawn as if it had no `style`, and `disc` is still in the file after a save

### Requirement: Stats Presentation
The widget MUST draw every reading with the SDK's gauge component, the SDK's gauge scale and the SDK's reading
formatter, and take every colour, font and size from the theme.

- The style of a position is chosen as follows:

  | Slot `style` | 1x1 and 2x2 | Gauge position at 4x2 and 4x4 | Text position |
  |---|---|---|---|
  | not present | plain | the theme's default gauge style | plain |
  | `gauge` | the theme's default gauge style | the theme's default gauge style | plain |
  | `ring`, `bar`, `verticalBar` | that style | that style | plain |
  | `plain` | plain | plain | plain |

- A reading that has a value but no fraction (see components, "Gauge Scale") is drawn plain, whatever style was chosen
- The slot's `min`, `max`, `warning`, `critical` and `decimals` are passed to the gauge scale
- The text size MUST NOT change as the value changes
- An unavailable reading is shown as a dash with its unit and label, and with an empty track when its style is a gauge
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

#### Scenario: Gauge at 1x1
- **WHEN** a 1x1 stats widget has the slot `{ "reading": "system:cpu/core/2/load", "style": "ring" }`
- **THEN** it shows a ring filled to that core's load with the value and the label `Core 2` inside it

#### Scenario: Theme decides the style
- **WHEN** a slot sets `"style": "gauge"` and the theme's default gauge style is `bar`
- **THEN** the slot is drawn as a bar

#### Scenario: Reading that cannot be scaled
- **WHEN** a slot with `"style": "ring"` points at a text reading
- **THEN** the text is drawn as a plain readout and no ring is drawn

#### Scenario: Slot levels
- **WHEN** a slot sets `"warning": 50` on `system:cpu/load` and the load is 60
- **THEN** the gauge's fill is drawn in the theme's warning colour and the value keeps the text colour

### Requirement: Stats Repaint
The widget MUST ask to be repainted only when what it would draw at rest has changed in one of the positions it
shows: the value text, the unit, the label, whether the value is current, the fraction the gauge moves to, or the
level.

#### Scenario: Value unchanged after rounding
- **WHEN** a load goes from 3.2 to 3.4 percent and the widget shows no decimals
- **THEN** the widget is not repainted, whether the slot is plain or a gauge

#### Scenario: Value becomes stale
- **WHEN** the reading goes from ok to stale with the same value
- **THEN** the widget is repainted once

#### Scenario: Slot that is not shown changes
- **WHEN** a 1x1 stats widget has three slots and the reading of the third changes
- **THEN** the widget is not repainted

## ADDED Requirements

### Requirement: Stats Compositions
The widget MUST choose its composition from its grid size and fill the positions with its slots in order.

- 1x1 and 2x2: one position, slot 1
- 4x2: one row of two gauge positions (slots 1 and 2) above one row of three text positions (slots 3 to 5)
- 4x4: two rows of two gauge positions (slots 1 to 4) above one row of three text positions (slots 5 to 7)
- Gauge positions in one card are equal rectangles, and their readouts are drawn at one text size
- The text row has the same height at 4x2 and at 4x4; its positions are three equal columns
- A position without a slot is left empty, apart from the first position when no slot is configured at all
- The widget MUST declare as its readings those of the positions its size shows, and no others

#### Scenario: Full 4x4
- **WHEN** a 4x4 stats widget has seven slots
- **THEN** slots 1 to 4 are drawn as gauges in two rows of two and slots 5 to 7 as text stats in one row below them

#### Scenario: 4x2
- **WHEN** a 4x2 stats widget has five slots
- **THEN** slots 1 and 2 are drawn as gauges side by side and slots 3 to 5 as text stats below them

#### Scenario: Fewer slots than positions
- **WHEN** a 4x4 stats widget has two slots
- **THEN** the first two gauge positions are drawn and the other positions are empty

#### Scenario: Only shown readings are subscribed to
- **WHEN** a 4x2 stats widget has seven slots
- **THEN** it declares the readings of slots 1 to 5

#### Scenario: Mixed kinds in the gauge rows
- **WHEN** a 4x4 stats widget shows a temperature and three percentages as gauges
- **THEN** all four values are drawn at the same text size

### Requirement: Gauge Motion
When the fraction a gauge shows changes, the fill MUST move from the fraction drawn at that moment to the new one
over about 300 milliseconds, slowing toward the end. The value text, the unit, the label and the colours change at
once.

- The widget reports that it is animating (see widget-sdk, "Animation Frames") only while a fill is moving
- A change that arrives while a fill is moving continues from the fraction drawn at that moment
- The first paint, and the first paint after the configuration changes, show every fill at rest
- A reading that receives its first value fills up from the empty track; a reading that becomes unavailable shows the
  empty track at once
- Plain positions never animate

#### Scenario: Value changes
- **WHEN** a gauge's reading goes from 20 to 60 percent
- **THEN** the number shows `60` at once, the fill grows from 20 to 60 percent over about 300 milliseconds, and the
  widget stops animating afterwards

#### Scenario: Nothing changes
- **WHEN** no shown value changes for a minute
- **THEN** the widget is not repainted and requests no animation frames

#### Scenario: Change during a move
- **WHEN** a second change arrives while the fill is halfway to the first target
- **THEN** the fill continues from where it is to the second target without a jump

#### Scenario: Snapshot
- **WHEN** a page with a gauge is rendered with `--snapshot`
- **THEN** the gauge is drawn at rest at its value

#### Scenario: Plain card
- **WHEN** a stats widget shows only plain positions and a value changes
- **THEN** it is repainted once and does not report that it is animating
