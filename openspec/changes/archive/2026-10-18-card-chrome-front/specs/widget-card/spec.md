## ADDED Requirements

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
