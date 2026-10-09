## MODIFIED Requirements

### Requirement: Host Window
The host application MUST provide a borderless window that:

- Covers the target monitor's full physical bounds (not the work area) with its content area exactly: no window border
  or frame is visible inside the monitor's bounds
- Cannot be resized or moved by the user
- Is process-wide PerMonitorV2 DPI aware; monitors are enumerated in physical pixels
- Re-applies the exact bounds when the monitor's scaling changes or the window moves to a monitor with different scaling
- Sits in normal Z-order (does not use topmost) and does NOT use window transparency
- Never takes focus: touching, clicking or dragging on it MUST NOT make it the foreground window or move keyboard
  focus away from the window that has it, so using the deck does not interrupt a game or any other application. As a
  consequence the window receives no keyboard input and does not close on Escape
- Is not required to have a taskbar button; it is closed from the tray icon (see tray-icon), or by Escape in the
  development override
- Starts activatable, so that it can be given focus for development, when the environment variable
  `URDECK_ACTIVATABLE` is set to `1`; in that case Escape closes the window

#### Scenario: Content area covers the full monitor
- **WHEN** the host starts on a 1100×3840 target monitor
- **THEN** the window's content area is 1100×3840 physical pixels at the monitor's origin
- **THEN** a mismatch between the content area and the target bounds is logged as a warning

#### Scenario: Window survives display changes
- **WHEN** display settings change, or the machine resumes from sleep
- **THEN** the host re-selects the target monitor and re-covers it, re-checking after 1.5s and again after 5s because
  monitors wake in arbitrary order

#### Scenario: Page returns after sleep
- **WHEN** the machine resumes from sleep, or the graphics device is reset
- **THEN** the window background and every widget are shown again without restarting the host

#### Scenario: Tap leaves focus alone
- **WHEN** another application's window has keyboard focus and the user taps the panel
- **THEN** the other window keeps focus and stays in front of the host window
- **THEN** a fullscreen application is not minimised

#### Scenario: Escape does nothing
- **WHEN** the host window is shown and the user presses Escape on the keyboard
- **THEN** the host keeps running

#### Scenario: Closed from the tray
- **WHEN** the user chooses Quit from the tray icon's menu
- **THEN** the host window closes and the process ends

#### Scenario: Development override
- **WHEN** the host is started with `URDECK_ACTIVATABLE=1`
- **THEN** the window can take focus and Escape closes it
