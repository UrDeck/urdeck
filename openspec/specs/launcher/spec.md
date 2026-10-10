# launcher Specification

## Purpose
Defines the host's launch service: what a widget may ask the host to open, how a target is understood and started, and
what is refused, so that every widget that launches something behaves like a desktop shortcut and follows one policy.
## Requirements
### Requirement: Launch Targets
The launch service MUST accept a target text and optional arguments, and MUST classify the target without touching the
disk or the network:

- A target that starts with a URI scheme of two or more characters followed by a colon is an address: `http` and
  `https` are web addresses, `shell:` is a shell item (for example a packaged application under `shell:AppsFolder`),
  and any other scheme is handed to whatever application Windows has registered for it
- Anything else is a file target: a rooted path to a file, a folder, an executable or a `.lnk`, or a bare name with no
  path separator that Windows resolves itself (for example `notepad`)
- Environment variables written as `%NAME%` in a file target are expanded
- A relative path that contains a separator, an empty target, and a web address that is not a well-formed absolute
  address are invalid
- Arguments apply to file targets only; arguments given with an address are ignored with a logged warning

The classification MUST be available to a widget before it launches, so that a widget can tell a valid target from an
invalid one without starting anything.

#### Scenario: Web address
- **WHEN** the target is `https://homeassistant.example.com`
- **THEN** it is a web address

#### Scenario: Packaged application
- **WHEN** the target is `shell:AppsFolder\Microsoft.WindowsTerminal_8wekyb3d8bbwe!App`
- **THEN** it is a shell item

#### Scenario: Drive letter is not a scheme
- **WHEN** the target is `C:\Program Files (x86)\Steam\steam.exe`
- **THEN** it is a file target

#### Scenario: Environment variable
- **WHEN** the target is `%USERPROFILE%\Documents`
- **THEN** it is a file target that names the user's documents folder

#### Scenario: Relative path
- **WHEN** the target is `tools\run.exe`
- **THEN** it is invalid

#### Scenario: Other scheme
- **WHEN** the target is `steam://open/main`
- **THEN** it is an address handed to the application registered for `steam`

### Requirement: Launching
The launch service MUST start a valid target the way Windows starts a desktop shortcut, and MUST report whether the
start was accepted.

- The target is handed to the Windows shell with the default action. It is never passed through a command interpreter
  and never started elevated
- For an executable file target the working directory is the executable's folder
- The launch happens during the handling of the tap that asked for it, not later, so that Windows lets the launched
  application come to the front
- Whether a running application is focused or a second instance or window is opened is decided by the application, as
  it is for a desktop shortcut
- The service does not move, resize or wait for the launched window
- A target that is invalid, or that Windows refuses (file not found, no application registered), starts nothing; the
  service reports failure and logs one line naming the target and the reason. It MUST NOT throw to the widget
- A second request for the same target and arguments within one second of a started launch is ignored and reported as
  started, so a double tap opens one window
- Every started launch is logged with its target; arguments are not logged

#### Scenario: Launch an application that is not running
- **WHEN** a widget asks to launch the path of an installed application that is not running
- **THEN** the application starts and its window comes to the front

#### Scenario: Launch a web address
- **WHEN** a widget asks to launch an `https` address
- **THEN** the default browser opens the address and comes to the front

#### Scenario: Missing file
- **WHEN** a widget asks to launch a path that does not exist
- **THEN** nothing starts, failure is reported, and the log names the target and the reason

#### Scenario: Double tap
- **WHEN** the same target is requested twice within half a second
- **THEN** it is started once

#### Scenario: Arguments stay out of the log
- **WHEN** a target is launched with arguments
- **THEN** the log line shows the target and not the arguments

### Requirement: The Deck Keeps Its Place
Launching MUST NOT change the deck's own window: it stays where it is, stays on top of its display as before, and
does not take keyboard focus for itself after the launch has been handed over.

#### Scenario: After a launch
- **WHEN** a shortcut has launched an application
- **THEN** the launched application has the focus and the deck is still shown on its display

