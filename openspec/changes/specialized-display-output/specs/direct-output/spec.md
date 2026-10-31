## Purpose

Defines how the host shows the deck on a specialized display, a monitor that Windows has taken off the desktop and
that only a dedicated app can draw on: when this way of showing the deck is used, how the display is acquired and
released, what is drawn and when, touch, power, and what happens when the display is lost.

## ADDED Requirements

### Requirement: Presenting On A Specialized Display
When the deck's display (see display-targeting) is a specialized display, the host MUST show the deck by drawing
directly on that display, without a window, from the same running host that offers the tray icon and the settings
window. When the deck's display is a desktop monitor, the host MUST show the deck in its window as before.

- There is no setting for the way of presenting: it follows from the kind of the deck's display
- When the deck's display changes kind while the host runs (the user removes it from the desktop, or puts it back),
  the deck moves to the other way of presenting without a restart, keeping the current page, and the log says so
- On a Windows edition without specialized displays, or with none, nothing changes for the user
- Only one way of presenting is active at a time; the deck is never shown on two displays

#### Scenario: Panel removed from the desktop while running
- **WHEN** the deck is on the panel as a desktop monitor and the user turns on "Remove display from desktop" for it
- **THEN** within a few seconds the deck is on the panel again, drawn directly, on the same page, and the log says the
  deck moved to direct output

#### Scenario: Panel put back on the desktop
- **WHEN** the deck is drawn directly on the panel and the user turns "Remove display from desktop" off
- **THEN** the deck is shown in its window on the panel, on the same page

#### Scenario: Started with the panel specialized
- **WHEN** the host starts and the remembered display is a connected specialized display
- **THEN** the deck is drawn on it and no deck window is created

#### Scenario: Edition without specialized displays
- **WHEN** the host runs on Windows 11 Pro
- **THEN** the deck behaves as in host-shell and display-targeting, and the log has no direct output errors

### Requirement: Acquiring And Releasing The Display
The host MUST take the specialized display for its own use while it shows the deck there, and give it back when it
stops.

- The display is driven at its preferred resolution, with the refresh rate closest to 60 Hz and 8-bit colour
- Acquiring, the chosen mode, the orientation and the drawing path are logged once each time they change
- The display is released when the deck leaves it (another display chosen, the display put back on the desktop),
  when the host quits, and when the host process ends for any reason
- When another app holds the display, the host does not take it from that app; the deck waits (see "Losing The
  Display")
- The user is never shown or asked for a resolution, refresh rate or scaling

#### Scenario: Quit releases the display
- **WHEN** the deck is drawn on a specialized display and the user chooses Quit
- **THEN** the process ends and another app can acquire the display

#### Scenario: Display held by another app
- **WHEN** another app has acquired the specialized display before the host starts
- **THEN** the deck is not shown, the tray says the display is in use by another app, and the deck appears when the
  other app releases it

### Requirement: Orientation On A Specialized Display
The host MUST draw the deck upright on a specialized display.

- The orientation is `display.rotation` in the configuration: 0, 90, 180 or 270 degrees clockwise from the display's
  native scanout
- When `display.rotation` is absent, the host uses the orientation the display last had on the desktop if it saw
  one, otherwise the native orientation, and writes the value it chose
- The user changes it with the picker's Rotate action (see settings-window); a change applies at once, rebuilding the
  page
- Layout, theme and touch follow the rotated size: the deck on a 1100×3840 panel looks the same drawn directly as in
  its window

#### Scenario: Portrait panel with a landscape scanout
- **WHEN** the panel's native scanout is 3840×1100 and it was portrait on the desktop before it was removed
- **THEN** the deck is drawn as on a 1100×3840 portrait panel, and the configuration records the rotation

#### Scenario: Rotate
- **WHEN** the user presses Rotate for the deck's specialized display
- **THEN** the deck turns 90 degrees clockwise and the configuration's `display.rotation` changes

### Requirement: What Direct Output Draws
On a specialized display the deck MUST show what it shows in its window on a desktop monitor of the same size.

- The page, the dock, the page indicator, the theme's pattern, the background (shader, picture, gradient or colour),
  the scrim, moving card borders, press feedback, the page swipe and its settle, and the tap on an indicator dot
- A still frame matches what `--snapshot` draws for the same configuration, size and moment
- A widget that needs a view hosted by the desktop compositor shows a placeholder card naming what is not available
  on a specialized display, and the log names the widget once
- When the GPU cannot be used and drawing falls back to the processor, a moving background is drawn still and the log
  says why; everything else is unchanged

#### Scenario: Same picture as a snapshot
- **WHEN** the deck is drawn directly with motion off and the frame dump is compared with `--snapshot` at the same
  size and time
- **THEN** the pictures match within the golden tests' tolerance

#### Scenario: Swipe
- **WHEN** the user swipes up on a specialized panel with two pages
- **THEN** the next page follows the finger and settles as it does in the window

### Requirement: Frames On A Specialized Display
Direct output MUST draw only when something changed, and MUST follow the motion rules of the window.

- With nothing moving, no frame is drawn or presented until a widget, the page or the configuration changes; the
  display keeps showing the last frame
- While something moves (a moving background, an animating widget, a swipe, a moving border) frames are drawn at 30
  per second; the log has a started and stopped line with the frame count and duration, like the background's
- The motion level, Windows' animation setting and display off apply as in motion and backgrounds
- Widgets are called on one thread, as in the window

#### Scenario: Idle page
- **WHEN** a page with only a clock (no seconds) and stats is shown and nothing moves
- **THEN** a frame is presented when a value changes and none in between

#### Scenario: Moving background
- **WHEN** a theme's moving shader background is shown at the full motion level
- **THEN** the log reports about 30 frames per second when the frames stop

### Requirement: Touch On A Specialized Display
Taps, presses and swipes on a specialized touch panel MUST behave as on the same panel as a desktop monitor (see
widget-input and page-navigation), and MUST NOT act on any other display.

- Touches are read from the digitizer that belongs to the deck's display and mapped through the display's
  orientation; a touch lands on the widget under the finger within a few pixels
- A touch on the panel never moves the cursor, clicks, or opens anything on a desktop display
- Touches are read only while the deck is shown there; reading costs nothing at rest (see widget-input, "Input Costs
  Nothing At Rest")
- When no digitizer can be matched to the display, the deck is still drawn and the log says touch is unavailable and
  why; when several could match, the log names them and the host uses `display.touch` if the configuration names one
- Any one-time Windows setting the user needs so that the panel's touches do not reach the desktop is on the site's
  specialized displays page

#### Scenario: Tap a shortcut
- **WHEN** the user taps a shortcut widget on the specialized panel
- **THEN** its target is launched, the card shows press feedback, and nothing happens on the primary display

#### Scenario: Rotated panel
- **WHEN** `display.rotation` is 90 and the user taps the top-left card
- **THEN** the top-left card receives the tap

### Requirement: Power On A Specialized Display
Because Windows does not power a specialized display, the host MUST darken it whenever a desktop monitor would be
dark, and light it again afterwards.

- Dark while Windows has switched the displays off, while the session is locked, while the PC sleeps, and after the
  host ends (sign-out included: a released display must not keep the last frame lit); the panel is put into its
  standby where the hardware allows it, otherwise it shows black
- While dark, no frame is drawn, widgets hold no reading subscriptions, and motion stops, as for a hidden deck
- On display on, unlock and resume the deck returns with current values without user action; if the display has to
  be acquired again, it is, and the log says so
- Each change is logged (`display off`, `session locked`, `resume`) with the action taken

#### Scenario: Lock
- **WHEN** the user locks the PC
- **THEN** the panel goes dark and stays dark until the session is unlocked

#### Scenario: Sleep and resume
- **WHEN** the PC sleeps and resumes
- **THEN** the deck is on the panel again with current values within a few seconds

#### Scenario: Displays switched off by Windows
- **WHEN** Windows switches the displays off after the idle time
- **THEN** the panel goes dark, and lights again when the displays come back

### Requirement: Losing The Display
While the specialized display cannot be used, the host MUST behave as for an absent display (see display-targeting,
"Absent Display"), and MUST recover by itself.

- Reasons: not connected, held by another app, not available in a remote session, the drawing device was lost, or
  Windows refused the mode. Each is logged with the display's name, and the tray says what the deck is waiting for
- When the reason goes away (the display returns, the other app releases it, the session is local again, the device
  is recreated), the deck appears without a restart
- Retries follow the same limits as placement (a quiet period, then a bounded number of attempts with growing delay);
  the host never spins

#### Scenario: Unplugged and replugged
- **WHEN** the specialized panel is unplugged and plugged in again
- **THEN** the deck disappears, its providers stop, and it returns on the panel when it is back

#### Scenario: Remote Desktop
- **WHEN** the user connects to the PC with Remote Desktop
- **THEN** the log says direct output is not available in a remote session, and the deck returns when the session is
  local again

### Requirement: Diagnosing Direct Output
A developer MUST be able to see what direct output draws and decides without a screenshot.

- With `URDECK_DIRECT_DUMP` set to a folder, the host writes the frame it last presented as a PNG there at most once a
  second while frames change; without it nothing is written
- The log has a line for each decision: target found (name, kind, device path), acquired or why not, mode, orientation,
  drawing path (GPU or processor and why), touch device or why none, frames started and stopped, dark and lit with the
  reason, released

#### Scenario: Frame dump
- **WHEN** `URDECK_DIRECT_DUMP` names a folder and the deck is drawn directly
- **THEN** the folder holds a PNG of the current page at the display's rotated size
