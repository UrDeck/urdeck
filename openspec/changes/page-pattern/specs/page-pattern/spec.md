## ADDED Requirements

### Requirement: Page Pattern Layer
When the theme's `pattern.kind` is `dots`, `grid`, `hatch` or `scanlines`, the deck MUST draw that pattern as one layer over
the whole window: above the background, its scrim and the theme's background colour, and under every card, the dock and
the page indicator.

- It does not move, change or restart when the page changes; pages slide over it
- It covers the dock's and the indicator's bands as well as the grid
- Cards are drawn over it with their own transparency, so a translucent card shows it through its fill
- It never receives pointer input: taps and swipes behave as they do without it
- It is drawn whatever the configuration's `background` is, `"type": "none"` included: it belongs to the theme
- With `pattern` absent or its `kind` `none`, nothing is drawn and nothing is created for it

#### Scenario: Carbon's dots
- **WHEN** Carbon is the theme
- **THEN** a grid of faint dots covers the window, visible in the gaps between cards and around the dock, and no dot is drawn
  over an opaque card

#### Scenario: Page swipe
- **WHEN** the user swipes from one page to the next with a pattern shown
- **THEN** the pages slide and the pattern stays in place

#### Scenario: No background
- **WHEN** the configuration has `"background": {"type": "none"}` and the theme has a pattern
- **THEN** the pattern is drawn over the theme's background colour

#### Scenario: No pattern
- **WHEN** the theme has no `pattern`, or its `kind` is `none`
- **THEN** the deck is drawn as before and no pattern layer exists

#### Scenario: Pattern changes with the theme
- **WHEN** the theme changes from Carbon to one without a pattern while the deck runs
- **THEN** the dots disappear without a restart

### Requirement: Pattern Kinds
The pattern MUST be drawn as its `kind` says, with `spacing` and `thickness` as fractions of the width of one grid cell
(a quarter of the window's width) and the first mark half a repeat from the window's top and left edges:

- `dots`: round dots of radius `thickness`, `spacing` apart in both directions
- `grid`: horizontal and vertical lines of width `thickness`, `spacing` apart; with `majorEvery`, every `majorEvery`-th line
  in each direction, starting with the first, is drawn in `major.color` and `major.thickness` (each falling back to the
  pattern's own when absent)
- `hatch`: parallel lines of width `thickness`, `spacing` apart measured across the lines, in the direction `angle`:
  degrees from horizontal, positive rising to the right (45, the default, runs from bottom left to top right, as on the design
  board); 0 or 180 is horizontal, 90 or -90 vertical
- `scanlines`: horizontal lines of width `thickness`, `spacing` apart
- `angle` is used by `hatch` only, and `majorEvery` and `major` by `grid` only; elsewhere they are ignored

#### Scenario: Blueprint's grid
- **WHEN** a theme sets a `grid` with `spacing` 0.182, `majorEvery` 5 and a brighter `major.color` on a 1100 pixel wide window
- **THEN** grid lines are drawn 50 pixels apart and every fifth line, starting with the first, is brighter

#### Scenario: Ember's hatch
- **WHEN** a theme sets a `hatch` with `angle` 45
- **THEN** the lines run from bottom left to top right

#### Scenario: Scanlines
- **WHEN** a theme sets `scanlines` with `spacing` 0.036 on a 1100 pixel wide window
- **THEN** horizontal lines are drawn 10 pixels apart over the whole window

### Requirement: Pattern Repeats In Whole Pixels
The pattern MUST repeat in whole physical pixels so that it is crisp and has no seam anywhere on the window, at any display
scaling:

- The distance between marks is the spacing in pixels rounded to a whole pixel, at least two pixels
- A line is at least one pixel wide and its width is a whole number of pixels
- A hatch's angle is adjusted by at most one degree where needed so that its lines continue unbroken across every repeat
- Where two tiles of the pattern meet there is no visible line, gap or change in the marks

#### Scenario: Carbon's spacing on the panel
- **WHEN** Carbon (`spacing` 0.127) is shown on a window 1100 pixels wide
- **THEN** the dots are exactly 35 pixels apart everywhere on the window

#### Scenario: Display scaling
- **WHEN** the deck is shown at 150 % display scaling with a pattern
- **THEN** the marks are evenly spaced and no seam is visible between the tiles

### Requirement: Pattern Colour
A pattern MUST be drawn in `pattern.color`, including its alpha. A pattern without a `color` MUST be drawn in the theme's
muted text colour at one sixteenth of its opacity.

#### Scenario: Translucent colour
- **WHEN** a theme sets `pattern.color` to `#10ffffff`
- **THEN** the marks are white at an alpha of 16 over the background

#### Scenario: No colour
- **WHEN** a theme sets `pattern.kind` to `dots` and no `color`
- **THEN** the dots are drawn in its muted text colour at one sixteenth of that colour's opacity

### Requirement: Pattern Costs Nothing At Rest
The pattern MUST be made when the theme, the window's size or the display scaling changes, and MUST NOT be made or
repainted when a page is built or slides, a widget repaints, the readings change or the background moves. Making it MUST
take at most 20 ms once the process is warm, and for the patterns of the built-in and drafted themes (Carbon, Blueprint,
Ember, Rigline) it MUST take at most 10 MB of memory on the 1100x3840 panel. A pattern whose repeat is wider or taller than
1024 pixels is still drawn correctly and may cost more; the log says how large it was made. It does not depend on the display being on or the deck being shown, but it is released with the deck's page and made
again when the deck is shown again.

#### Scenario: Clocks ticking over a moving background
- **WHEN** Carbon is shown with its moving background and three clocks repaint every second for a minute
- **THEN** the pattern is made once and not again

#### Scenario: Memory on the panel
- **WHEN** Carbon is shown on the 1100x3840 panel
- **THEN** the process holds at most 10 MB more than with the same theme without its pattern

### Requirement: Pattern In Snapshots
A snapshot MUST draw the pattern exactly as the deck does: the same marks at the same pixels, over the background and its
scrim and under the cards, the dock and the page indicator.

#### Scenario: Snapshot of Carbon
- **WHEN** a snapshot of a Carbon page is taken
- **THEN** the dots appear in the gaps between the cards at the positions they have on the panel
