## ADDED Requirements

### Requirement: Opening A Provider's Settings
The launcher MUST treat the target `urdeck://settings/providers/<provider id>` as a request to the host and never hand it to
the Windows shell. The host opens the editor if it is closed (a second window is never made) and shows the Providers
section on that provider. An id that names no loaded provider opens the section without a selection and logs one line.
Like any other launch it happens during the tap that asked for it, is not repeated within one second, and reports whether
it was accepted. Where the host has no editor to open (a preview, `--snapshot`), the launch reports failure and logs one
line. The SDK MUST offer a way to build the target from a provider id so a widget need not know the text.

#### Scenario: Tap on a notice
- **WHEN** a widget launches the settings target for `steam` while the editor is closed
- **THEN** the editor opens on the Providers section with Steam focused

#### Scenario: Editor already open
- **WHEN** the editor is open on the Pages section and the target for `homeassistant` is launched
- **THEN** the same window shows the Providers section with Home Assistant focused

#### Scenario: Unknown provider
- **WHEN** the target names a provider that is not loaded
- **THEN** the Providers section opens with nothing focused, and the log has one line naming the id
