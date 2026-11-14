## ADDED Requirements

### Requirement: Lists In The Picker
The picker MUST mark a list reading as a list and show its number of rows as its live value. When the setting accepts
lists only, only list readings are listed and the picker says so; when it does not accept lists, none are listed.
Choosing a list shows its fields under the choice, so the user sees what the rows will hold.

#### Scenario: Choosing the friends list
- **WHEN** the user opens the picker from the List widget's reading and types "steam"
- **THEN** only Steam's list readings are listed, each marked as a list with its row count, and choosing "Friends
  playing" shows its fields name, game and avatar

#### Scenario: A list for a stats slot
- **WHEN** the user opens the picker from a stats slot
- **THEN** list readings are listed among the others, marked as lists, their value being the count
