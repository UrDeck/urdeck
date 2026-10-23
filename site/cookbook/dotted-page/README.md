`pattern` is drawn once over the whole window, above the background and under the cards, the dock and the page indicator. It does
not slide with the pages and it is drawn whatever the configuration's `background` is, because it belongs to the theme.

- `kind` is `dots`, `grid`, `hatch` or `scanlines`. Dots are round with a radius of `thickness`.
- `spacing` and `thickness` are fractions of a grid cell. Marks sit on whole pixels, so the spacing is rounded and a line is never
  under one pixel wide. Marks closer than three pixels are reported (`THEME052`).
- `color` has its alpha first: `#33ffffff` is white at 20 %. Without a colour the pattern is the muted text colour at a sixteenth
  of its opacity, which is why the built-in themes look so quiet.

The card fill is slightly translucent (`#e61a2238` is 90 %), so a little of the pattern shows through the cards. Make it opaque for
a clean card, or change `kind` to `grid` and add `majorEvery` for a drafting sheet.
