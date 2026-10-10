# Grid Layout Specification

## Purpose

Defines the resolution-agnostic 4-column grid UrDeck uses to place and size widgets on any monitor.
## Requirements
### Requirement: 4-Column Grid System
The layout engine MUST provide a deterministic 4-column grid system where:

- All widgets are placed on a grid with exactly 4 columns
- Widgets can span 1-4 columns in width (integer values only)
- Widgets can span 1-N rows in height (integer values only)
- Each column has equal width: `ColumnWidth = monitorWidth / 4`
- Each row has equal height: `RowHeight = ColumnWidth` (square grid cells)
- Widget positions are in grid units, not pixels
- A widget's cell rectangle is the block of grid cells it spans; its card rectangle (see Card Rectangle) lies inside it

#### Scenario: Grid is square-cell based
- **WHEN** a monitor is 1100 pixels wide
- **THEN** each grid column is 275 pixels wide and each row is 275 pixels tall

#### Scenario: Widget spans multiple columns
- **WHEN** a widget is configured with `width: 4` on an 1100px monitor
- **THEN** the widget's cell rectangle spans the full monitor width (1100 pixels)

#### Scenario: Widget position stored in grid units
- **WHEN** a widget is placed at `col: 0, row: 0, width: 2, height: 1`
- **THEN** the widget's cell rectangle at 1100px monitor is at `(0, 0)` with size `(550, 275)`

### Requirement: Grid-to-Pixel Conversion
The layout engine MUST convert grid units to the pixel coordinates of the widget's cell rectangle at render time based
on the active monitor's current dimensions:

- `PixelX = Col * ColumnWidth`
- `PixelY = Row * RowHeight`
- `PixelWidth = Width * ColumnWidth`
- `PixelHeight = Height * RowHeight`

#### Scenario: Conversion for 3840×2160 monitor
- **WHEN** a monitor is 3840×2160 and a widget has `col: 1, row: 2, width: 2, height: 3`
- **THEN** `ColumnWidth = RowHeight = 960`, so the widget's cell rectangle is at `(960, 1920)` with size `(1920, 2880)`

#### Scenario: Conversion for 7680×2160 monitor (triple 4K)
- **WHEN** a monitor is 7680×2160 and a widget has `col: 0, row: 0, width: 4, height: 2`
- **THEN** `ColumnWidth = RowHeight = 1920`, so the widget's cell rectangle is at `(0, 0)` with size `(7680, 3840)`

### Requirement: Monitor-Aware Layout Manager
The layout engine MUST be resolution-agnostic and cheap to recompute when the target monitor changes:

- `GridLayoutManager` is constructed from (and `RenderWidgetLayout` recomputes for) a screen size; it holds no monitor-specific state
- The host detects monitor changes (hot-plug, resolution change, retargeting via config) and rebuilds the layout from the new window size, recalculating grid dimensions and widget pixel positions
- Layout is computed from the window size in device-independent pixels; per-widget surfaces render at the monitor's physical resolution
- Does NOT persist monitor-specific pixel positions (only grid-unit positions are persisted)

Note: `GridLayoutManager` declares a `MonitorChanged` event that is not raised; monitor-change handling lives in the host, which simply rebuilds the layout.

#### Scenario: Monitor resolution changes
- **WHEN** the monitor resolution changes from 1100×3840 to 1100×2160
- **THEN** grid column width remains 275 pixels but the number of rows decreases

#### Scenario: Target monitor changes
- **WHEN** the configured target monitor changes to one with a different resolution (e.g. 1080×1920)
- **THEN** the host recalculates column width and every widget's position and size for the new monitor

### Requirement: Layout Validation
The layout engine MUST validate widget placement during layout computation. Invalid values are clamped (never rejected) and a warning is logged:

- A widget's `Width` must be between 1 and 4 (clamped first)
- A widget's `Col` must be ≥ 0 and `Col + Width` ≤ 4 (clamped to `4 - Width` after width is clamped)
- A widget's `Row` must be ≥ 0
- A widget's `Height` must be ≥ 1

#### Scenario: Widget would overflow horizontal bound
- **WHEN** a widget is placed at `col: 3, width: 2`
- **THEN** the layout engine logs a warning and clamps the position to `col: 2, width: 2`

#### Scenario: Negative row
- **WHEN** a widget is placed at `row: -1`
- **THEN** the layout engine logs a warning and clamps to `row: 0`

### Requirement: Card Rectangle
The layout engine MUST produce, for each widget, a card rectangle: the widget's cell rectangle inset on every side by
half of the theme's gap. The gap is a fraction of the column width, so it scales with the display. The card rectangle
is where the widget's surface is placed and is the size the widget draws at. A gap of 0 makes the card rectangle equal
to the cell rectangle.

#### Scenario: Gap between neighbouring cards
- **WHEN** the gap is 0.04 of the column width on an 1100 pixel wide monitor (11 pixels)
- **THEN** two horizontally adjacent 1x1 widgets have 11 pixels between their cards, and each card is 5.5 pixels from
  its cell's edges

#### Scenario: Gap does not depend on widget size
- **WHEN** a 4x2 widget sits directly above a 1x1 widget
- **THEN** the space between their cards equals the space between two adjacent 1x1 cards

#### Scenario: Zero gap
- **WHEN** the theme's gap is 0
- **THEN** each widget's card rectangle equals its cell rectangle

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

