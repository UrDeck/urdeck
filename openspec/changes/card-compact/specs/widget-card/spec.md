## MODIFIED Requirements

### Requirement: Card Shape Is Page-Wide
Every card on a page MUST use the same shape and border width, whatever the widget's grid size, except that a compact card
uses the theme's compact values (see "Compact Cards"). The shape is a rounded rectangle (`card.shape` absent or `round`) or
a rectangle with cut corners (`chamfer`).

- A rounded card's corner radius is limited to half of the card's smaller side; a radius of 0 gives square cards
- A chamfered card cuts the corners named by `card.cutCorners` (top-right and bottom-left when absent) with a straight
  line, `card.cut` from the corner along each side, limited to half of the card's smaller side; its other corners are
  square and `card.radius` is not used. An empty `cutCorners` gives a square card
- Neither a widget nor a per-widget setting can change or remove the card

#### Scenario: Different widget sizes
- **WHEN** a 4x2 widget and a 2x1 widget are on the same page
- **THEN** both cards have the same corner radius and border width in pixels

#### Scenario: Flat tiles
- **WHEN** the theme's corner radius is 0
- **THEN** every card on the page is drawn with square corners

#### Scenario: Chamfered cards
- **WHEN** a theme sets `card.shape` to `chamfer`, `card.cut` to 0.145 and `card.cutCorners` to `["tr", "bl"]`
- **THEN** every card has its top-right and bottom-left corners cut by 0.145 of a cell along each side, square
  top-left and bottom-right corners, and the widget's content is clipped to that shape

#### Scenario: Cut larger than the card
- **WHEN** a chamfer's cut is larger than half of a card's smaller side
- **THEN** that card's corners are cut by half of its smaller side

#### Scenario: Theme without compact values
- **WHEN** a theme sets no `card.compact` and a page has a 4x2 and a 1x1 widget
- **THEN** both cards have the same corner radius and border width in pixels

## ADDED Requirements

### Requirement: Compact Cards
A card MUST be compact when it is a dock slot or a page card of grid size 1x1. A compact card uses the theme's
`card.compact` values in place of the page card's, each on its own; a value `card.compact` does not set is the page card's.

- `compact.radius`, `compact.borderWidth` and `compact.shape` (`round`, `chamfer` or `circle`) replace the page values
- `compact.accentStrip` and `compact.cornerMarks` are merged key by key over the page card's strip and marks: a key the
  compact group sets wins, the others come from the page card's group. With no page group, the compact group stands alone
- `circle` draws the card as a rounded rectangle whose corner radius is half its smaller side: a circle on a square card.
  On a card that is not square it is a capsule, and one information line per loaded theme says so
- The widget in a dock slot receives a theme whose corner radius and border width are the compact ones (a circle's radius
  is half the slot's card); a 1x1 widget on a page receives the page's theme. The padding does not change
- Everything else a compact card has (fill, tint, border style, highlight, what is drawn outside it) is drawn from its
  compact shape; tint and strip colours of dock slots count by slot position

#### Scenario: Circular dock buttons
- **WHEN** a theme sets `card.compact.shape` to `circle` and the dock shows four shortcuts
- **THEN** each dock card is a circle, its border follows the circle, and the icon tile inside it is as round as the
  widget's theme radius makes it

#### Scenario: Compact on a page
- **WHEN** a theme sets `card.compact.radius` to 0.24, its `card.radius` is 0.27, and a page shows a 1x1 and a 2x2 widget
- **THEN** the 1x1 card has a radius of 0.24 of a cell, the 2x2 card 0.27, and both widgets receive the page's theme

#### Scenario: Strip on tiles only
- **WHEN** a theme sets `card.compact.accentStrip` and no `card.accentStrip`
- **THEN** dock cards and 1x1 page cards have the strip and larger cards do not

#### Scenario: Partial marks
- **WHEN** a theme sets `card.cornerMarks` to brackets in yellow, 0.014 thick, and `card.compact.cornerMarks` to a tick at
  the bottom-left with its own size and inset
- **THEN** compact cards show only the tick at the bottom-left, 0.014 thick and yellow, and larger cards the brackets

#### Scenario: Circle with a chamfered page
- **WHEN** a theme sets `card.shape` to `chamfer` and `card.compact.shape` to `circle`
- **THEN** page cards larger than 1x1 are chamfered and compact cards are circles
