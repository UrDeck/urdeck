## MODIFIED Requirements

### Requirement: Reserved Bottom Band
The layout engine MUST be able to reserve space at the bottom of the screen for the host's own elements, and the grid
MUST NOT extend into it. Two bands can be reserved, each on its own or both: the dock at the very bottom of the
screen, and the page indicator directly above it (or at the very bottom when there is no dock).

- Each band's height is a fraction of the column width, so it scales with the display and the user never sees a pixel
  size
- The grid area is the screen without the reserved bands. The number of whole rows is the grid area's height divided
  by the row height, rounded down
- The column width and the row height do not change when a band is reserved, and grid positions keep their meaning
- With no band the grid area is the whole screen, as before
- A partial row left over at the bottom is not a row; the bands may use it

#### Scenario: Band on the tall panel
- **WHEN** the screen is 1100x3840 and a band of a quarter of a cell (about 69 pixels) is reserved
- **THEN** the grid area is 1100x3771, there are 13 whole rows of 275 pixels, and widgets stay where they were

#### Scenario: Band that costs a row
- **WHEN** the screen is 1000x1000, the column width is 250 and a band of 60 pixels is reserved
- **THEN** the grid area is 940 pixels tall and has 3 whole rows

#### Scenario: No band
- **WHEN** no band is reserved
- **THEN** the grid area is the whole screen

#### Scenario: Dock and indicator on the tall panel
- **WHEN** the screen is 1100x3840, a dock band of 0.71 of a cell (195 pixels) and an indicator band of a quarter of a
  cell (69 pixels) are reserved
- **THEN** the dock is the bottom 195 pixels, the indicator's band is the 69 pixels above it, the grid area is
  1100x3576 and there are still 13 whole rows

#### Scenario: Dock alone
- **WHEN** a dock band is reserved and no indicator band
- **THEN** the grid area is the screen without the dock band
