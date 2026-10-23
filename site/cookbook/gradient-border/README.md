A gradient border needs a border to colour, so the recipe sets `card.borderWidth` (0.012 of a cell, about 3 pixels). Without a
width the gradient is not drawn and `urdeck-cli theme validate` says so (`THEME050`).

- `card.borderGradient.colors` are the stops, two to eight, evenly spaced.
- `kind` `linear` runs across the card at `angle` degrees: 0 is left to right, 90 top to bottom, 135 from the top left corner to the
  bottom right one.
- `kind` `conic` goes once round the card's centre, starting at the right. That is the kind that can **spin** with
  `card.borderAnimate`, which is how a rotating spectrum border is made; see [motion](/docs/themes/motion/).

The gradient colours the border whether it is solid or dashed. The fill stays `colors.cardFill`, so keep it dark and opaque enough
for the text. Change the two stops to your accent colours and the card is yours.
