## MODIFIED Requirements

### Requirement: User Themes
The host MUST discover user themes in a `themes` folder in the data folder (see data-folder).

- A user theme MAY omit values; omitted values are taken from the default theme
- A user theme with the same name as a built-in theme takes precedence over it
- A user theme whose settings file cannot be read is skipped with a logged warning naming the theme, and does not
  affect other themes
- An individual value that is invalid (for example an unparseable colour) is replaced by the default theme's value
  with a logged warning

#### Scenario: Partial user theme
- **WHEN** a user theme defines only the accent colour and the corner radius
- **THEN** it can be selected, and every other value matches the default theme

#### Scenario: Broken theme file
- **WHEN** a user theme's settings file contains invalid content
- **THEN** that theme is not available, a warning is logged, and other themes remain selectable

#### Scenario: Invalid single value
- **WHEN** a user theme sets the text colour to a string that is not a colour
- **THEN** the theme loads, the default theme's text colour is used, and a warning is logged

#### Scenario: Theme in the per-user data folder
- **WHEN** a per-user copy runs and `%APPDATA%\UrDeck\themes\` holds a valid theme folder `mine`
- **THEN** the configuration can select `mine`, and a `themes` folder in the application folder is not read
