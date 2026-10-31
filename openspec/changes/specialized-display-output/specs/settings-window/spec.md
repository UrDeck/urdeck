## ADDED Requirements

### Requirement: Specialized Displays In The Picker
The monitor picker MUST list connected specialized displays and let the user choose one as the deck's display, as it
does for desktop monitors (see "Monitor Picker").

- A specialized display is a row with its friendly name, its resolution and orientation as the deck draws it (after
  rotation), a **Specialized** badge, and a number continuing after the desktop monitors' numbers. It has no position
  ("Left of Display 1"), since it is not on the desktop. Specialized rows come after the desktop rows, in device path
  order
- Choosing it moves the deck there (see display-targeting, "Explicit Display Choice"; direct-output)
- The deck's specialized display has a **Rotate** action that turns the deck 90 degrees clockwise (see direct-output,
  "Orientation On A Specialized Display"); other rows have none
- A specialized display held by another app is shown and can be chosen; its row says it is in use, and the deck waits
  for it
- The list follows displays being removed from or put back on the desktop while the window is open, without polling

#### Scenario: Picker with a specialized panel
- **WHEN** the primary "DELL U2723QE" is on the desktop and the panel "HYTE Y70Touch" is specialized and holds the deck
- **THEN** the list has "DELL U2723QE" (Primary, number 1) and "HYTE Y70Touch", 1100×3840, portrait, Specialized,
  number 2, marked as the deck's display, with a Rotate action

#### Scenario: Panel removed from the desktop while the window is open
- **WHEN** the settings window is open and the user removes the panel from the desktop
- **THEN** the panel's row gains the Specialized badge and loses its position, without reopening the window

### Requirement: Identify On A Specialized Display
Identify MUST show the deck's specialized display's number on that display too.

- The number is drawn by the deck in the same corner and style as the markers on desktop monitors, for the same two
  seconds, and does not stop touch or motion on the rest of the deck
- A specialized display that does not hold the deck gets no marker (nothing may draw on it); its row says so while the
  markers show

#### Scenario: Identify with the deck on a specialized panel
- **WHEN** the deck is on the specialized panel and the user presses Identify
- **THEN** "1" appears on the primary monitor and "2" on the panel, and both are gone after about two seconds
