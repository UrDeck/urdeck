## Purpose

Arranging a page in the editor by direct manipulation: moving and resizing widgets by dragging, moving widgets on and
off the dock, and reordering pages, always with a keyboard or menu equivalent.

## ADDED Requirements

### Requirement: Moving A Widget By Dragging
The user MUST be able to drag a widget in the preview to another place on the page. While dragging, the editor shows the
cells the widget would take, snapped to the grid, and whether it fits:

- a free target is shown as fitting; releasing there moves the widget
- over a space taken by a widget of the same size, the editor says releasing will swap them; releasing swaps the two
- over a space taken by a widget of another size, or past the bottom of the page, the editor names what is in the way;
  releasing there leaves everything where it was
- no other widget ever moves because of a drag, except the one swapped with
- the preview scrolls when the widget is dragged near its top or bottom edge
- Escape cancels the drag; each completed drag is one undo step

#### Scenario: Move to a free space
- **WHEN** the user drags a Stats widget at 2x2 to two free columns three rows lower and releases
- **THEN** the widget is there, on the deck too, and one Undo puts it back

#### Scenario: Swap
- **WHEN** the user drags a 2x2 Stats widget onto another 2x2 Stats widget and releases
- **THEN** the two widgets have exchanged places

#### Scenario: Blocked
- **WHEN** the user drags a 2x2 widget over a 4x2 Weather widget
- **THEN** the editor shows "Weather is in the way", and releasing leaves both widgets where they were

### Requirement: Resizing A Widget By Dragging
The selected widget MUST have a handle that, when dragged, shows the widget's declared sizes as stops, marks the one it
will snap to, and marks the stops that do not fit with what is in the way. Releasing applies the marked size if it
fits; otherwise nothing changes.

#### Scenario: Snap to a declared size
- **WHEN** the user drags the handle of a Stats widget at 2x2 towards four columns and two rows
- **THEN** the 4x2 stop is marked and releasing makes the widget 4x2

#### Scenario: A stop that does not fit
- **WHEN** the 4x4 stop would cover a Weather widget
- **THEN** the 4x4 stop is shown as not fitting with "Weather is in the way"

### Requirement: Arranging The Dock
The user MUST be able to drag a widget that supports the size 1x1 onto the dock when the dock has a free slot, drag
dock slots along the dock to reorder them, and drag a dock slot onto the page to move it back. The same is possible
from the properties pane: Move to the dock, Move to the page, Move earlier and Move later. An empty dock slot offers to
add a widget that supports 1x1.

#### Scenario: Shortcut to the dock
- **WHEN** the user drags a 1x1 Shortcut from the page onto the dock's third slot
- **THEN** the Shortcut is the dock's third slot on every page and no longer on the page

#### Scenario: Dock full
- **WHEN** the dock has four slots and the user selects a page widget that supports 1x1
- **THEN** Move to the dock is disabled and dragging onto the dock shows no place to drop

### Requirement: Moving A Widget To Another Page
The user MUST be able to move a page widget to another page from the properties pane (Move to page, listing the other
pages). The widget keeps its size and settings and takes the first free space of that size on the target page; the editor
then shows that page with the widget selected. When no space fits, nothing changes and the pane says so.

#### Scenario: Weather to another page
- **WHEN** the user chooses Move to page, Other, for a 4x2 Weather widget and Other has room
- **THEN** Weather is on Other at its first free 4x2 space, no longer on the first page, and one Undo reverses it

#### Scenario: No room
- **WHEN** the target page has no free space of the widget's size
- **THEN** nothing changes and the pane says there is no free space on that page

### Requirement: Adding Into A Tight Page
When the editor adds a widget and its default size has no free space on the page, it MUST use the largest other size the
widget declares that fits, and refuse only when none does.

#### Scenario: A 4x1 row is left
- **WHEN** the user adds a Clock whose default is 4x2 to a page with one free 4x1 row
- **THEN** the Clock is added at 4x1 in that row

### Requirement: Page List
The editor MUST offer the pages as a list with a small picture of each page's layout, where pages can be reordered by
dragging and renamed in place, as an alternative to the tabs.

#### Scenario: Reorder in the list
- **WHEN** the user drags "Performance" above "Main" in the page list
- **THEN** the pages are in the new order on the deck and in the tabs, and the start page is unchanged

### Requirement: Keyboard Moves
With a widget selected, Ctrl and an arrow key MUST move it one cell in that direction when it fits there, and otherwise
say what is in the way. In the dock, Ctrl with Left or Right moves the slot earlier or later.

#### Scenario: Nudge
- **WHEN** a 1x1 widget is selected and the cell to its right is free and the user presses Ctrl+Right
- **THEN** the widget moves one column to the right
