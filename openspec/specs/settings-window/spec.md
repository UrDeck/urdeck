# settings-window Specification

## Purpose
The window the user opens from the tray icon to change how UrDeck is set up without editing files. This version has
one page, the monitor picker, which chooses the display the deck is shown on.
## Requirements
### Requirement: Opening The Settings Window
The host MUST open the settings window when the user asks for it from the tray icon, and keep at most one open.

- A left click on the tray icon and the tray menu's Settings entry do the same thing (see tray-icon)
- The window is an ordinary window: it takes focus when it opens, has a title bar, can be moved and closed, is not
  resizable, and has a taskbar button for as long as it is open. Closing it removes the taskbar button. The
  notification-area icon is not affected
- Asking again while it is open brings the existing window to the front, restores it if it was minimised, and gives
  it focus; no second window appears
- It opens centred on the display under the pointer. When that display is the deck's display and another display is
  connected, it opens on the primary display, or on the first other display if the deck is on the primary one. When
  the deck's display is the only display, it opens there, above the deck
- Opening, using and closing it never gives focus to the deck window, and the deck keeps running, repainting and
  taking taps while it is open
- While the deck is hidden because its display is absent, the window can still be opened
- Escape closes it

#### Scenario: Escape closes it
- **WHEN** the settings window has focus and the user presses Escape
- **THEN** the window closes and the deck is unaffected

#### Scenario: Open from the tray
- **WHEN** the user left-clicks the tray icon
- **THEN** the settings window appears with focus and a taskbar button

#### Scenario: Closing removes the taskbar button
- **WHEN** the user closes the settings window
- **THEN** its taskbar button is gone, the tray icon remains, and the deck is unaffected

#### Scenario: Open twice
- **WHEN** the settings window is open behind another application's window and the user left-clicks the tray icon
- **THEN** the same window comes to the front with focus and no second window exists

#### Scenario: Opens away from the deck
- **WHEN** the deck is on a 1100×3840 panel, the primary display is another monitor, and the pointer is over the panel
- **THEN** the window opens centred on the primary display

#### Scenario: Single display
- **WHEN** the deck is on the only connected display
- **THEN** the window opens centred on it, in front of the deck

#### Scenario: Deck absent
- **WHEN** the deck's display is not connected and the user left-clicks the tray icon
- **THEN** the settings window opens on the display under the pointer

### Requirement: Monitor Picker
The settings window MUST show every connected display and let the user choose the deck's display.

- Each connected display is a row with its friendly name (the display's own name, or when it reports none, a generic
  name), its resolution in physical pixels, its orientation (landscape or portrait), a **Primary** badge when
  it is the primary display, its position relative to the other displays (such as "Left of Display 1"; none when it is
  the only display), and a number. The number is the display's place in the list, and the same number is what Identify
  shows. The list is ordered by position on the desktop, left to right, then top to bottom
- The display the deck is on is marked as the deck's display
- When the configuration remembers a display that is not connected, its name is shown as an extra row that cannot
  be chosen and says the deck is waiting for it. It has no number
- Choosing a connected row moves the deck to it at once (see display-targeting, "Explicit Display Choice")
- A **Choose automatically** button hands the choice back to `monitorName` and `monitor`. It is disabled while the
  display is already chosen that way, that is, while the remembered display has a `selector` or none is remembered
- The user is never shown a device name such as `DISPLAY2`, a scaling percentage or a device path; Identify and the
  number are how a display is told from another with the same name
- The list shows the displays in their state when the window opened and again whenever the displays change while it
  is open (a display connected or disconnected, a resolution or the arrangement changed). The selected row, the
  badges and the waiting row follow. The window does not poll

#### Scenario: List of displays
- **WHEN** a 2560×1440 display "DELL U2723QE" (primary) and a 1100×3840 panel "HYTE Y70Touch" to its right are connected
- **THEN** the list has two rows: "DELL U2723QE", 2560×1440, landscape, Primary, number 1, and "HYTE Y70Touch",
  1100×3840, portrait, "Right of Display 1", number 2

#### Scenario: Deck's display marked
- **WHEN** the deck is on "HYTE Y70Touch"
- **THEN** that row is marked as the deck's display and the other is not

#### Scenario: Two displays with the same name
- **WHEN** two connected displays are both named "DELL U2723QE"
- **THEN** both rows exist, with different numbers and positions

#### Scenario: Display reports no name
- **WHEN** a connected display reports no friendly name
- **THEN** its row shows a generic name, its resolution and its position

#### Scenario: Remembered display not connected
- **WHEN** the remembered display "HYTE Y70Touch" is disconnected
- **THEN** the list has a row for it that cannot be chosen and says the deck is waiting for it, besides the connected displays

#### Scenario: Choosing a display
- **WHEN** the user chooses the row "HYTE Y70Touch"
- **THEN** the deck covers that display and the row is marked as the deck's display

#### Scenario: Choose automatically
- **WHEN** a display was chosen explicitly and the user presses Choose automatically
- **THEN** the deck is on the display the selectors pick, and the button is disabled

#### Scenario: Already automatic
- **WHEN** the remembered display has a `selector`
- **THEN** Choose automatically is disabled

#### Scenario: Display unplugged while open
- **WHEN** the settings window is open and a display is disconnected
- **THEN** the list loses that row without being reopened, or shows it as waiting if it is the remembered display

### Requirement: Identify
The settings window MUST be able to show which physical display each row is.

- An **Identify** action shows the number of each connected display, large, in a corner of that display, on every
  connected display at once, and removes all of them after about two seconds
- A marker never takes focus, does not appear in the taskbar or the window switcher, and does not block the display's
  content other than the area it covers. The deck's own display gets one too
- Choosing Identify again while markers are showing restarts the two seconds without adding markers
- Closing the settings window removes any marker

#### Scenario: Identify
- **WHEN** two displays are connected and the user presses Identify
- **THEN** "1" appears on the first and "2" on the second, and both are gone after about two seconds

#### Scenario: Identify does not take focus
- **WHEN** the user presses Identify
- **THEN** the settings window keeps focus

#### Scenario: Closed while markers show
- **WHEN** the user closes the settings window while markers are showing
- **THEN** the markers disappear

### Requirement: Cost Of The Settings Window
Opening and closing the settings window MUST NOT raise the host's idle cost.

- With the window closed, the host's idle CPU and GPU use are those of the deck without a settings window
- After the window is opened and closed 20 times and the host has run a garbage collection, its private bytes
  are within 10 MB of what they were before the first opening, and do not keep rising from one cycle to the next
- While the window is open and nothing changes, it uses no measurable CPU
- The measured numbers are recorded in `docs/perf/settings-window.md`

#### Scenario: Open and close twenty times
- **WHEN** the window is opened and closed 20 times
- **THEN** the host's private bytes after a garbage collection are within 10 MB of the starting value

#### Scenario: Idle while open
- **WHEN** the window is open on the monitor page and no display changes
- **THEN** the host process uses no measurable CPU for it

