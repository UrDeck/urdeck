`card.shadow` is the card's shape moved by `x` and `y` (fractions of a cell), grown by `spread` and blurred by `blur`. A `blur` of 0
is a hard edge, which is the whole trick: the shadow looks like a second, black card behind the first.

- The shadow is drawn **under all the cards of the page**, so it never covers a neighbour's content. Its reach, here 0.05 of a
  cell, has to stay inside `card.gap` (0.11), or it would run under the next card. When a card fill is translucent the deck
  shortens the reach to the gap and logs it once, and `theme validate` warns first (`THEME054`); an opaque fill keeps the
  shadow whole.
- The border and the shadow share one colour here (`#111111`). `card.borderWidth` of 0.02 is about 5 pixels on the panel.
- A light theme needs more than the card: the page indicator is white by default, so `indicator` gives it dark colours, and the
  `colors` for text, muted text and accent are dark too. Set `meta.mode` to `light` so an editor can group the theme.

Try `"blur": 0.03` for a soft shadow, or a negative `x` for a shadow to the left. A pressed card's shadow shrinks and dims with it.
