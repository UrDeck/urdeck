# display-targeting Specification

## Purpose
Defines which physical display the deck belongs to and how the host keeps the deck on it: the identity that is
remembered for a display, how a connected display is matched to it, how the host converges to a verified placement
after display changes, and what happens while that display is not connected.

## Requirements
### Requirement: Remembered Display
The host MUST remember the physical display the deck is shown on and follow that display, not its position in the
enumeration order or its `DISPLAYn` device name.

- The remembered display is stored in the configuration as `display`, with a readable name, a model code and a device
  path (see host-shell, "Configuration Management")
- When the configuration has no remembered display, the host chooses one with the selectors (see host-shell, "Monitor
  Selection") and remembers the result. The choice is logged with the display's name
- A remembered display records the selector it was chosen with. When the selectors in the configuration no longer
  match that record, the host chooses again and replaces the remembered display. A remembered display without such a
  record is kept whatever the selectors say
- The user is never required to know a display's resolution, device name or scaling factor

#### Scenario: First run remembers the display
- **WHEN** the configuration has `monitorName: "tallest"` and no `display`, and a 1100×3840 panel named
  "HYTE Y70Touch" is the tallest portrait display
- **THEN** the deck covers that panel and the configuration file gains a `display` entry naming it

#### Scenario: A taller display is added later
- **WHEN** the panel is remembered and a taller portrait display is connected
- **THEN** the deck stays on the panel

#### Scenario: Selector edited
- **WHEN** the panel is remembered from `tallest` and the user changes `monitorName` to `primary` and saves
- **THEN** the deck moves to the primary display and that display is remembered instead

#### Scenario: Remembered display removed from the file
- **WHEN** the user deletes the `display` entry and saves
- **THEN** the host chooses again with the selectors and remembers the result

### Requirement: Matching A Remembered Display
The host MUST find the remembered display among the connected displays in this order:

1. The display with the same device path
2. Otherwise, the display with the same model code, when exactly one connected display has it
3. Otherwise, when several connected displays have that model code, the first of them in device path order; the
   ambiguity is logged as a warning naming the candidates
4. Otherwise the remembered display is absent (see "Absent Display")

When the display is found by its model code, the host MUST update the remembered device path and log the change.
A different device name (`DISPLAYn`), position, resolution or scaling MUST NOT prevent a match.

#### Scenario: Enumeration order changes
- **WHEN** the remembered panel was `DISPLAY1` and is `DISPLAY3` after a restart
- **THEN** the deck is on the panel

#### Scenario: Moved to another output
- **WHEN** the remembered panel is plugged into another output of the graphics card, so its device path differs
- **THEN** the deck is on the panel and the configuration holds the new device path

#### Scenario: Two identical displays
- **WHEN** two connected displays have the remembered model code and neither has the remembered device path
- **THEN** the deck is on one of them, the same one on every start, and a warning names both

### Requirement: Verified Placement After Display Changes
After every display change the host MUST converge to a placement it has checked, without user action. A display change
is: a resume from sleep, a display connected or disconnected, a change of resolution, arrangement or scaling, and a
configuration reload.

- A placement is correct when the window's content area equals the target display's bounds in physical pixels, the
  window's scaling equals that display's scaling, and the laid-out page size matches the content area
- Display changes that arrive in a burst are handled together: the host waits until they stop for a short time before
  it places the window
- When the check fails the host places the window again, with a growing delay between attempts, until the check
  passes. After 30 seconds without a correct placement it logs a warning with what differs and waits for the next
  display change
- After a correct placement the host checks again twice, a few seconds apart, and starts over when a check fails. A
  check that passes does no further work
- The page is rebuilt at most once per display change, when placement is correct, and only when the size, the scaling
  or the display changed. After a resume from sleep it is always rebuilt once
- Every attempt, its result and the final state are logged

#### Scenario: Resume from sleep
- **WHEN** the computer wakes and the displays come back one after another, the panel last
- **THEN** the deck is on the panel, at the panel's size and scaling, without having been shown on another display
- **THEN** the page was rebuilt once

#### Scenario: Scaling settles late
- **WHEN** the panel reports the primary display's scaling for a moment after it is connected, and its own a second
  later
- **THEN** the deck ends up laid out for the panel's own scaling and the content area covers the panel exactly

#### Scenario: Mixed scaling
- **WHEN** the primary display uses 150% scaling and the panel uses 100%
- **THEN** the deck is rendered on the panel at its native resolution

#### Scenario: Nothing changed
- **WHEN** a display change is reported and the deck's placement is still correct
- **THEN** the window is not moved and the page is not rebuilt

#### Scenario: Placement cannot be reached
- **WHEN** the content area still differs from the target display's bounds after 30 seconds of attempts
- **THEN** a warning with the expected and the actual bounds is logged, the host keeps running, and the next display
  change starts new attempts

### Requirement: Absent Display
While the remembered display is not connected the host MUST NOT show the deck on any other display.

- The window is hidden as soon as a display change shows the display is gone, before any wait
- While hidden, no widget is refreshed or painted, and widgets hold no reading subscriptions, so providers stop as
  they do when their last widget is removed
- The host keeps running; the tray icon stays and says which display it is waiting for (see tray-icon)
- When the display is connected again the deck is placed on it, by the rules of "Verified Placement After Display
  Changes", and its page is rebuilt
- The reason, with the name of the remembered display and the connected displays, is logged when the deck is hidden
  and when it returns
- Starting with the remembered display absent is the same state: no window is shown

#### Scenario: Display switched off
- **WHEN** the panel is disconnected while the host runs
- **THEN** the deck is not visible on any display and its providers stop

#### Scenario: Display returns
- **WHEN** the panel is connected again
- **THEN** the deck appears on it with current values, without restarting the host

#### Scenario: Started without the display
- **WHEN** the host starts and the remembered display is not connected
- **THEN** no window is shown, the tray icon is present, and the log says which display is awaited

#### Scenario: Quit while hidden
- **WHEN** the deck is hidden and the user chooses Quit from the tray icon
- **THEN** the process ends
