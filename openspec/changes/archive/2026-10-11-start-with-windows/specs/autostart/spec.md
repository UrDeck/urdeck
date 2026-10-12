## Purpose

Defines how UrDeck starts when the user signs in to Windows: what is registered, the states the user can see, and how
the tray entry changes them. Windows holds the setting; the configuration file does not.

## ADDED Requirements

### Requirement: Start With Windows
The host MUST be able to register itself to start when the current user signs in:

- The registration is per user and needs no elevation
- It names the executable of the copy that made it, quoted, followed by the argument `--autostart`
- It is visible to the user where Windows lists startup apps (Task Manager, Settings), under the name "UrDeck"
- Nothing is registered, changed or removed unless the user chooses the tray entry (see "Autostart Entry"): starting,
  running and closing the host leave the registration as it is, and `--snapshot` and `--quit` never touch it
- A copy started by the registration behaves like one started by hand, and logs that Windows started it at sign-in

#### Scenario: Registered copy starts at sign-in
- **WHEN** the user has switched "Start with Windows" on and signs in to Windows again
- **THEN** UrDeck starts without a prompt, the deck appears on its display and the log says it was started at sign-in

#### Scenario: Never switched on
- **WHEN** the user has never chosen the entry
- **THEN** nothing in the registry names UrDeck and UrDeck does not start at sign-in

#### Scenario: Started from another working directory
- **WHEN** Windows starts the registered copy with a working directory that is not the application's folder
- **THEN** the configuration, plugins, themes and log of the application's folder are used

### Requirement: Autostart State
The host MUST report what Windows will do at the next sign-in, read from Windows each time it is asked, as one of:

- **On**: this copy is registered and Windows has it enabled
- **Off**: nothing is registered
- **Another copy**: a UrDeck executable in a different folder is registered
- **Switched off in Windows**: this copy is registered, and the user disabled it in Task Manager or Settings

Paths are compared without regard to case. A registration the host cannot read is treated as Off and logged once.

#### Scenario: Disabled in Task Manager
- **WHEN** the entry was switched on and the user then disables UrDeck in Task Manager's startup apps
- **THEN** the state is "Switched off in Windows" and UrDeck does not start at the next sign-in

#### Scenario: Registration points at a moved copy
- **WHEN** the registered executable is `D:\old\UrDeck.Host.exe` and the running copy is in another folder
- **THEN** the state is "Another copy"

### Requirement: Autostart Entry
The tray menu MUST have an entry "Start with Windows" (see tray-icon, "Tray Menu"):

- It shows a check mark only in the state On
- Choosing it in the state On removes the registration
- Choosing it in any other state registers the running copy and makes Windows enable it, so that the state is On.
  When that replaces the registration of another copy, the log names the executable that was registered before
- Each change is logged with the state before and after. A change that fails (the registry cannot be written) is
  logged as a warning, and the entry shows the state that is actually in effect the next time the menu opens
- The state is read when the menu opens, so a change made in Task Manager or Settings while the host runs is shown
  without a restart

#### Scenario: Switched on
- **WHEN** the state is Off and the user chooses "Start with Windows"
- **THEN** the running copy is registered, and the entry has a check mark when the menu is opened again

#### Scenario: Switched off
- **WHEN** the state is On and the user chooses "Start with Windows"
- **THEN** the registration is removed, the entry has no check mark and UrDeck does not start at the next sign-in

#### Scenario: Switched on again after disabling it in Windows
- **WHEN** the state is "Switched off in Windows" and the user chooses "Start with Windows"
- **THEN** the state is On and Task Manager shows UrDeck as enabled

#### Scenario: Switched on from a second copy
- **WHEN** a copy in `C:\dev\bin` is running, the registered executable is in `C:\Users\me\AppData\Local\Programs\UrDeck`
  and the user chooses "Start with Windows" in the copy from `C:\dev\bin`
- **THEN** the copy in `C:\dev\bin` is the one registered and the log names the executable it replaced

#### Scenario: Changed outside the host
- **WHEN** the entry has a check mark and the user disables UrDeck in Task Manager while the host runs
- **THEN** the entry has no check mark the next time the menu is opened
