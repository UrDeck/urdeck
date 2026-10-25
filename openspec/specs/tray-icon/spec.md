# tray-icon Specification

## Purpose
Defines the notification-area icon: its tooltip, its native popup menu built from an ordered list of entries (Quit in version 1), and its behaviour across Explorer restarts.
## Requirements
### Requirement: Tray Menu
The tray icon MUST open a native popup menu on right-click:

- The menu is built from an ordered list of entries, each with a label and an action, so adding an entry means adding
  to the list and nothing in the tray code changes
- An entry may also say whether it is checked. That is asked every time the menu opens, and a checked entry is shown
  with the menu's standard check mark. An entry that does not say so has no check mark
- An entry may also say whether it is shown, and may give its label when the menu opens. Both are asked every time the
  menu opens
- The menu has these entries, in this order: **Restart to update to <version>** (shown only while an update is ready;
  see app-updates, "Update Check"), **Settings** (the same action as a left click), **Open data folder** (opens the
  data folder in File Explorer; see data-folder, "Opening The Data Folder"), **Start with Windows** (checked as
  autostart, "Autostart Entry", says) and **Quit**
- Choosing an entry runs its action; dismissing the menu (a click elsewhere, Escape) runs nothing and leaves the menu
  closed
- Opening the menu does not take focus from the window that has it, other than the menu itself while it is open

#### Scenario: Settings entry opens the window
- **WHEN** the user right-clicks the icon and chooses Settings
- **THEN** the settings window is shown and has focus

#### Scenario: Open data folder entry
- **WHEN** the user right-clicks the icon and chooses Open data folder
- **THEN** File Explorer opens the data folder and the host keeps running

#### Scenario: Update entry
- **WHEN** an update to `0.2.0` is ready and the user opens the menu
- **THEN** the first entry is "Restart to update to 0.2.0"; with no update ready the menu has no such entry

#### Scenario: Quit closes the host
- **WHEN** the user right-clicks the icon and chooses Quit
- **THEN** the host window closes, its resources are released and the process ends
- **THEN** the icon is no longer in the notification area

#### Scenario: Quit with Settings open
- **WHEN** the settings window is open and the user chooses Quit
- **THEN** both windows close and the process ends

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

### Requirement: Notification Area Icon
The host MUST show a notification-area icon for as long as it runs:

- The icon is the application icon at the small-icon size of the current DPI
- Its tooltip is "UrDeck" while the deck is shown. While the deck is hidden because its display is absent (see
  display-targeting, "Absent Display"), the tooltip is "UrDeck: waiting for" followed by the display's name
- It is added when the host window is created, whether or not that window is shown, and removed when the host closes,
  so no stale icon is left behind
- When Explorer restarts (the `TaskbarCreated` message), the icon is added again, with the current tooltip
- Clicking it once with the left button opens the settings window, or brings it forward when it is open (see
  settings-window, "Opening The Settings Window"). A double-click does the same and does not open a second window
- It does not change whether the deck window takes focus, and uses no package other than the Windows API

#### Scenario: Icon shown while running
- **WHEN** the host starts
- **THEN** a UrDeck icon with the tooltip "UrDeck" is in the notification area

#### Scenario: Icon removed on exit
- **WHEN** the host closes by any route
- **THEN** the icon is removed from the notification area

#### Scenario: Explorer restarts
- **WHEN** Explorer is restarted while the host runs
- **THEN** the icon appears again without restarting the host

#### Scenario: Left click opens Settings
- **WHEN** the user clicks the icon with the left button
- **THEN** the settings window is shown and has focus
- **THEN** the deck window's focus behaviour is unchanged: it still does not take focus

#### Scenario: Double click opens one window
- **WHEN** the user double-clicks the icon with the left button
- **THEN** exactly one settings window is open

#### Scenario: Icon stays while Settings is open or closed
- **WHEN** the settings window is opened and then closed
- **THEN** the notification-area icon is present throughout

#### Scenario: Waiting for the display
- **WHEN** the remembered display "HYTE Y70Touch" is disconnected
- **THEN** the tooltip reads "UrDeck: waiting for HYTE Y70Touch"
- **THEN** it reads "UrDeck" again when the deck is back on the display

