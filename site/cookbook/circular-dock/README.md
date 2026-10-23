A dock slot and a page card of one grid cell are **compact**. `card.compact` overrides the page card's values for them, each on its
own, and a value it does not set is the page card's.

- `compact.shape` `circle` is a rounded card whose radius is half its smaller side: a circle on a square card. A card that is not
  square would become a capsule (`THEME056` remarks on it), but a dock slot is a square.
- `compact.borderWidth` gives the compact cards a ring while `card.borderWidth` of 0 leaves the larger cards without a border.
  The ring's colour is `colors.cardBorder`.
- `dock.height` is the height of the dock's band and the side of a slot. 0.71 is what the built-in themes use: on the 1100x3840
  panel the dock fits in the space under the thirteenth row, so no row is lost.

`compact.radius` and `compact.accentStrip` can change the look further, and a 1x1 widget on a page becomes a circle too, so keep
that in mind when you place a clock or a single stat.
