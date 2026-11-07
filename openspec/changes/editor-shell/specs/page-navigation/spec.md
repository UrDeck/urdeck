## MODIFIED Requirements

### Requirement: Current Page Is Runtime State
The host MUST keep the page being shown as runtime state, separate from the configuration. Pages are identified by
their `id`, not by their position or name:

- On startup the page whose `id` equals `startPage` is shown. When `startPage` is absent or names no page, the first
  page is shown and the log says so once
- A page without an `id`, or with an `id` another page already has, is given a new unique one, which the host writes
  to the file once; this is not an undo step
- Changing page never writes the configuration file
- When the configuration is reloaded the host MUST keep showing the page the user is on: the page with the same `id`
  as before; if no page has that id, the same index clamped to the page list
- While the editor is open, the deck shows the page selected in the editor (see editor), and the editor shows the page
  the deck moves to when the user swipes on the deck. Closing the editor leaves the deck on the page it shows

#### Scenario: Start page
- **WHEN** the configuration has three pages and `startPage` is the id of the second
- **THEN** the second page is shown on startup

#### Scenario: Start page missing
- **WHEN** `startPage` names an id no page has
- **THEN** the first page is shown on startup and the log says the start page was not found

#### Scenario: Reordering keeps the start page
- **WHEN** the start page is moved from first to third place
- **THEN** it is still the page shown on the next startup

#### Scenario: Swiping leaves the file alone
- **WHEN** the user swipes from the first page to the second
- **THEN** `urdeck-config.json` is not modified

#### Scenario: Reload keeps the page
- **WHEN** the user is on the page with id `p-games` and the configuration changes
- **THEN** the page with id `p-games` is still shown after the reload, also when it was renamed

#### Scenario: Current page was removed
- **WHEN** the user is on the third page and a reload leaves only two pages, none with the old page's id
- **THEN** the second page is shown

#### Scenario: Deck follows the editor
- **WHEN** the editor is open and the user selects the "Home" tab
- **THEN** the deck shows the Home page, and after the editor closes the deck still shows it
