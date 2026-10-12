# tray-icon Specification

## Purpose
Defines the notification-area icon: its tooltip, its native popup menu built from an ordered list of entries (Quit in version 1), and its behaviour across Explorer restarts.
## Requirements
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

### Requirement: Tray Menu
The tray icon MUST open a native popup menu on right-click:

- The menu is built from an ordered list of entries, each with a label and an action, so adding an entry means adding
  to the list and nothing in the tray code changes
- An entry may also say whether it is checked. That is asked every time the menu opens, and a checked entry is shown
  with the menu's standard check mark. An entry that does not say so has no check mark
- The menu has two entries, in this order: **Start with Windows** (checked as autostart, "Autostart Entry", says) and
  **Quit**
- Choosing an entry runs its action; dismissing the menu (a click elsewhere, Escape) runs nothing and leaves the menu
  closed
- Opening the menu does not take focus from the window that has it, other than the menu itself while it is open

#### Scenario: Quit closes the host
- **WHEN** the user right-clicks the icon and chooses Quit
- **THEN** the host window closes, its resources are released and the process ends
- **THEN** the icon is no longer in the notification area

#### Scenario: Menu dismissed
- **WHEN** the menu is open and the user clicks elsewhere or presses Escape
- **THEN** the menu closes and the host keeps running

#### Scenario: Entries come from the list
- **WHEN** an entry is added to the menu's list
- **THEN** it is shown in the menu at its position and runs its action when chosen, with no other code changed

#### Scenario: Checked entry
- **WHEN** the menu opens and an entry reports that it is checked
- **THEN** the entry is shown with a check mark, and the other entries without one

#### Scenario: Check state changes between openings
- **WHEN** an entry reported checked, its state changes, and the menu is opened again
- **THEN** the entry is shown without a check mark

