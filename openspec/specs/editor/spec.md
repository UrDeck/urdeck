# editor Specification

## Purpose

The editor is where a user builds and changes their deck without opening a file: it shows the page as the deck will
show it, and every change made in it reaches the deck at once and is saved.

## Requirements

### Requirement: Editor Sections
The editor window MUST be organised in sections in a navigation pane: **Pages** (the page editor), **Providers**,
**Display** and **General**, in that order; other changes add sections between them. Pages is the section shown when
the window opens. The Providers entry shows how many providers need settings before they can start, when any do.

#### Scenario: Opening on Pages
- **WHEN** the user opens the editor from the tray icon
- **THEN** the Pages section is shown, on the page the deck is currently showing

#### Scenario: A provider needs settings
- **WHEN** a page shows a Home Assistant reading and no Home Assistant address is set
- **THEN** the Providers entry in the navigation shows a badge with 1

### Requirement: Page Preview
The Pages section MUST show the selected page as the deck draws it: the same widgets, theme, background, dock and page
indicator, laid out for the deck's display, so that text fits and wraps as it does on the deck. The preview is never
an imitation of the deck's drawing.

- When the deck's display is not connected, the preview is laid out for the display the deck last used
- Two zooms: **Fit width** (the page fills the available width with cells about the size of a large icon, and
  scrolls vertically) and **Whole page** (the whole page, dock included, fits the area)
- A **Grid** switch shows the page's cells and a button on each empty cell to add a widget there
- The number of columns and rows comes from the deck's layout for its display, never from a fixed number
- The preview animates what the deck animates; it stops drawing while the window is minimised

#### Scenario: Same layout as the deck
- **WHEN** the deck is on a 1100×3840 panel and the user opens the editor on a 2560×1440 monitor
- **THEN** the preview shows the page in the panel's proportions with the same widgets in the same cells and the same
  text, scaled to fit the editor

#### Scenario: Whole page
- **WHEN** the user chooses Whole page
- **THEN** the full page including the dock is visible without scrolling

#### Scenario: Grid on
- **WHEN** the Grid switch is on and the page has empty cells
- **THEN** each empty cell shows an add button and the cell lines are visible; the page's own pixels are unchanged

### Requirement: Live And Sample Readings
The preview MUST show live readings by default and the providers' sample readings when the user switches to Sample.
In Live, a reading whose provider cannot start because a required setting is missing shows its sample value, so the
preview always shows what the widget looks like.

#### Scenario: Sample
- **WHEN** the user switches the preview to Sample
- **THEN** every reading in the preview shows the value from its provider's sample file and the deck is unaffected

#### Scenario: Home Assistant not set up
- **WHEN** a Control widget uses a Home Assistant light and Home Assistant has no address, in Live
- **THEN** the preview shows the light with its sample state while the deck shows that it needs setting up

### Requirement: Selecting A Widget
The user MUST be able to select a widget or a dock slot by clicking it in the preview or with the keyboard. The
selected widget is outlined in the preview with its type and size, and the properties pane shows: its type, its sizes
(for a page widget), and a Remove button. With nothing selected the properties pane shows the page's settings.

- Arrow keys move the selection to the nearest widget in that direction; Delete removes the selected widget; Escape
  clears the selection; each widget has an accessible name with its type, size and row
- A widget whose type is not installed can be selected and removed; its type id is shown

#### Scenario: Click to select
- **WHEN** the user clicks the Weather widget in the preview
- **THEN** it is outlined, labelled "Weather · 4x2", and the properties pane shows Weather with its sizes 4x2 and 4x4

#### Scenario: Keyboard
- **WHEN** a widget is selected and the user presses the down arrow
- **THEN** the widget below it is selected

### Requirement: Adding A Widget
The editor MUST offer every installed widget type in a palette with its name, description, sizes and, for a widget
from a third-party plugin, the plugin's name. Choosing one adds a widget of that type with its default settings at the
default size:

- at the empty cell the user clicked, when it fits there; otherwise at the first free space on the page, scanning rows
  from the top and columns from the left
- when the page has no space for it, nothing is added and the editor says so
- the palette shows a disabled entry for a web page widget, which a later version adds

The new widget is selected.

#### Scenario: Add at a cell
- **WHEN** the grid is on and the user clicks the add button of an empty cell in column 3 of row 9 and chooses Shortcut
- **THEN** a Shortcut at 1x1 is in column 3 of row 9, selected, and on the deck within half a second

#### Scenario: No room
- **WHEN** the page has no free 2x2 space and the user adds Stats
- **THEN** nothing is added and the editor says that the page has no free 2x2 space

### Requirement: Changing A Widget's Size
The properties pane MUST offer exactly the sizes the widget declares. Choosing one resizes the widget in place, keeping
its row and moving it left only as far as needed to stay on the page. A size that does not fit is not applied: the
editor names what is in the way (or says it would run past the bottom of the page) and offers to move the widget to the
first free space of that size, or to keep the current size. No other widget moves.

#### Scenario: Resize fits
- **WHEN** a Stats widget at 2x2 in column 3 is changed to 4x2 and the cells to its left are free
- **THEN** it becomes 4x2 starting in column 1, in the same row

#### Scenario: Resize blocked
- **WHEN** a Stats widget at 4x2 is changed to 4x4 and a Weather widget occupies the rows below
- **THEN** the size is unchanged and the editor says "4x4 does not fit here. Weather is in the way." with a button to
  move it to the first free 4x4 space

### Requirement: Removing A Widget
The user MUST be able to remove the selected widget with Remove or the Delete key. Removal is one undo step.

#### Scenario: Remove and undo
- **WHEN** the user removes a widget and presses Undo
- **THEN** the widget is back with its settings, position and size

### Requirement: Managing Pages
The Pages section MUST show the pages as tabs, in order, with the start page marked, and let the user add a page,
rename it, duplicate it, move it earlier or later, delete it, and make it the start page. Each is one undo step.

- A new page is empty, named "Page N", and inserted after the current page
- Deleting a page that has widgets asks first; the last page cannot be deleted
- Deleting the start page makes the page after it (or before it, for the last page) the start page in the same step,
  and the confirmation says so
- With nothing selected, the properties pane shows the page's name, whether it is the start page, its order, the page
  indicator mode for all pages, how many dock slots are used, and Delete

#### Scenario: Rename
- **WHEN** the user renames "Page 2" to "Games"
- **THEN** the tab and the file say "Games" and the deck, if it shows that page, stays on it

#### Scenario: Delete the start page
- **WHEN** the user deletes "Main", the start page, and confirms
- **THEN** "Main" is gone, the next page is the start page, and one Undo restores both

### Requirement: Undo And Redo In The Editor
The editor MUST offer Undo and Redo for every change made in it and for changes to the file from outside, with the
keyboard shortcuts Ctrl+Z and Ctrl+Y, and say in a tooltip what will be undone. There is no Save button: every change is
saved as it is made.

#### Scenario: Tooltip
- **WHEN** the last change made Stats 4x2
- **THEN** the Undo button's tooltip says "Undo Make Stats 4x2"

### Requirement: Deck Status
The navigation pane MUST show whether the deck is up to date and where it is shown, whether a change is being saved,
whether the deck is waiting for its display, and when a change could not be saved.

#### Scenario: Waiting
- **WHEN** the deck's display is disconnected and the user changes a widget
- **THEN** the status says the deck is waiting for that display and that changes are saved

### Requirement: Configuration File States
The Pages section MUST tell the user, above the preview, when:

- the file was changed outside the editor and the editor took the new content, with a button to undo that
- the file is not valid, with its line and column, while the deck keeps the last good configuration
- the file was written by a newer version of UrDeck, in which case nothing can be changed in the editor

#### Scenario: Changed outside
- **WHEN** the file is changed in a text editor while the editor is open
- **THEN** the editor shows the new content and a bar saying the file changed outside, and "Undo the reload" restores
  the previous content

### Requirement: General Section
The General section MUST show the application's version and let the user turn automatic update checks on and off, turn
starting with Windows on and off, show the configuration file in File Explorer, and open the log.

#### Scenario: Turn off update checks
- **WHEN** the user turns off "Check for updates automatically"
- **THEN** the configuration has `"updates": "off"` and no update check runs at the next start
