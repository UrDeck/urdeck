`card.shape` is `round` by default, a rounded rectangle of `card.radius`. `chamfer` replaces the rounded corners with straight
cuts, and `card.radius` stops mattering: the corners that are not cut are square.

- `card.cut` is how long the cut is along each side, a fraction of a grid cell. It is limited to half of the card's smaller side,
  so a small card never gets a cut larger than itself.
- `card.cutCorners` names the corners to cut: `tl`, `tr`, `br`, `bl`. `["tr", "bl"]` is the diagonal pair that gives the look; an
  empty list gives square cards.
- The border follows the cut. `colors.cardBorder` is transparent in the default theme, so the recipe sets a colour and a
  `card.borderWidth` of 0.008 of a cell (about 2 pixels on the panel it was made for). A border of 0 draws nothing.
- The widget's content is clipped to the shape, so a clock or a gauge never pokes out of a cut corner.

The fill is opaque on purpose: the text is measured against it, and `urdeck-cli theme validate` reports any pair under 4.5:1.
Try `"cut": 0.2` for a sharper look, or all four corners for an octagon.
