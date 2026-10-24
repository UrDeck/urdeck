## MODIFIED Requirements

### Requirement: Published Application Folder
Publishing the host (`dotnet publish` of `UrDeck.Host` in the Release configuration) MUST produce a folder that runs
on a PC with neither the .NET SDK nor a .NET or Windows App SDK runtime installed. It contains:

- `UrDeck.Host.exe` with its runtime and dependencies
- `plugins/` with every first-party widget and provider assembly that the build places in the build output's
  `plugins/`
- `urdeck.ico` and the sample page as `urdeck-config.default.json`
- No `urdeck-config.json` and no `urdeck.portable`: a published copy runs in per-user mode (see data-folder)

The folder can be copied anywhere and started from there; it needs write access only to the data folder.

The build output (`dotnet build`) holds the same files plus `urdeck.portable`, so a development build keeps its
configuration, log and user files in its own folder. The build never writes `urdeck-config.json`.

#### Scenario: Published copy runs
- **WHEN** the published folder is copied to another location and `UrDeck.Host.exe --snapshot out.png --size 1100x3840`
  is run there
- **THEN** a PNG with the sample page's widgets is written and the log reports no missing plugin or widget type

#### Scenario: Same plugins as the build
- **WHEN** the host is built and published from the same sources
- **THEN** the published `plugins/` holds the same assemblies as the build output's `plugins/`

#### Scenario: Build keeps the working configuration
- **WHEN** the configuration in a build output folder was edited and the tracked sample page is then changed and the
  host rebuilt
- **THEN** the build output's `urdeck-config.json` still holds the edited configuration, and
  `urdeck-config.default.json` holds the new sample

### Requirement: Local Install
The repository MUST provide one command that installs the current sources for the current user
(`scripts/Install-Local.ps1`):

- It publishes the host and copies the result to `%LOCALAPPDATA%\Programs\UrDeck`, or to the folder given as
  `-Destination`. No elevation is needed
- If a copy from the destination is running, it is asked to close (see host-shell, "Quit Command") before files are
  replaced, and is started again afterwards. A copy that was not running is started only when `-Start` is given
- The application's files are replaced, and files of earlier versions that are no longer published are removed
- The installed copy runs in per-user mode, so the user's files are in `%APPDATA%\UrDeck` and the command does not
  touch them. It does not write a configuration: the first start creates it (see data-folder, "First Run")
- **One-time move:** when the destination holds user files from before the data folder existed
  (`urdeck-config.json`, `urdeck.log`, `urdeck.log.1`, `cache/`, `themes/`, `backgrounds/`, or files in `plugins/` that
  the publish did not produce) and `%APPDATA%\UrDeck` has no `urdeck-config.json`, those files are moved there and the
  command says so. When `%APPDATA%\UrDeck` already has a configuration, nothing is moved, the old files stay where
  they are and the command names them
- It does not register the app to start with Windows; that stays the user's choice in the tray menu
- A failure (the publish fails, the running copy does not close, a file cannot be replaced or moved) stops the command
  with a message and a non-zero exit code, and leaves the user's files as they were

#### Scenario: First install
- **WHEN** the command is run and the destination does not exist
- **THEN** the destination holds a runnable copy without a configuration, and its first start creates
  `%APPDATA%\UrDeck\urdeck-config.json` from the sample page

#### Scenario: Update keeps the user's files
- **WHEN** the command is run over an installed copy whose configuration in `%APPDATA%\UrDeck` was edited and whose
  data folder's `themes/`, `backgrounds/` and `plugins/` hold files the user added
- **THEN** the application's files are the new ones, and everything in `%APPDATA%\UrDeck` is unchanged

#### Scenario: Install over a copy from before the data folder
- **WHEN** the destination holds `urdeck-config.json`, `themes/mine/` and a user plugin next to the executable, and
  `%APPDATA%\UrDeck` does not exist
- **THEN** those files are in `%APPDATA%\UrDeck` afterwards, none of them is left in the destination, and the started
  copy shows the same page

#### Scenario: Update while running
- **WHEN** the command is run while the installed copy is running
- **THEN** the running copy closes, the files are replaced and the new copy is running when the command ends

#### Scenario: Running copy does not close
- **WHEN** the installed copy is running and does not end within the time `--quit` waits
- **THEN** the command stops with a message, replaces nothing and the running copy is left as it is
