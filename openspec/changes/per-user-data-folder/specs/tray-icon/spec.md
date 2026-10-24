## MODIFIED Requirements

### Requirement: Tray Menu
The tray icon MUST open a native popup menu on right-click:

- The menu is built from an ordered list of entries, each with a label and an action, so adding an entry means adding
  to the list and nothing in the tray code changes
- An entry may also say whether it is checked. That is asked every time the menu opens, and a checked entry is shown
  with the menu's standard check mark. An entry that does not say so has no check mark
- The menu has four entries, in this order: **Settings** (the same action as a left click), **Open data folder**
  (opens the data folder in File Explorer; see data-folder, "Opening The Data Folder"), **Start with Windows**
  (checked as autostart, "Autostart Entry", says) and **Quit**
- Choosing an entry runs its action; dismissing the menu (a click elsewhere, Escape) runs nothing and leaves the menu
  closed
- Opening the menu does not take focus from the window that has it, other than the menu itself while it is open

#### Scenario: Settings entry opens the window
- **WHEN** the user right-clicks the icon and chooses Settings
- **THEN** the settings window is shown and has focus

#### Scenario: Open data folder entry
- **WHEN** the user right-clicks the icon and chooses Open data folder
- **THEN** File Explorer opens the data folder and the host keeps running

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
