## ADDED Requirements

### Requirement: Specialized Displays As Deck Displays
The host MUST treat a connected specialized display (a monitor Windows has removed from the desktop) as a display the
deck can be on, alongside the desktop monitors, and MUST keep a display's identity when it moves between the two.

- The connected displays are the desktop monitors and the specialized displays that are not head-mounted. Each has a
  kind, desktop or specialized, which is logged with it
- A remembered display is matched by the rules of "Matching A Remembered Display" over both kinds: the device path,
  then the model code. A display that was remembered as a desktop monitor and is now specialized (or the reverse) is
  the same display, and the deck follows it (see direct-output, "Presenting On A Specialized Display")
- `display` records the kind the display was last seen as; the host updates it and writes the configuration when the
  kind changes
- With no remembered display, when one or more specialized displays are connected, the selectors choose among the
  specialized displays only; otherwise among the desktop monitors as before. The log says which set was used
- A specialized display that cannot be used (held by another app, a remote session) counts as absent for "Absent
  Display", with that reason in the log and the tray

#### Scenario: Toggle keeps the remembered display
- **WHEN** the panel is remembered as a desktop monitor and the user removes it from the desktop
- **THEN** the remembered display still names the panel, its kind becomes specialized, and the deck is on the panel

#### Scenario: First run with a specialized panel
- **WHEN** the configuration has no `display`, `monitorName` is `"tallest"`, the primary monitor is on the desktop,
  and the panel is specialized
- **THEN** the deck is on the panel and the configuration remembers it

#### Scenario: Specialized display in use by another app
- **WHEN** the remembered display is specialized and another app holds it
- **THEN** the deck is not shown on any other display, and the tray and the log say the display is in use
