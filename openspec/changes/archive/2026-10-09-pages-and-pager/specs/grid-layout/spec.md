## ADDED Requirements

### Requirement: Reserved Bottom Band
The layout engine MUST be able to reserve a band at the bottom of the screen for the host's own elements (the page
indicator now, the dock later), and the grid MUST NOT extend into it:

- The band's height is a fraction of the column width, so it scales with the display and the user never sees a pixel
  size
- The grid area is the screen without the band. The number of whole rows is the grid area's height divided by the row
  height, rounded down
- The column width and the row height do not change when a band is reserved, and grid positions keep their meaning
- With no band the grid area is the whole screen, as before
- A partial row left over at the bottom is not a row; the band may use it

#### Scenario: Band on the tall panel
- **WHEN** the screen is 1100x3840 and a band of a quarter of a cell (about 69 pixels) is reserved
- **THEN** the grid area is 1100x3771, there are 13 whole rows of 275 pixels, and widgets stay where they were

#### Scenario: Band that costs a row
- **WHEN** the screen is 1000x1000, the column width is 250 and a band of 60 pixels is reserved
- **THEN** the grid area is 940 pixels tall and has 3 whole rows

#### Scenario: No band
- **WHEN** no band is reserved
- **THEN** the grid area is the whole screen

### Requirement: Widgets Must Fit The Grid Area
A widget whose cell rectangle does not lie entirely inside the grid area MUST NOT be placed. The layout engine MUST
log an error naming the widget and its position. The other widgets on the page are placed as usual. This follows
clamping of invalid columns, widths, rows and heights (see "Layout Validation").

#### Scenario: Widget runs into the band
- **WHEN** a page has a 4x2 widget at row 12 and the grid has 13 whole rows
- **THEN** the widget is not placed, an error is logged, and the other widgets are drawn

#### Scenario: Widget fits exactly
- **WHEN** a 4x1 widget is at the last whole row
- **THEN** it is placed

#### Scenario: Existing page
- **WHEN** a page whose widgets occupy rows 0 to 7 is shown on the 1100x3840 panel with a band reserved
- **THEN** every widget is placed where it was
