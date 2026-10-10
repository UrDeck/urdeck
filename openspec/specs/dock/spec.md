# dock Specification

## Purpose
Defines the dock: a row of widget slots at the bottom of the screen that is the same on every page, so that the
launchers a user reaches for most are always in the same place.
## Requirements
### Requirement: Dock Placement
When the configuration's `dock` has at least one entry, the host MUST reserve a band for the dock at the very bottom
of the screen, on every page.

- From the bottom of the screen upwards the order is: the dock, the page indicator, the widget grid (see grid-layout,
  "Reserved Bottom Band", and page-navigation, "Indicator Mode")
- The band's height is a theme value, a fraction of the grid's column width (see theme, "Theme Contents"), so it
  scales with the display and the user never sees a pixel size
- The dock is reserved whenever it has entries, even when that leaves few grid rows; a page widget that no longer
  fits is not placed and an error is logged (see grid-layout, "Widgets Must Fit The Grid Area")
- With an empty or absent `dock` no band is reserved and the screen is laid out exactly as without this capability

#### Scenario: Dock on the tall panel
- **WHEN** the screen is 1100x3840, the indicator is always shown and the dock has four entries
- **THEN** the dock occupies about the bottom 195 pixels, the indicator's band lies directly above it, and the grid
  keeps its 13 whole rows

#### Scenario: No dock entries
- **WHEN** `dock` is empty
- **THEN** no dock band is reserved and the indicator and the grid are where they were before

#### Scenario: Dock on a short screen
- **WHEN** the screen is 1000x1000 and the dock has entries
- **THEN** the dock is reserved, the grid has fewer whole rows, and a page widget in a row that is gone is not placed
  and an error is logged

### Requirement: Dock Slots
The dock MUST offer four slots of equal size, and MUST show the entries as one group centred horizontally in the band.

- A slot is a square whose side is the band's height, never more than a quarter of the screen width
- The group is as wide as the number of entries shown (one to four slots); slots are adjacent, the first entry at the
  left
- A slot is laid out as a grid cell of that size: its card is the slot inset by half the theme's gap on every side,
  so neighbouring cards are one gap apart and a card never touches the screen edge or the indicator's band

#### Scenario: Four entries on the tall panel
- **WHEN** the dock has four entries on the 1100x3840 panel
- **THEN** four square slots of about 195 pixels are shown side by side, the group is centred with the same space to
  its left and right, and the cards are one gap apart

#### Scenario: Two entries
- **WHEN** the dock has two entries
- **THEN** two slots are shown, centred as a group

#### Scenario: Equal size
- **WHEN** the dock has entries of different widget types
- **THEN** every card has the same size

### Requirement: Dock Content
A slot MUST accept any widget type that supports the size 1x1, and MUST show it as a miniature of that widget's 1x1
card.

- The widget is created, configured, attached, refreshed and painted exactly like a widget on a page, at the size
  1x1, whatever `width` and `height` its entry carries
- The theme is resolved for the slot's size, as it is for a grid cell: the card's corner radius, border, padding and
  every text size are the theme's fractions of the slot's side. The widget receives an ordinary render context and
  cannot tell that it is in the dock
- A widget type that does not support 1x1 is not shown: its slot stays empty and one warning naming the type is logged
- An entry whose `typeId` is not registered, or is missing, leaves its slot empty with a logged warning, like an
  unknown widget on a page
- An entry with `isVisible` set to `false` leaves its slot empty

#### Scenario: Shortcut in the dock
- **WHEN** a dock entry is a shortcut to an installed application
- **THEN** its slot shows a small card with the application's icon

#### Scenario: Another 1x1 widget
- **WHEN** a dock entry is a clock
- **THEN** its slot shows the clock's 1x1 composition, scaled to the slot, with padding and text in proportion

#### Scenario: Widget without a 1x1 size
- **WHEN** a dock entry names a widget type whose only size is 4x2
- **THEN** its slot is empty, a warning names the type, and the other slots are shown

#### Scenario: Unknown type
- **WHEN** a dock entry has a `typeId` that is not registered
- **THEN** its slot is empty and a warning is logged

### Requirement: Dock Configuration
The dock MUST be configured by the top-level `dock` list of the configuration file, whose entries are widget objects
of the same shape as the widgets of a page.

- An entry's position is its index in the list; its `col` and `row` are ignored
- At most four entries are shown; further entries are ignored with one logged warning, and stay in the file
- Widget-specific settings and properties the widget does not know are preserved when the configuration is saved
- A change to `dock` in the file takes effect on configuration reload, without a restart
- The dock is not part of any page: it is the same whatever page is shown

#### Scenario: Two shortcuts
- **WHEN** `dock` holds two objects with `typeId` `urdeck.widgets.shortcut` and a `target` each
- **THEN** the dock shows the first in the left slot and the second in the right slot of a centred pair

#### Scenario: Five entries
- **WHEN** `dock` holds five entries
- **THEN** the first four are shown, a warning is logged, and after a save the fifth is still in the file

#### Scenario: Dock edited while running
- **WHEN** the user adds an entry to `dock` and saves the file
- **THEN** the dock shows the new entry without a restart, and the page that was shown is still shown

#### Scenario: Unknown property survives
- **WHEN** a dock entry has a property its widget does not know and the configuration is saved
- **THEN** the property is still in the file

### Requirement: The Dock Stays While Pages Change
The dock MUST NOT move, be rebuilt or lose its state when the page changes.

- During a swipe and the slide that follows, the pages move and the dock stays where it is
- The dock's widgets stay alive across page changes: their readings stay subscribed, their icons stay loaded and they
  are not repainted for a page change
- The dock is rebuilt only when what it depends on changes: the configuration, the theme, the plugins, or the size or
  scaling of the display
- The dock is drawn over the window background, never over a page's widgets, and no page widget extends under it

#### Scenario: Swipe to another page
- **WHEN** the user swipes from the first page to the second
- **THEN** the dock does not move, shows the same widgets, and none of them is reloaded or repainted

#### Scenario: Configuration reload
- **WHEN** the configuration file is saved with a changed dock entry
- **THEN** the dock is rebuilt with the new entry

### Requirement: Dock Input
Press feedback and taps MUST reach the widgets of the dock as they reach the widgets of a page (see widget-input).

- A point inside a dock card belongs to that card's widget; the gaps between cards and the rest of the band belong to
  nobody
- A horizontal swipe that starts on the dock changes the page, as it does anywhere else; the widget it started on is
  released from its press and receives no tap
- A widget in the dock that does not accept taps shows no press feedback and receives nothing

#### Scenario: Tap on a docked shortcut
- **WHEN** the user taps a shortcut in the dock
- **THEN** its card shows the press while the finger is down, and its target is launched once on release

#### Scenario: Tap beside the slots
- **WHEN** the user taps the dock's band to the left of the first card
- **THEN** nothing happens

#### Scenario: Swipe that starts on the dock
- **WHEN** the user puts a finger down on a docked shortcut and swipes to the left
- **THEN** the page changes, the dock stays, and nothing is launched

### Requirement: Dock In A Snapshot
The snapshot command MUST draw the dock with the same layout, theme and widget code as the live window, and MUST wait
for the readings and icons its widgets asked for as it does for a page's.

#### Scenario: Snapshot with a dock
- **WHEN** a snapshot is taken of a configuration with two pages and two docked shortcuts
- **THEN** the image shows the page, the indicator above the dock, and the two shortcuts with their icons in the dock

### Requirement: The Dock Costs Only Its Widgets
The dock MUST NOT add recurring work of its own. With no dock entries nothing is created for it, and with entries the
cost at rest is that of its widgets: a dock of shortcuts produces no frames and uses no CPU or GPU while nobody
touches it.

#### Scenario: Idle dock of shortcuts
- **WHEN** a dock of four shortcuts is shown and nobody touches the deck
- **THEN** CPU and GPU use are not above those of the same page without a dock

#### Scenario: No dock
- **WHEN** `dock` is empty
- **THEN** no surface, theme or view is created for it
