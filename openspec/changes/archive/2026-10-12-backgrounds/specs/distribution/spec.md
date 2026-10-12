## MODIFIED Requirements

### Requirement: Local Install
The repository MUST provide one command that installs the current sources for the current user
(`scripts/Install-Local.ps1`):

- It publishes the host and copies the result to `%LOCALAPPDATA%\Programs\UrDeck`, or to the folder given as
  `-Destination`. No elevation is needed
- If a copy from the destination is running, it is asked to close (see host-shell, "Quit Command") before files are
  replaced, and is started again afterwards. A copy that was not running is started only when `-Start` is given
- The application's files are replaced, and files of earlier versions that are no longer published are removed
- Files the user owns are never replaced or removed: `urdeck-config.json` once it exists, `urdeck.log` and
  `urdeck.log.1`, `cache/`, `themes/`, `backgrounds/`, and files in `plugins/` that the publish did not produce
- A destination without `urdeck-config.json` receives the sample configuration
- It does not register the app to start with Windows; that stays the user's choice in the tray menu
- A failure (the publish fails, the running copy does not close, a file cannot be replaced) stops the command with a
  message and a non-zero exit code, and leaves the user's files as they were

#### Scenario: First install
- **WHEN** the command is run and the destination does not exist
- **THEN** the destination holds a runnable copy with the sample configuration

#### Scenario: Update keeps the user's files
- **WHEN** the command is run over an installed copy whose configuration was edited and whose `themes/`,
  `backgrounds/` and `plugins/` hold files the user added
- **THEN** the application's files are the new ones, and the configuration, the log, the themes, the backgrounds and
  the added plugins are unchanged

#### Scenario: Update while running
- **WHEN** the command is run while the installed copy is running
- **THEN** the running copy closes, the files are replaced and the new copy is running when the command ends

#### Scenario: Running copy does not close
- **WHEN** the installed copy is running and does not end within the time `--quit` waits
- **THEN** the command stops with a message, replaces nothing and the running copy is left as it is
