## MODIFIED Requirements

### Requirement: Data Folder Location
The host MUST keep the user's files in one data folder, chosen once at startup before anything is read or logged:

- **Portable mode:** when a file named `urdeck.portable` is in the application folder (the folder of
  `UrDeck.Host.exe`), the data folder is the application folder. The marker's content is ignored
- **Portable release:** when the copy runs from the portable zip of a release (see release-process, "Release Assets"), the
  data folder is the folder the zip was unpacked into, which holds the application folder and is not replaced by an
  update. The log names it as `Data folder: <path> (portable release)`
- **Per-user mode:** otherwise the data folder is `UrDeck` in the user's roaming application data folder
  (`%APPDATA%\UrDeck`)
- The choice is made once per process. Adding or removing the marker takes effect at the next start
- Every command of the host uses the same data folder: the deck, `--snapshot`, `--quit` and `--demo`
- The first line the host writes to the log names the data folder and the mode, as
  `Data folder: <path> (per-user)` or `Data folder: <path> (portable: urdeck.portable next to the executable)`
- When the per-user data folder cannot be created or written, the host logs the reason to the system's debug output,
  shows no deck and exits with code 1

#### Scenario: Installed copy
- **WHEN** the host starts from a folder without `urdeck.portable`
- **THEN** its configuration, log, themes, backgrounds, user plugins and cache are in `%APPDATA%\UrDeck`

#### Scenario: Portable copy
- **WHEN** the host starts from a folder that holds `urdeck.portable`
- **THEN** its configuration, log, themes, backgrounds, plugins and cache are in that folder, and nothing is written
  to `%APPDATA%\UrDeck`

#### Scenario: Portable release survives its update
- **WHEN** the portable zip of `0.1.0` is unpacked into `D:\UrDeck`, the page is edited, and the copy updates itself to
  `0.2.0`
- **THEN** the configuration is in `D:\UrDeck`, not in the application folder inside it, and the updated copy shows the
  edited page

#### Scenario: Log names the folder
- **WHEN** the host starts in per-user mode
- **THEN** the first line of this run in `%APPDATA%\UrDeck\urdeck.log` is `Data folder: <that folder> (per-user)`

#### Scenario: Marker added while running
- **WHEN** `urdeck.portable` is created next to the executable while the host runs in per-user mode
- **THEN** the host keeps using `%APPDATA%\UrDeck` until it is started again
