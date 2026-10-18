## ADDED Requirements

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
