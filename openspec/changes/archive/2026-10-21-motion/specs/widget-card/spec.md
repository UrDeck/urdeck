## MODIFIED Requirements

### Requirement: Card Border Styles
A card's border MUST be drawn inside the card's edge, for every shape, in the theme's border width, in this style:

- `card.borderStyle` `solid` (the default) draws a continuous line; `none` draws no border
- `dashed` draws dashes and gaps of `card.borderDash` (`on` and `off`; three and two border widths when absent),
  following the shape round its corners. The dash length and the gap are scaled together by the same factor, so that a
  whole number of periods fits the border's length and no corner shows a short dash or a long gap
- `card.borderGradient` colours the border, solid or dashed, with a gradient instead of the border colour: `linear` runs
  across the card at its angle as a fill gradient does; `conic` sweeps once round the card's centre, starting at the
  right. The gradient does not move unless the border is animated (see "Animated Border")
- A border width of 0 draws no border, whatever its style or gradient

#### Scenario: Dashed square card
- **WHEN** a theme sets `card.borderStyle` to `dashed` and a border width of 2 pixels on square cards
- **THEN** each card's border is a dashed line inside its edge whose dashes meet each corner the same way

#### Scenario: Dashed chamfer
- **WHEN** a theme with chamfered cards sets `card.borderStyle` to `dashed`
- **THEN** the dashes follow the cut corners without a gap in the line's course

#### Scenario: Conic border
- **WHEN** a theme sets `card.borderGradient` with `kind` `conic` and six colours, and no `borderAnimate`
- **THEN** each card's border runs through the six colours once round the card and does not change between repaints

## ADDED Requirements

### Requirement: Animated Border
When a theme sets `card.borderAnimate` (or `card.compact.borderAnimate` for compact cards), the deck MUST animate the
border of every card that style applies to, on the page and in the dock:

- `spin` turns a `conic` border gradient round the card's centre, one turn per `period` seconds, clockwise. Without a
  conic gradient there is no spin (`theme-lint`, `THEME051`) and the border is drawn still
- `pulse` changes the border's opacity between 1 and 0.55 and back once per `period` seconds; it works with a solid colour,
  a linear or a conic gradient
- `none`, or no `borderAnimate`, draws the border still
- The animation advances in steps, 15 per second, a constant of the host, not a theme value. The card's own surface MUST
  NOT be repainted for the animation: the border is drawn over the card by the system compositor
- The animated border keeps the shape, width and colours of the still border: at angle 0 and full opacity it is the still
  border, pixel for pixel within anti-aliasing
- It moves with the card when the card is pressed or the page slides, and it takes no pointer input
- It runs only while the effective motion level is `full` (see `motion`), the display is on and the deck is shown. At
  `reduced` or `off` the border is drawn still, as without `borderAnimate`. While the display is off or the deck is hidden
  it is paused and produces no frames
- A `dashed` border is not animated: it is drawn still and the theme gets `THEME057`
- A snapshot draws an animated border still, at angle 0 and full opacity
- One information line is logged when animated borders start or stop, with the mode, the period, the number of cards and
  the reason (`motion level`, `display off`, `deck hidden`, `page rebuilt`)

#### Scenario: Spin
- **WHEN** a theme has a six-colour conic border gradient and `borderAnimate` `spin` with a period of 7, and the effective
  motion level is `full`
- **THEN** every card's border colours turn once round the card in 7 seconds, and the cards' contents are not repainted
  for it

#### Scenario: Pulse
- **WHEN** a theme has a solid border and `borderAnimate` `pulse` with a period of 4
- **THEN** every card's border fades to 0.55 opacity and back to full every 4 seconds

#### Scenario: Reduced motion
- **WHEN** the effective motion level is `reduced` and the theme has `borderAnimate` `spin`
- **THEN** the border is drawn as the same theme without `borderAnimate` draws it, and nothing turns

#### Scenario: Display off
- **WHEN** the display is switched off while borders turn
- **THEN** no animation frames are produced until the display is on again, when the borders turn again

#### Scenario: Pressed card
- **WHEN** a card with a turning border is pressed
- **THEN** the border scales with the card and keeps turning

#### Scenario: Snapshot
- **WHEN** a snapshot is taken of a theme with `borderAnimate` `spin`
- **THEN** the picture is the same as the same theme without `borderAnimate`

#### Scenario: Dashed and animated
- **WHEN** a theme has a dashed border and `borderAnimate` `pulse`
- **THEN** the dashed border is drawn still and the theme's findings include `THEME057`

### Requirement: Animated Border Cost
On the reference panel (1100x3840) with eight cards with turning borders, the animated borders MUST cost at most 5 % of one
core and 8 MB of private memory over the same page with still borders, measured as the spike S2 was
(`docs/perf/theme-look.md`). With the motion level below `full`, a theme with `borderAnimate` MUST cost the same as the
same theme without it.

#### Scenario: Measured on the panel
- **WHEN** the Rigline fixture is shown on the panel with eight cards for a minute
- **THEN** the process uses at most 5 % of one core and 8 MB more private memory than with `borderAnimate` removed
