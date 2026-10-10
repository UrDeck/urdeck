## MODIFIED Requirements

### Requirement: Indicator Mode
The indicator MUST have a mode setting in the configuration with the values `always`, `fade`, `off` and `auto`, and
`auto` MUST be the default when the setting is absent. An unknown value is treated as `auto` with a logged warning.

- `always`: a band is reserved for the indicator (see grid-layout); the indicator is always visible in it. The band is
  at the bottom of the screen, or directly above the dock when there is one (see dock, "Dock Placement")
- `fade`: the indicator floats over the page, in a translucent pill behind it so it stays readable, at the bottom of
  the grid area: at the bottom of the screen, or just above the dock when there is one. It appears when the page
  changes or a swipe starts and fades out a couple of seconds after the page has settled. While it is not visible,
  taps pass through it to what is underneath
- `off`: the indicator is never shown and no band is reserved for it
- `auto`: with one page, as `off`. With more, as `always` when the grid keeps at least four whole rows after the
  indicator's band and the dock, if there is one, are reserved, and as `fade` otherwise
- The mode is decided from the number of grid rows, never from a pixel size
- The indicator never overlaps the dock, in any mode

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

#### Scenario: Always, with a dock
- **WHEN** the mode is `always`, the configuration has two pages and a dock
- **THEN** the indicator's band lies directly above the dock and the grid ends above the indicator's band

#### Scenario: Fade, with a dock
- **WHEN** the mode is `fade`, there is a dock, and the user changes page
- **THEN** the indicator appears over the bottom of the grid, just above the dock, and does not cover the dock

#### Scenario: Auto with a dock on the tall panel
- **WHEN** the mode is `auto`, the configuration has two pages and a dock, and the screen is 1100x3840
- **THEN** the behaviour is that of `always` and the grid keeps 13 whole rows
