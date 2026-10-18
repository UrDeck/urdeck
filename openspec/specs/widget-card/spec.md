# Widget Card Specification

## Purpose

Defines the card drawn behind every widget: who draws it, what shape it has and how widget content relates to it, so
that every widget on a page shares one card shape that only the theme controls.
## Requirements
### Requirement: Host-Drawn Card
The host MUST draw a card for every placed widget before the widget renders, using the theme's card values: its shape,
its fill (the card fill, a fill gradient or the card's tint), its border (width, colour or gradient, solid or dashed) and
its corner radius. The card covers the widget's whole surface: the surface and the card are the same rectangle. Widgets
MUST NOT need to draw their own background or card.

#### Scenario: Widget draws only content
- **WHEN** a widget's render draws nothing
- **THEN** its place on the page shows an empty card in the theme's card fill

#### Scenario: Theme changes the card
- **WHEN** the same widget renders under two themes with different card fills and corner radii
- **THEN** its card differs accordingly without any change to the widget

### Requirement: Card Shape Is Page-Wide
Every card on a page MUST use the same shape and border width, whatever the widget's grid size. The shape is a rounded
rectangle (`card.shape` absent or `round`) or a rectangle with cut corners (`chamfer`).

- A rounded card's corner radius is limited to half of the card's smaller side; a radius of 0 gives square cards
- A chamfered card cuts the corners named by `card.cutCorners` (top-right and bottom-left when absent) with a straight
  line, `card.cut` from the corner along each side, limited to half of the card's smaller side; its other corners are
  square and `card.radius` is not used. An empty `cutCorners` gives a square card
- Neither a widget nor a per-widget setting can change or remove the card

#### Scenario: Different widget sizes
- **WHEN** a 4x2 widget and a 1x1 widget are on the same page
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

### Requirement: Content Is Clipped To The Card
Widget content MUST be clipped to the card's shape, including its rounded corners, so a widget that fills its whole
surface still shows the card's shape. A widget MAY draw over the card fill up to the card's edge.

#### Scenario: Edge-to-edge content
- **WHEN** a widget fills its entire surface with an opaque colour under a theme with rounded corners
- **THEN** the result has the card's rounded corners and nothing is drawn outside them

### Requirement: Themed Error State
When a widget's render fails, the host MUST show the failure inside that widget's card, in the card's shape and with all
of the card's chrome, using the theme's critical colour, with the widget name and the error message. Nothing the widget
drew before it failed stays visible. Other widgets are unaffected and the failure is logged.

#### Scenario: Widget render throws
- **WHEN** a widget throws during render
- **THEN** its card shows the widget name and message in the theme's critical colour, clipped to the card shape

#### Scenario: Error on a chamfered gradient card
- **WHEN** a widget throws during render under a theme with a chamfered shape and a fill gradient
- **THEN** the error is shown on a chamfered card with the gradient fill and the border, as the card would be without
  the error

### Requirement: Same Card Everywhere
The card MUST look the same in the live window and in a snapshot rendered without a screen: its position, size, shape,
fill, border and everything the theme draws on it or around it. In a snapshot nothing a card's chrome or content draws
reaches past the card's rectangle except what the theme draws outside the card.

#### Scenario: Snapshot matches the window
- **WHEN** a page is rendered with `--snapshot` at the live window's pixel size
- **THEN** each card's position, size, fill, border and corner radius match the live window

#### Scenario: Format 2 chrome in a snapshot
- **WHEN** a page under a theme with a chamfer, a fill gradient and a dashed border is rendered with `--snapshot`
- **THEN** each card's shape, fill and border match the live window's

### Requirement: Card Fill
A card's fill MUST be, in this order of precedence: the theme's fill gradient (`card.fillGradient`), the card's tint
(`card.tint` `cycle`), the theme's card fill colour.

- A fill gradient is linear across the card at its angle (0 runs left to right, 90 top to bottom), from its first colour
  to its last, evenly spaced
- With `tint` `cycle` and `colors.cardTints` set, a card is filled with the tint at its index, counting round the list.
  A card's index is its place among its page's placed and visible cards, ordered by grid row and then by grid column,
  starting at 0 on every page; a hidden widget or one that is not placed takes no index. A dock slot's index is its
  position in the dock, whether or not the slots before it show a widget. Without `colors.cardTints` the card fill is used
- The text colour does not change with the tint

#### Scenario: Gradient fill
- **WHEN** a theme sets `card.fillGradient` to two colours at angle 90
- **THEN** every card is filled from the first colour at its top edge to the second at its bottom edge, and the card
  fill colour is not used

#### Scenario: Tints in page order
- **WHEN** a theme sets `card.tint` to `cycle` with three `cardTints` and a page has four visible cards, one in each of
  two rows of two
- **THEN** the cards of the first row take the first and second tints from left to right, the second row's left card the
  third, and its right card the first again

#### Scenario: A hidden widget takes no tint
- **WHEN** the second of three cards in page order is hidden
- **THEN** the remaining cards take the first and second tints

#### Scenario: Every page starts at the first tint
- **WHEN** a configuration with two pages is shown under a theme with `tint` `cycle`
- **THEN** the first card of each page has the first tint

#### Scenario: Dock slots by position
- **WHEN** the dock's second entry shows no widget and its third does
- **THEN** the third slot's card takes the third tint

### Requirement: Card Border Styles
A card's border MUST be drawn inside the card's edge, for every shape, in the theme's border width, in this style:

- `card.borderStyle` `solid` (the default) draws a continuous line; `none` draws no border
- `dashed` draws dashes and gaps of `card.borderDash` (`on` and `off`; three and two border widths when absent),
  following the shape round its corners. The dash length and the gap are scaled together by the same factor, so that a
  whole number of periods fits the border's length and no corner shows a short dash or a long gap
- `card.borderGradient` colours the border, solid or dashed, with a gradient instead of the border colour: `linear` runs
  across the card at its angle as a fill gradient does; `conic` sweeps once round the card's centre, starting at the
  right. The gradient does not move
- A border width of 0 draws no border, whatever its style or gradient

#### Scenario: Dashed square card
- **WHEN** a theme sets `card.borderStyle` to `dashed` and a border width of 2 pixels on square cards
- **THEN** each card's border is a dashed line inside its edge whose dashes meet each corner the same way

#### Scenario: Dashed chamfer
- **WHEN** a theme with chamfered cards sets `card.borderStyle` to `dashed`
- **THEN** the dashes follow the cut corners without a gap in the line's course

#### Scenario: Conic border
- **WHEN** a theme sets `card.borderGradient` with `kind` `conic` and six colours
- **THEN** each card's border runs through the six colours once round the card and does not change between repaints

### Requirement: Hairlines Are Whole Pixels
A line width of a format 2 card style that resolves to 2 pixels or less MUST be drawn at a whole number of pixels, rounded
to the nearest and never less than 1. One warning per key per loaded theme says when a width resolves under 1 pixel. A
version 1 card (one that uses no card value of format 2) keeps its border width unrounded.

#### Scenario: A thin border is crisp
- **WHEN** a theme with a format 2 card value sets a border width that resolves to 2.5 pixels on one panel and 1.6 on
  another
- **THEN** the border is 2.5 pixels wide on the first and 2 on the second

#### Scenario: Under a pixel
- **WHEN** a format 2 card's border width resolves to 0.6 pixels
- **THEN** it is drawn 1 pixel wide and one warning names the theme and `card.borderWidth`

#### Scenario: Version 1 border
- **WHEN** `glass` is shown in the dock, where its border width resolves to about 1.6 pixels
- **THEN** the border is drawn at that width, as before

### Requirement: Version 1 Cards Are Unchanged
A theme that uses no card value of format 2 MUST draw its cards exactly as before card chrome existed: the same pixels,
in the window and in a snapshot.

#### Scenario: Built-in themes
- **WHEN** a page is rendered under `default-dark`, `default-light` or `glass`
- **THEN** the picture is identical to the one before this requirement

### Requirement: Card Chrome Does Not Slow Repaints
A widget's repaint under any theme MUST cost at most 1.5 times the repaint of the same widget under a version 1 theme of
the same card size, measured on the engine's software raster. What a card's chrome needs that does not change between
repaints (its shape, its gradients, a fill with a gradient) is made once for each card size when the theme or the layout
changes, not on each repaint.

#### Scenario: Gradient card repaint
- **WHEN** a 4x2 card under a theme with a fill gradient and a gradient border repaints with unchanged content
- **THEN** the repaint takes at most 1.5 times that of the same card under `default-dark`

#### Scenario: Nothing is made per repaint
- **WHEN** a widget repaints a hundred times without a change of theme or layout
- **THEN** the gradient fill is drawn into an image once for its card size, not a hundred times

### Requirement: Edge Light And Sheen
A theme MAY light its cards from inside. The host MUST draw:

- `card.highlight.top` and `card.highlight.bottom`: a line of `highlight.thickness` just inside the card's top or bottom
  edge, following the shape round its corners, in its colour, fading to transparent over the first and the last eighth of
  the card's width. The highlight is drawn after the widget's content, so content never covers it
- `card.sheen`: a radial light centred at `x` times the card's width and `y` times its height, reaching `size` times the
  card's width, from its colour at the centre to transparent. The sheen is drawn over the fill and under the content

Both are clipped to the card's shape.

#### Scenario: Top highlight over content
- **WHEN** a theme sets `card.highlight.top` and a widget fills its whole card with an opaque colour
- **THEN** the highlight line is visible along the top edge over the widget's colour, and fades out towards both ends

#### Scenario: Sheen
- **WHEN** a theme sets `card.sheen` with `x` 0.05, `y` 0 and `size` 0.7
- **THEN** each card is lightest near its top-left corner, and the light is gone at 0.7 of the card's width from there

### Requirement: Accent Strip
A theme MAY draw a lit line along one edge of every card (`card.accentStrip`). The host MUST draw it inside the card along
`side`, `thickness` wide and `length` times the side long, centred on the side, clipped to the card's shape, over the fill
and under the content.

- Its colour is the entry of `accentStrip.colors` at the card's index (as the card fill's tint counts it), counting round
  the list; the theme's accent when `colors` is absent
- With `fade`, it fades from its colour at the start of its length to transparent at its end
- With `glow`, a blurred copy of the strip in the strip's colour (or the glow's own colour) at the glow's alpha lies under
  it, inside the card

#### Scenario: Carbon's scan line
- **WHEN** a theme sets `card.accentStrip` with `side` `bottom`, `length` 1, one colour and `fade`
- **THEN** every card has a line along its bottom edge in that colour, strongest at the left and gone at the right

#### Scenario: Colours by card
- **WHEN** a theme sets `card.accentStrip.colors` to four colours on a page of five cards
- **THEN** the cards take the four colours in order and the fifth card the first again

### Requirement: Corner Marks
A theme MAY mark its cards' corners (`card.cornerMarks`). The host MUST draw a mark at each corner in `corners` (top-left
and bottom-right when absent), in its colour (the theme's accent when absent), after the widget's content, so content
never covers it.

- `bracket` draws two lines from the corner, one along each edge, each `size` long; `tick` draws one line from the corner
  along the card's top or bottom edge, `size` long
- `thickness` is the lines' width; `inset` is the distance from the card's edge to the mark's outer edge, inward when
  positive. A negative inset puts the mark partly or wholly outside the card
- The part of a mark that lies on the card is drawn on the card, whatever the card's shape; the part outside the card is
  drawn with what the theme draws outside cards

#### Scenario: Brackets on the card
- **WHEN** a theme sets `card.cornerMarks` to `bracket` with a positive `inset` and a widget fills its card with an opaque
  colour
- **THEN** the brackets are visible at the top-left and bottom-right corners over the widget's colour

#### Scenario: A mark across the edge
- **WHEN** a theme sets `card.cornerMarks.inset` to minus half its `thickness`
- **THEN** half of each mark's line width lies on the card and is drawn over the content

### Requirement: Drawn Outside The Card
A theme MAY draw outside its cards: a shadow (`card.shadow`), a glow (`card.glow`) and the part of a corner mark that lies
outside the card. The host MUST draw these under every card of the page, so they never cover any card's content, and MUST
cut each card's own shape out of its own shadow and glow, so a translucent card does not show them through its fill.

- A shadow is the card's shape moved by `x` and `y`, grown by `spread` on every side, blurred by `blur` (0 is a hard edge),
  in its colour
- A glow is the card's shape blurred outwards by `blur`, in its colour (the border colour when absent) at its alpha
- The part of a corner mark outside the card is drawn exactly where the mark's part on the card continues it
- Nothing of this takes input: a tap or a press is decided by the cards alone

#### Scenario: Hard shadow
- **WHEN** a theme sets `card.shadow` with `x` and `y` 0.055 and `blur` 0 in black
- **THEN** each card has a sharp black copy of its shape offset down and to the right, visible beside the card and under
  no card's content

#### Scenario: Soft shadow on an opaque card
- **WHEN** a theme with an opaque card fill sets a soft shadow that reaches further than the gap
- **THEN** the shadow shows in the gaps and round the page's outer cards, and the neighbours' content is unaffected

#### Scenario: Marks outside the card
- **WHEN** a theme sets `card.cornerMarks` with an `inset` of minus 0.045 and a thickness of 0.014
- **THEN** each mark is drawn whole, in the gap beside its corner

#### Scenario: A mark across the edge
- **WHEN** a mark lies half on the card and half outside it
- **THEN** it is drawn as one unbroken mark

### Requirement: Reach On Translucent Cards
When any of a theme's card fill colours (`colors.cardFill`, a `card.fillGradient` colour or a `colors.cardTints` entry) is
not fully opaque, the host MUST limit how far a card's shadow and glow reach past its edge to the gap between cards,
scaling the shadow's offset and blur, and the glow's blur, down together. One information line per loaded theme says that
the reach was limited and to what. A theme whose card fills are all opaque keeps its full reach.

#### Scenario: Prism's shadow
- **WHEN** a theme with translucent cards sets a shadow that reaches 193 pixels on a page whose gap is 25 pixels
- **THEN** the shadow reaches 25 pixels past each card, no card shows a neighbour's shadow through its fill, and one
  information line is logged

#### Scenario: Opaque cards keep their shadow
- **WHEN** a theme with opaque cards sets a shadow that reaches 95 pixels with a gap of 25
- **THEN** the shadow reaches 95 pixels

### Requirement: Outside The Card Costs Nothing At Rest
What is drawn outside the cards MUST be made when the layout or the theme changes and MUST NOT be redrawn when a widget
repaints, the readings change or a page slides. For one page it MUST take at most 20 ms to make once the process is warm,
and the pictures of a theme, which all its pages share, MUST take at most 10 MB of memory. A theme that draws nothing outside
its cards creates nothing for it.

#### Scenario: Clocks ticking
- **WHEN** a page with shadows shows three clocks that repaint every second for a minute
- **THEN** the shadows are made once and not again

#### Scenario: Version 1 theme
- **WHEN** a version 1 theme is shown
- **THEN** no element, picture or sprite exists for the outside of the cards

