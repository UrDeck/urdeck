## ADDED Requirements

### Requirement: Themed Icon
The SDK MUST provide a themed icon component that draws a glyph of the theme's icon set into a rectangle and tells the caller
whether it drew one, so that every widget that shows a glyph does it alike.

- The widget passes the rectangle, the key (`weather/rain`), an optional colour that overrides the theme's tint and, for the
  `ramp` tint, a position from 0 to 1 along the theme's ramp (0.5 when it passes none)
- The glyph is drawn in the largest centred square that fits the rectangle; stroke width is the theme's stroke times the
  square's side, caps and joins are round unless the theme's stroke cap is square, and shapes marked as filled by the file
  are filled with the same colour
- The colour is the widget's override when given; else the theme's tint: `accent`, `accent2`, `text`, a fixed colour, or
  the ramp's colour at the position; `none` draws in the text colour
- With a glow, a blurred copy of the glyph is drawn behind it, in the glow's colour or else the tint, at the glow's opacity;
  the copy is cached per key, size and colour and the cache is disposed with the theme
- It returns `true` when it drew a glyph and `false`, drawing nothing, when the theme's icon lookup has no icon for the key
- The component reads no file, parses no text and does not own anything it is given; a rectangle with no width or height
  draws nothing and returns `false`
- The component names no colour, font or size of its own

#### Scenario: A glyph
- **WHEN** a widget draws `weather/rain` into a 96 pixel square under a theme with tint `accent`
- **THEN** the glyph is stroked in the accent colour 6.4 pixels wide, centred, and the call returns `true`

#### Scenario: Wide rectangle
- **WHEN** the rectangle is 200 by 100 pixels
- **THEN** the glyph is a 100 pixel square centred in it

#### Scenario: Ramp tint
- **WHEN** the tint is `ramp` and the widget passes the position 1
- **THEN** the glyph is drawn in the last colour of the theme's ramp

#### Scenario: Override
- **WHEN** the widget passes a colour
- **THEN** the glyph is drawn in that colour whatever the tint

#### Scenario: Glow
- **WHEN** the theme sets a glow and the same glyph is drawn twice at the same size and colour
- **THEN** the glow is blurred once and drawn from the cache the second time

#### Scenario: No icon
- **WHEN** the lookup has no icon for the key
- **THEN** nothing is drawn and the call returns `false`

## MODIFIED Requirements

### Requirement: Image Tile
The SDK MUST provide an image tile component that draws an image, an optional label and, when there is no image, a
glyph or a placeholder, so that every widget that shows an icon or a picture does it alike.

- The widget passes the rectangle, the image or none, an optional label text, an optional placeholder text and an optional
  glyph key
- The image is scaled to fit the rectangle with its aspect ratio kept and is centred; it is drawn with smooth
  sampling and keeps its own colours and transparency (the theme does not recolour it)
- An image is never enlarged to more than twice its own pixel size; a smaller image is drawn at twice its size,
  centred
- With a label, the label is drawn as a text line at the label step in the muted text colour, centred below the
  image, and the image takes the space that remains above it; without one no space is reserved
- With no image and a glyph key that the theme's icon lookup answers, the glyph is drawn where the image would be, with the
  themed icon component (see "Themed Icon") in the area the placeholder square would take, and no placeholder is drawn
- With no image and no glyph, or a glyph key the lookup does not answer, a placeholder is drawn where the image would be: a
  rounded square in the theme's dimmed accent colour, with the first letter or digit of the placeholder text, upper-cased,
  in the theme's value role and text colour; with no placeholder text the square is drawn empty
- The component holds no state between paints and does not own the image
- The component names no colour, font or size of its own, and draws no card and no pressed state

#### Scenario: Square icon in a square rectangle
- **WHEN** a widget draws a 256 pixel icon into a 200 pixel square
- **THEN** the icon fills the square, drawn smoothly, with its transparent areas left untouched

#### Scenario: Wide image
- **WHEN** a widget draws an image twice as wide as it is high into a square rectangle
- **THEN** the image spans the width, is centred vertically, and nothing is drawn outside the rectangle

#### Scenario: Small image
- **WHEN** a widget draws a 48 pixel image into a 240 pixel square
- **THEN** the image is drawn 96 pixels wide in the centre

#### Scenario: With a label
- **WHEN** a widget draws an image with the label `Steam`
- **THEN** `Steam` is drawn below the image at the theme's label size in the muted colour and the two do not overlap

#### Scenario: Placeholder
- **WHEN** a widget draws the tile with no image, no glyph and the placeholder text `homeassistant`
- **THEN** a rounded square in the dimmed accent colour with the letter `H` is drawn

#### Scenario: Glyph instead of the placeholder
- **WHEN** a widget draws the tile with no image and the glyph key `app/link`
- **THEN** the link glyph is drawn in the theme's icon tint and no placeholder square is drawn

#### Scenario: Glyph key the theme does not answer
- **WHEN** a widget draws the tile with no image, the glyph key `app/nosuch` and the placeholder text `homeassistant`
- **THEN** the placeholder with the letter `H` is drawn

#### Scenario: Theme decides the look
- **WHEN** the active theme changes
- **THEN** the placeholder, the glyph and the label use the new theme's colours, font, stroke and label size
