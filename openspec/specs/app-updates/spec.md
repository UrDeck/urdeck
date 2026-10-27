# app-updates Specification

## Purpose
Defines how UrDeck is installed, kept up to date and removed on a user's PC: the per-user installer, the update check
of an installed copy, the setting that turns it off, and uninstalling.
## Requirements
### Requirement: Installer
`UrDeck-win-Setup.exe` MUST install UrDeck for the current user:

- It needs no administrator rights and installs into `%LocalAppData%\UrDeck`
- It adds a Start menu shortcut named UrDeck and no desktop shortcut
- It starts the deck when it finishes, which creates the configuration on first run (see data-folder, "First Run")
- Run with `--silent` it shows no window and asks nothing
- Installing over an installed copy installs the newer version and keeps the data folder
- It does not register the app to start with Windows; that stays the user's choice in the tray menu

#### Scenario: Fresh install
- **WHEN** a user without UrDeck runs `UrDeck-win-Setup.exe`
- **THEN** no elevation prompt appears, the deck starts with the sample page, and UrDeck is in the Start menu

#### Scenario: Silent install
- **WHEN** `UrDeck-win-Setup.exe --silent` is run
- **THEN** no window of the installer is shown and the app is installed

### Requirement: Update Check
An installed copy MUST find, download and apply newer releases by itself:

- It reads the releases of the GitHub repository `UrDeck/urdeck` about one minute after start and then every 24 hours.
  Nothing else is sent, and no account or token is used
- A copy whose version has no prerelease label takes only stable releases. A copy on a prerelease takes the newest
  release, prerelease or stable
- A newer release is downloaded in the background. When it is ready, the tray menu offers "Restart to update to
  <version>" (see tray-icon, "Tray Menu"); choosing it closes the deck, applies the update and starts the new version
- An update that is ready and not chosen is applied when the deck quits, so the next start runs the new version
- The log names each step: the check and its result, the download, and the update applied
- A failed check or download logs a warning and is tried again at the next check; the deck is never interrupted
- A copy that the installer did not install (a build output, a copy from `dotnet publish`) never checks

#### Scenario: Update offered
- **WHEN** `0.1.0` is installed and `0.2.0` is published
- **THEN** within a day the tray menu offers "Restart to update to 0.2.0", and choosing it restarts the deck as `0.2.0`
  with the same page

#### Scenario: Applied on quit
- **WHEN** an update is ready and the user chooses Quit
- **THEN** the next start runs the new version

#### Scenario: Stable copy and a prerelease
- **WHEN** `0.1.0` is installed and only `0.2.0-rc.1` is newer
- **THEN** no update is offered

#### Scenario: Offline
- **WHEN** the check runs without a network
- **THEN** a warning is logged and the deck keeps running

#### Scenario: Development build
- **WHEN** the host runs from a build output folder
- **THEN** no request to GitHub is made

### Requirement: Update Setting
The configuration's `updates` key MUST control the update check:

- `auto` (the default, and what an absent key means) checks, downloads and offers as "Update Check" says
- `off` makes no request and offers nothing; an update already downloaded is not applied
- Another value logs a warning and is treated as `auto`
- A change takes effect when the configuration is reloaded

#### Scenario: Updates off
- **WHEN** the configuration has `"updates": "off"` and a newer release exists
- **THEN** no request to GitHub is made and the tray menu has no update entry

### Requirement: Uninstall
Uninstalling UrDeck from Windows' installed apps MUST remove the app and its secrets, and leave the user's files:

- The deck is closed first
- The Start with Windows registration of this copy is removed
- Every Credential Manager credential whose target starts with `UrDeck/` is deleted (see provider-secrets), so nothing
  UrDeck stored for a service stays behind
- The application folder and the Start menu shortcut are removed
- The data folder (`%APPDATA%\UrDeck`) is left as it was, so a later install shows the same page

#### Scenario: Uninstall and reinstall
- **WHEN** an installed copy with Start with Windows on is uninstalled and then installed again
- **THEN** after the uninstall nothing of UrDeck starts at sign-in, and after the new install the deck shows the page
  the user had

#### Scenario: Secrets removed
- **WHEN** an installed copy with a Home Assistant token stored is uninstalled
- **THEN** Credential Manager no longer lists `UrDeck/homeassistant/token`, and after a new install the provider reports
  the missing token

