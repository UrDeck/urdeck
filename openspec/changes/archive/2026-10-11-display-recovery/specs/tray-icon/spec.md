## MODIFIED Requirements

### Requirement: Tray Icon
The host MUST show a notification-area icon for as long as it runs:

- The icon is the application icon at the small-icon size of the current DPI
- Its tooltip is "UrDeck" while the deck is shown. While the deck is hidden because its display is absent (see
  display-targeting, "Absent Display"), the tooltip is "UrDeck: waiting for" followed by the display's name
- It is added when the host window is created, whether or not that window is shown, and removed when the host closes,
  so no stale icon is left behind
- When Explorer restarts (the `TaskbarCreated` message), the icon is added again, with the current tooltip
- Clicking or double-clicking it with the left button does nothing in this version
- It does not change whether the host window takes focus, and uses no package other than the Windows API

#### Scenario: Icon shown while running
- **WHEN** the host starts
- **THEN** a UrDeck icon with the tooltip "UrDeck" is in the notification area

#### Scenario: Icon removed on exit
- **WHEN** the host closes by any route
- **THEN** the icon is removed from the notification area

#### Scenario: Explorer restarts
- **WHEN** Explorer is restarted while the host runs
- **THEN** the icon appears again without restarting the host

#### Scenario: Left click is inert
- **WHEN** the user clicks or double-clicks the icon with the left button
- **THEN** nothing opens and the host window's focus state does not change

#### Scenario: Waiting for the display
- **WHEN** the remembered display "HYTE Y70Touch" is disconnected
- **THEN** the tooltip reads "UrDeck: waiting for HYTE Y70Touch"
- **THEN** it reads "UrDeck" again when the deck is back on the display
