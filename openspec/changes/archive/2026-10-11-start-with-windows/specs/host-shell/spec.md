## ADDED Requirements

### Requirement: Single Instance
At most one host MUST run from one application folder for one signed-in user:

- A host that starts while another host from the same folder is running writes one line to the log, shows no window
  and no tray icon, loads no plugin, does not write the configuration, and exits with code 0
- Hosts in different folders do not exclude each other, so a development build can run beside an installed copy
- `--snapshot` and `--quit` are not hosts in this sense: they run while a host is running and do not prevent one
  from starting
- The folder is compared without regard to case
- When the first host ends by any route, including being terminated, a new one can start at once

#### Scenario: Second copy from the same folder
- **WHEN** a host is running and `UrDeck.Host.exe` in the same folder is started again
- **THEN** the second process exits with code 0, the log has one line saying another copy is running, and there is
  still one tray icon and one deck

#### Scenario: Development build beside the installed copy
- **WHEN** the installed copy is running and a host is started from a build output folder
- **THEN** both run, each with the configuration and log of its own folder

#### Scenario: Snapshot while running
- **WHEN** a host is running and `--snapshot out.png` is run from the same folder
- **THEN** the PNG is written and the running host is unaffected

#### Scenario: After a crash
- **WHEN** the running host is terminated and `UrDeck.Host.exe` is started again
- **THEN** the new host starts normally

### Requirement: Quit Command
`UrDeck.Host.exe --quit` MUST ask the host that runs from the same folder to close and wait for it:

- The running host closes as it does for the tray's Quit: the window closes, the configuration is saved, the tray icon
  is removed and the process ends
- The command exits with 0 once that host has ended, and with 0 at once when no host from that folder is running
- It exits with 1 when the host has not ended after 10 seconds, and logs that
- It shows no window, loads no plugin and does not write the configuration
- It only reaches a host of the same signed-in user

#### Scenario: Quit a running host
- **WHEN** a host is running and `--quit` is run from the same folder
- **THEN** the deck and the tray icon disappear, the log shows the same shutdown lines as after the tray's Quit, and
  the command exits with 0

#### Scenario: Nothing running
- **WHEN** `--quit` is run and no host from that folder is running
- **THEN** the command exits with 0 and starts nothing

#### Scenario: Another folder's host
- **WHEN** a host from another folder is running and `--quit` is run
- **THEN** that host keeps running and the command exits with 0

### Requirement: Log File
`urdeck.log` MUST stay readable and bounded over a run of weeks:

- Every line starts with the local date and time as `[yyyy-MM-dd HH:mm:ss.fff]`, followed by the level and the message
- When the file is larger than 5 MB at startup, or passes 5 MB while the host runs, it is renamed to `urdeck.log.1`,
  replacing an earlier `urdeck.log.1`, and a new `urdeck.log` is started. The two files together are therefore never
  much larger than 10 MB
- A rename that fails (the file is open in another program) is not an error: logging continues in the same file and
  the rename is tried again later
- Logging never throws into the caller and never blocks the user interface for longer than one file write

#### Scenario: Lines carry the date
- **WHEN** the host writes a line on 11 October 2026 at 07:15:02.250
- **THEN** the line starts with `[2026-10-11 07:15:02.250]`

#### Scenario: Log passes the cap
- **WHEN** `urdeck.log` passes 5 MB while the host runs
- **THEN** its content is in `urdeck.log.1`, a new `urdeck.log` holds the lines written after that, and no line is
  lost

#### Scenario: Second rotation
- **WHEN** `urdeck.log` passes 5 MB and `urdeck.log.1` already exists
- **THEN** the earlier `urdeck.log.1` is replaced

#### Scenario: Log open in an editor
- **WHEN** the log is due to be renamed and another program holds it open without sharing
- **THEN** the host keeps running and keeps logging once the file can be written again
