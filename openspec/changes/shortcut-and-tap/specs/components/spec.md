## ADDED Requirements

### Requirement: Image Tile
The SDK MUST provide an image tile component that draws an image, an optional label and, when there is no image, a
placeholder, so that every widget that shows an icon or a picture does it alike.

- The widget passes the rectangle, the image or none, an optional label text and an optional placeholder text
- The image is scaled to fit the rectangle with its aspect ratio kept and is centred; it is drawn with smooth
  sampling and keeps its own colours and transparency (the theme does not recolour it)
- An image is never enlarged to more than twice its own pixel size; a smaller image is drawn at twice its size,
  centred
- With a label, the label is drawn as a text line at the label step in the muted text colour, centred below the
  image, and the image takes the space that remains above it; without one no space is reserved
- With no image, a placeholder is drawn where the image would be: a rounded square in the theme's dimmed accent
  colour, with the first letter or digit of the placeholder text, upper-cased, in the theme's value role and text
  colour; with no placeholder text the square is drawn empty
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
- **WHEN** a widget draws the tile with no image and the placeholder text `homeassistant`
- **THEN** a rounded square in the dimmed accent colour with the letter `H` is drawn

#### Scenario: Theme decides the look
- **WHEN** the active theme changes
- **THEN** the placeholder and the label use the new theme's colours, font and label size
