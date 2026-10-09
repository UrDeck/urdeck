## Purpose

Defines how a user moves between the pages of a configuration: which page is shown, how a swipe or a tap changes it,
what stays alive while it changes, and the page indicator that shows where the user is.

## ADDED Requirements

### Requirement: Current Page Is Runtime State
The host MUST keep the page being shown as runtime state, separate from the configuration:

- On startup the page at index `activePage` is shown; an index outside the page list is clamped to the nearest page
- Changing page never writes the configuration file
- When the configuration is reloaded the host MUST keep showing the page the user is on: the page with the same name
  as before; if no page has that name, the same index clamped to the page list
- When the page list has several pages with the same name the first one with that name is used

#### Scenario: Start page
- **WHEN** the configuration has three pages and `activePage` is `1`
- **THEN** the second page is shown on startup

#### Scenario: Swiping leaves the file alone
- **WHEN** the user swipes from the first page to the second
- **THEN** `urdeck-config.json` is not modified and `activePage` is unchanged

#### Scenario: Reload keeps the page
- **WHEN** the user is on the page named "Games" and the configuration is saved after an edit to a widget
- **THEN** the page named "Games" is still shown after the reload, not the page at `activePage`

#### Scenario: Current page was removed
- **WHEN** the user is on the third page and a reload leaves only two pages, none with the old page's name
- **THEN** the second page is shown

### Requirement: Swipe Changes Page
A horizontal swipe on the page MUST change the page, using one gesture path for mouse, touch and pen:

- The direction is decided once the pointer has moved a small distance from where it went down; a gesture that is
  mostly horizontal is a swipe, one that is mostly vertical is not and does not change the page
- The page follows the pointer while it moves, and the neighbouring page slides in beside it
- On release the page changes to the neighbour if the swipe went past half the width or was fast enough in that
  direction, and otherwise returns to where it was; the slide ends at the page edge in either case
- Only the first pointer that goes down takes part; further contacts while it is down are ignored
- A press and release that moves less than the small distance and does not become a swipe is a tap

#### Scenario: Swipe to the next page
- **WHEN** the user drags a finger more than half the width to the left and lets go
- **THEN** the next page slides fully into view

#### Scenario: Short swipe returns
- **WHEN** the user drags a finger a short distance slowly and lets go
- **THEN** the page slides back to where it was and the page does not change

#### Scenario: Fast flick
- **WHEN** the user flicks quickly to the left over a short distance
- **THEN** the next page slides into view

#### Scenario: Vertical movement
- **WHEN** the user drags mostly vertically
- **THEN** the page does not move

#### Scenario: Second finger
- **WHEN** a second finger touches while a swipe is in progress
- **THEN** the second finger has no effect

### Requirement: Swiping Past The Ends Bounces
The first page MUST NOT have a page before it and the last page MUST NOT have a page after it, and the pages MUST NOT
wrap around. A swipe towards a missing page moves the current page with growing resistance and, on release, returns
it to its place. A configuration with one page has nothing to swipe to and behaves the same way.

#### Scenario: Swipe right on the first page
- **WHEN** the user swipes to the right on the first page
- **THEN** the page moves a little with resistance, no other page appears, and on release the page returns

#### Scenario: No wrap
- **WHEN** the user swipes to the left on the last page
- **THEN** the first page is not shown

### Requirement: Only The Pages In Use Are Alive
At rest the host MUST hold only the current page: its widgets exist, subscribe to their readings and may animate.
While a swipe is in progress the neighbour page in the direction of the swipe is also alive. Once the slide has
settled, the page that is no longer shown MUST be released: its widgets are disposed, their readings unsubscribed and
their surfaces freed. No widget on a page that is not shown may be repainted or animate.

- The neighbour page is built, and its readings subscribed, as soon as the swipe direction is decided, not earlier: a
  tap or a vertical drag builds nothing and starts no provider
- A widget showing a reading that has a previous value draws that value, dimmed, until a new value arrives; it draws
  the unavailable mark only when the reading has no value (see data-providers)
- A widget that does not use readings (the clock) draws the current state when its page is built

#### Scenario: One page alive at rest
- **WHEN** the host has three pages and has settled on the second
- **THEN** only the second page's widgets exist and the readings only the other pages use are not sampled

#### Scenario: Neighbour built for a swipe
- **WHEN** the user starts dragging to the left on the first page
- **THEN** the second page is built and slides in with the drag

#### Scenario: Left page released
- **WHEN** the slide to the second page has settled
- **THEN** the first page's widgets are disposed and their frame-clock membership and timers are gone

#### Scenario: Returning to a page after a while
- **WHEN** the user returns to a page of readings after two hours
- **THEN** each reading shows its last value, dimmed, until the first new value arrives, not a dash

#### Scenario: Clock after a long absence
- **WHEN** the user returns to a page holding a clock after two hours
- **THEN** the clock shows the current time on its first paint

#### Scenario: Idle cost
- **WHEN** the pages are not being swiped and nothing animates
- **THEN** no frames are produced and CPU and GPU use are not above those of a single page

### Requirement: Page Indicator
The host MUST draw a page indicator showing the number of pages and the current one:

- One dot per page, with the current page drawn as a pill, so that three pages on the middle one read as dot, pill, dot
- During a swipe the pill moves between the dots with the page, so that its position reflects how far the swipe has
  gone, and it settles on the page that is shown
- The indicator is drawn by the engine from the theme, in the snapshot as well as in the window
- The indicator is not shown for a configuration with one page when its mode is `auto`

#### Scenario: Three pages on the middle one
- **WHEN** a configuration has three pages and the second is shown
- **THEN** the indicator shows three marks with the middle one drawn as a pill

#### Scenario: Pill follows the swipe
- **WHEN** the user has dragged a third of the way from the first page to the second
- **THEN** the pill has moved a third of the way from the first dot to the second

#### Scenario: One page
- **WHEN** the configuration has one page and the indicator mode is `auto`
- **THEN** no indicator is shown and no band is reserved

### Requirement: Tapping A Dot Changes Page
A tap on the indicator MUST switch to the page of the dot that was tapped, with the slide. The touch target of a dot is
its whole slot in the indicator, not only the visible dot, and is at least a third of a grid cell wide. A tap on the
current page's slot does nothing.

#### Scenario: Tap on the third dot
- **WHEN** the first page is shown and the user taps the third dot
- **THEN** the third page is shown

#### Scenario: Tap beside a dot
- **WHEN** the user taps inside a dot's slot but beside the visible dot
- **THEN** that dot's page is shown

### Requirement: Indicator Mode
The indicator MUST have a mode setting in the configuration with the values `always`, `fade`, `off` and `auto`, and
`auto` MUST be the default when the setting is absent. An unknown value is treated as `auto` with a logged warning.

- `always`: a band at the bottom of the screen is reserved for the indicator (see grid-layout); the indicator is
  always visible in it
- `fade`: the indicator floats over the page, in a translucent pill behind it so it stays readable. It appears when
  the page changes or a swipe starts and fades out a couple of seconds after the page has settled. While it is not
  visible, taps pass through it to what is underneath
- `off`: the indicator is never shown and no band is reserved
- `auto`: with one page, as `off`. With more, as `always` when the grid keeps at least four whole rows after the band is
  reserved, and as `fade` otherwise
- The mode is decided from the number of grid rows, never from a pixel size

#### Scenario: Always
- **WHEN** the mode is `always` and the configuration has two pages
- **THEN** the indicator is visible at the bottom at all times and widgets never extend under it

#### Scenario: Fade
- **WHEN** the mode is `fade` and the user changes page
- **THEN** the indicator appears and then fades out when the page has been still for a couple of seconds

#### Scenario: Fade does not block taps
- **WHEN** the mode is `fade`, the indicator has faded out, and the user taps where it was
- **THEN** the tap is not taken by the indicator

#### Scenario: Auto on the tall panel
- **WHEN** the mode is `auto`, the configuration has two pages and the screen is 1100x3840
- **THEN** the behaviour is that of `always`

#### Scenario: Auto on a very short display
- **WHEN** the mode is `auto`, the configuration has two pages and reserving the band would leave fewer than four
  whole rows
- **THEN** the behaviour is that of `fade` and no band is reserved

#### Scenario: Unknown mode
- **WHEN** the mode is set to `sometimes`
- **THEN** the mode is `auto` and a warning is logged
