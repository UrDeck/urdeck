## ADDED Requirements

### Requirement: Explicit Display Choice
The user MUST be able to choose the deck's display from the settings window, and to hand the choice back to the
selectors, without editing the configuration file (see settings-window, "Monitor Picker").

- Choosing a connected display remembers it as `display` with its name, model and device path and **no** `selector`,
  so it is kept whatever `monitorName` and `monitor` say (see "Remembered Display"), and writes the configuration
- The deck moves to the chosen display by the rules of "Verified Placement After Display Changes", without a restart
  and without waiting for a display change. Choosing the display the deck is already on does not move it or rebuild
  its page; it writes the configuration only when that turns an automatic choice into an explicit one, and writes
  nothing when the display was already chosen explicitly
- Choosing automatically removes `display` and writes the configuration. The host then chooses again with the
  selectors, as when the entry is deleted from the file, and remembers the result with its selector. The deck moves
  to that display at once
- Properties of `display` that the host does not know are kept when a display is chosen
- Both are logged with the display's name and whether it was chosen or left to the selectors
- A choice never makes the deck appear on a display other than the one chosen: if the chosen display disappears the
  deck is hidden as in "Absent Display"

#### Scenario: Choosing a display
- **WHEN** the deck is on the primary display, `monitorName` is `"primary"`, and the user chooses the display "HYTE Y70Touch"
- **THEN** the deck covers that display, and the configuration's `display` names it and has no `selector`

#### Scenario: A chosen display survives the selectors
- **WHEN** a display was chosen in the settings window and the user later edits `monitorName` and saves
- **THEN** the deck stays on the chosen display

#### Scenario: Choosing the current display again
- **WHEN** the display was chosen explicitly and the user chooses the display the deck is on
- **THEN** the deck does not move or rebuild its page and the configuration file is not written

#### Scenario: Pinning the display the deck is on
- **WHEN** the deck is on a display the selectors picked and the user chooses that display
- **THEN** the deck does not move or rebuild its page, and `display` is written without a `selector`

#### Scenario: Choosing automatically
- **WHEN** a display was chosen in the settings window, `monitorName` is `"tallest"`, and the user chooses automatically
- **THEN** the deck moves to the tallest portrait display and `display` is remembered again with the selector `tallest`

#### Scenario: Chosen display is unplugged
- **WHEN** a display was chosen in the settings window and is disconnected
- **THEN** the deck is hidden and is not shown on another display, and the settings window shows the display as waiting
