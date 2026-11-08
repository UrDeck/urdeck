## Why

`editor-shell` lets a user add, size and remove widgets and manage pages through buttons and menus. Building a page
quickly wants direct manipulation: drag a widget to another cell, drag its corner to another size, drag widgets on and
off the dock, reorder pages (`docs/handoff/2026-11-06-editor-design.md`, artboard 3;
`docs/handoff/editor-design/Arrange.dc.html`). The rules stay the shell's: a widget never moves because another one
moved, nothing is placed where it does not fit, and the user is told what is in the way.

**Order:** after `editor-shell`. Independent of `editor-forms` and the other `editor-*` changes (it can be applied
before or in parallel with them).
**Model:** Sonnet. Pointer handling over the preview canvas with auto-scroll is the fiddly part; the engine side
(targets, swaps, snapping) is pure and tested.

## What Changes

- **Move by dragging**: the card lifts and follows the pointer; a green outline shows the cells it will take, snapped to
  the grid. Over a taken space the outline turns orange and names what is in the way; releasing there swaps the two
  widgets when they have the same size, otherwise the widget goes back. Dragging near the top or bottom edge scrolls.
- **Resize by dragging** a corner handle of the selected widget: the widget's declared sizes are drawn as dashed stops;
  it snaps to the nearest size that fits; a stop that does not fit is struck through with what is in the way.
- **The dock**: drag a widget that has a 1x1 size onto the dock (when it has a free slot), along it to reorder, and off
  it onto the page (to a free cell) to move it back. Each has a menu equivalent in the properties pane ("Move to the
  dock", "Move to the page", "Move earlier", "Move later"), so nothing needs a mouse. Empty dock slots offer "Add…".
- **Pages as a list**: the page tabs can open as a list with small thumbnails of each page's layout, dragged to
  reorder, double-clicked to rename, with the same ⋯ menu as the tabs.
- **Keyboard moves**: with a widget selected, Ctrl+arrow keys move it one cell when the target is free.
- Every drag is one undo step; Escape during a drag cancels it.

## Capabilities

### New Capabilities

- `editor-arrange`: moving and resizing widgets by dragging, swapping, the dock by dragging and by menu, the page list,
  keyboard moves.

### Modified Capabilities

None. The dock's and grid's rules (`dock`, `grid-layout`) are unchanged; this change only offers new ways to edit them.

## Impact

- `src/UrDeck.Engine/Editor/PageEditing.cs`: `DropTarget` (cells, fits, blocker, swap candidate), `Swap`, `ResizeStops`,
  dock operations (`MoveToDock`, `MoveToPage`, `MoveInDock`); tests.
- `src/UrDeck.Host/Editor/`: drag layer over the preview (`ArrangeOverlay`), resize handles, dock drop zone, `PageList`
  with thumbnails; properties pane menu entries.
