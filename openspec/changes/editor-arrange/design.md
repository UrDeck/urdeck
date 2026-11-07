## Context

- `editor-shell`: the preview (`PreviewPresenter`: one `SKXamlCanvas` in a `ScrollViewer`, XAML overlays positioned
  from the session's layout rectangles), `PageEditing` (`Fits`, `Blocker`, `FirstFree`, `Resize`, `MoveTo`, page
  operations) as document transactions, selection by `WidgetRef`.
- Dock rules (`dock` spec): up to four slots, order only, any widget type that supports 1x1, the same on every page;
  `dock` in the configuration is a list of widget objects (their `col`/`row`/`width`/`height` are not used).
- Design: `Arrange.dc.html` (A pages list, B move, C resize, D dock).

## Goals / Non-Goals

**Goals:** direct manipulation that never surprises (no widget moves on its own); every drag has a keyboard or menu
equivalent; one undo step per gesture.

**Non-Goals:** touch editing on the panel (decided: all editing happens on the desktop); multi-select; moving a widget
to another page by dragging (use cut/paste later, or the menu "Move to page…" — not in this change).

## Decisions

### D1. Drag layer

- An `ArrangeOverlay` (XAML `Canvas`) over the preview canvas, same size, receiving pointer events. A press on a card
  selects it (as today); moving more than 4 DIP with the button down starts a drag (pointer captured).
- While dragging: a lifted copy of the card (a XAML `Image` of the card's pixels taken from the preview's last frame,
  slightly scaled and with a shadow) follows the pointer; the source cell shows a dashed outline.
- Target: the cell under the card's top-left quarter-cell offset (grab offset kept), clamped to the grid;
  `PageEditing.DropTarget(page, widget, col, row)` returns `Free` (green outline), `Swap(other)` (orange outline,
  "Release to swap with <name>"), or `Blocked(other or "past the bottom")` (orange, "<name> is in the way").
- Release: Free → `MoveTo` "Move <name>"; Swap → `Swap` "Swap <a> and <b>" (both positions exchanged, one transaction);
  Blocked → the card animates back, nothing written. Escape cancels.
- Auto-scroll: within 48 DIP of the stage's top or bottom edge, scroll by up to 12 DIP per frame proportional to depth.
- The preview session keeps painting the page underneath unchanged during the drag (the widget's slot is not moved until
  release), so there is no relayout per pointer move.

### D2. Swap rule

Two widgets swap only when they have the same width and height; their `col`/`row` are exchanged. A widget dropped onto
itself (same cell) is no change.

### D3. Resize handle

- The selected widget shows a handle at its bottom-right corner (12 DIP). Dragging it shows each declared size as a
  dashed rectangle anchored at the widget's top-left (`ResizeStops`): label "4x2", a fitting stop normal, the stop the
  pointer is nearest (by distance from the pointer to the stop's bottom-right corner) in accent with "· snaps here", a
  stop that does not fit struck through (diagonal hatch) with "<name> is in the way".
- Release on a fitting stop → `Resize`; on a blocked one → nothing. Sizes wider than the space to the right of the
  widget's column are anchored left as `Resize` does (the shell's rule: keep the row, move left as needed).

### D4. Dock

- The dock area in the preview is a drop zone while a widget that supports 1x1 is dragged and the dock has a free slot
  (or the dragged widget is already in the dock): an insertion marker between slots.
- Drop on the dock: `MoveToDock(widget, index)` "Move <name> to the dock" (removes it from the page, inserts into
  `dock` at index; its `col`/`row`/`width`/`height` are kept in the object as they are but unused). A widget at another
  size than 1x1 on the page is accepted (the dock draws it at 1x1); returning to the page restores its size if it fits.
- Drag along the dock: `MoveInDock(from, to)`.
- Drag a dock slot onto the page: `MoveToPage(slot, cell)` at the drop cell if a 1x1 fits there (or its kept size),
  else blocked.
- Properties pane (dock slot or page widget): "Move to the dock" (first free slot; disabled when the dock is full or the
  type has no 1x1), "Move to the page" (first free cell), "Move earlier"/"Move later" (in the dock).
- Empty dock slot: click → the palette filtered to types with 1x1, adding straight into that slot.

### D5. Page list

A toggle next to the tabs switches them to a vertical list (in the left of the stage): each row has a grip, a thumbnail
(the page's widgets as rounded blocks on a 4-column grid, drawn from the JSON, not rendered), the name, "N widgets",
and the ⋯ menu. `ListView` with `CanReorderItems` → `MovePage`; double-click → inline `TextBox` rename. The tabs and the
list show the same pages; the choice is remembered for the session.

### D6. Keyboard moves

Ctrl+arrow moves the selected page widget one cell if `Fits`, else does nothing and announces "<name> is in the way"
(automation notification). In the dock, Ctrl+Left/Right = Move earlier/later.

## Risks / Trade-offs

- [Pointer capture lost when the pointer leaves the window] → cancel the drag on `PointerCaptureLost`.
- [Drag image looks wrong on a moving background] → the lifted image is the card only (cropped from the frame), over
  the unchanged page.
