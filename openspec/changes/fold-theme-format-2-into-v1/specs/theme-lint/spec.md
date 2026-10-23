## MODIFIED Requirements

### Requirement: Findings About The File
A theme whose file cannot be used MUST be reported, and a theme whose values are wrong MUST be reported value by value:

| Code | Severity | Meaning |
|---|---|---|
| `THEME001` | error | `theme.json` is missing, is not valid JSON or holds no object. The theme cannot load |
| `THEME002` | warning | a key the deck does not read. The message names the closest key that is read when one is within two edits |
| `THEME003` | warning | a value of the wrong JSON type; the deck skips that value and takes the default |
| `THEME004` | warning | a number outside its range; the message carries the range |
| `THEME005` | warning | a value that is not one of an enumeration's names; the message lists the names |
| `THEME006` | warning | a colour that is not `#RRGGBB` or `#AARRGGBB` |
| `THEME007` | warning | an array outside its length limits or with an invalid element |
| `THEME008` | warning | a group that cannot work without a member it does not have (a gradient without `colors`); the group is ignored |

- Every message says what is wrong and how to fix it, in one line, without a stack trace
- Each finding names the key by its path (`card.shadow.blur`), and the file it is in
- Comments and trailing commas in `theme.json` are accepted, as the deck accepts them
- A key the deck reads is not unknown; a key in the deferred-keys document is, because the deck does not
  read it
- A theme with an error from `THEME001` is not checked further

#### Scenario: Missing file
- **WHEN** a folder with no `theme.json` is checked
- **THEN** one `THEME001` error says the file is missing, and nothing else is reported

#### Scenario: Not JSON
- **WHEN** `theme.json` holds `{ "card": `
- **THEN** one `THEME001` error says the file is not valid JSON and where the reader stopped

#### Scenario: Misspelt key
- **WHEN** a theme has `card.borderWith`
- **THEN** a `THEME002` warning says the key is not read and suggests `card.borderWidth`

#### Scenario: Wrong type
- **WHEN** a theme sets `card.gap` to `"wide"`
- **THEN** a `THEME003` warning names `card.gap` and says a number is expected; the deck skips that value only

#### Scenario: Enumeration in another case
- **WHEN** a theme sets `card.borderStyle` to `Dashed`
- **THEN** there is no finding

#### Scenario: A leftover format key
- **WHEN** a theme declares `"format": 2`
- **THEN** a `THEME002` warning says the key is not read
