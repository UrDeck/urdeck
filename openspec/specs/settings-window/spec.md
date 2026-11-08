# settings-window Specification

## Purpose
The window the user opens from the tray icon to change how UrDeck is set up without editing files. This version has
one page, the monitor picker, which chooses the display the deck is shown on.
## Requirements
### Requirement: Opening The Settings Window
The host MUST open the settings window, which is the editor (see editor), when the user asks for it from the tray icon,
and keep at most one open.

- A left click on the tray icon and the tray menu's Settings entry do the same thing (see tray-icon)
- The window is an ordinary window titled "UrDeck": it takes focus when it opens, has a title bar, can be moved,
  resized (down to a minimum size), maximised and closed, and has a taskbar button for as long as it is open. Closing
  it removes the taskbar button. The notification-area icon is not affected
- It opens at a comfortable size for editing a page, no larger than the display's work area
- Asking again while it is open brings the existing window to the front, restores it if it was minimised, and gives
  it focus; no second window appears
- It opens centred on the display under the pointer. When that display is the deck's display and another display is
  connected, it opens on the primary display, or on the first other display if the deck is on the primary one. When
  the deck's display is the only display, it opens there, above the deck
- Opening, using and closing it never gives focus to the deck window, and the deck keeps running, repainting and
  taking taps while it is open
- While the deck is hidden because its display is absent, the window can still be opened
- Escape closes it when no dialog, flyout or text entry has the key

#### Scenario: Escape closes it
- **WHEN** the settings window has focus, no dialog is open, and the user presses Escape
- **THEN** the window closes and the deck is unaffected

#### Scenario: Open from the tray
- **WHEN** the user left-clicks the tray icon
- **THEN** the settings window appears with focus and a taskbar button, showing the Pages section

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

#### Scenario: Resizing
- **WHEN** the user drags the window's edge to make it larger
- **THEN** the preview area grows and the page in it is scaled to the new size

### Requirement: Monitor Picker
The settings window's Display section MUST show every connected display and let the user choose the deck's display.

- Each connected display is a row with its friendly name (the display's own name, or when it reports none, a generic
  name), its orientation (landscape or portrait), a **Primary** badge when it is the primary display, its position
  relative to the other displays (such as "Left of Display 1"; none when it is the only display), and a number. The
  number is the display's place in the list, and the same number is what Identify shows. The list is ordered by
  position on the desktop, left to right, then top to bottom
- The display the deck is on is marked as the deck's display
- When the configuration remembers a display that is not connected, its name is shown as an extra row that cannot
  be chosen and says the deck is waiting for it. It has no number
- Choosing a connected row moves the deck to it at once (see display-targeting, "Explicit Display Choice")
- A **Choose automatically** button hands the choice back to `monitorName` and `monitor`. It is disabled while the
  display is already chosen that way, that is, while the remembered display has a `selector` or none is remembered
- The user is never shown a device name such as `DISPLAY2`, a resolution, a scaling percentage or a device path;
  Identify and the number are how a display is told from another with the same name
- The list shows the displays in their state when the window opened and again whenever the displays change while it
  is open (a display connected or disconnected, a resolution or the arrangement changed). The selected row, the
  badges and the waiting row follow. The window does not poll

#### Scenario: List of displays
- **WHEN** a 2560×1440 display "DELL U2723QE" (primary) and a 1100×3840 panel "HYTE Y70Touch" to its right are connected
- **THEN** the list has two rows: "DELL U2723QE", landscape, Primary, number 1, and "HYTE Y70Touch", portrait,
  "Right of Display 1", number 2, and neither shows a resolution

#### Scenario: Deck's display marked
- **WHEN** the deck is on "HYTE Y70Touch"
- **THEN** that row is marked as the deck's display and the other is not

#### Scenario: Two displays with the same name
- **WHEN** two connected displays are both named "DELL U2723QE"
- **THEN** both rows exist, with different numbers and positions

#### Scenario: Display reports no name
- **WHEN** a connected display reports no friendly name
- **THEN** its row shows a generic name, its orientation and its position

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
Opening and closing the settings window MUST NOT raise the deck's running cost once it is closed, and while it is open its
preview MUST cost about what a second deck showing the same page costs. The deck's cost after the window closes matters
more than the window's own cost while it is open, as long as the window stays usable.

- With the window closed, the host's idle CPU and GPU use are those of the deck without a settings window, also after the
  window was opened and closed
- The first opening may keep a one-time cost (WinUI's control styles and caches). After that, memory does not keep rising
  from one opening to the next: after the window is opened and closed 20 times and the host has run a garbage collection,
  its private bytes after the 20th cycle are within 10 MB of what they were after the 10th, and nothing of a closed window
  stays in memory
- While the window is open on a section other than Pages, or minimised, it uses no measurable CPU
- While the Pages section is open, the preview draws only when the page changes or something on it animates
- The measured numbers are recorded in `docs/perf/settings-window.md`

#### Scenario: Open and close twenty times
- **WHEN** the window is opened and closed 20 times
- **THEN** the host's private bytes after a garbage collection are within 10 MB of their value after the tenth cycle,
  and no closed window remains in memory

#### Scenario: The deck after the window closed
- **WHEN** the window was opened and closed and the deck keeps running
- **THEN** the deck's CPU use is that of a deck whose settings window was never opened

#### Scenario: Idle while open
- **WHEN** the window is open on the Display section and no display changes
- **THEN** the host process uses no measurable CPU for it

#### Scenario: Minimised
- **WHEN** the window is minimised while showing Pages with a moving background
- **THEN** the preview stops drawing until the window is restored

### Requirement: Providers Section
The settings window MUST have a Providers section listing each loaded provider that declares settings beyond the base
ones, with one field per declared setting, generated from the declaration: a text box for text, a password box for a
secret, a number box for a number and a switch for on/off, each with the setting's label and help text.

- A secret's field never shows a stored value. It says whether a value is stored and where it comes from (Credential
  Manager or the environment variable, named), and offers to save a new value or remove the stored one. While the
  environment variable is set, the field says it overrides the stored value
- Nothing is applied until the user saves the provider's settings. Saving writes non-secret settings to the configuration
  file and secrets to the secret store (see provider-secrets), and restarts that provider (see data-providers, "A
  Settings Change Restarts Only Its Provider")
- A provider MAY offer a connection test. The section then shows a Test connection button that runs it with the values
  in the form, without saving them, and shows its one-line result or failure under the button; the test is cancelled
  after 10 seconds or when the window closes
- The deck never shows a secret

#### Scenario: Entering a token
- **WHEN** the user types a Home Assistant address and token into the Providers section and saves
- **THEN** the address is in `urdeck-config.json`, the token is in Credential Manager, and Home Assistant readings on the
  deck start arriving

#### Scenario: Testing before saving
- **WHEN** the user presses Test connection with a wrong token
- **THEN** the section shows that the token was rejected, and nothing is saved

#### Scenario: No configurable providers
- **WHEN** no loaded provider declares settings beyond the base ones
- **THEN** the Providers section says so and shows no fields
