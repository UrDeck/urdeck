Before starting: `editor-shell` must be applied. `editor-forms` is not needed. Read `proposal.md`, `design.md` (D1–D6),
the spec here and `docs/handoff/editor-design/Arrange.dc.html`. Model: Sonnet (ask for an Opus review if pointer
capture and auto-scroll in the `ScrollViewer` fight each other).

## 1. Engine

- [x] 1.1 `PageEditing.DropTarget` (Free, Swap, Blocked with the blocker or past the bottom), `Swap`, `ResizeStops` (each declared size: fits or blocker), `MoveToDock`, `MoveInDock`, `MoveToPage`; all as document transactions with the labels in the design
- [x] 1.2 Tests on Linux: free move, swap only for equal sizes, blocked, past the bottom, resize stops at the right edge, dock full, a non-1x1 type refused by the dock, a widget returned from the dock keeps its earlier size when it fits, one undo per operation

## 2. Host

- [x] 2.1 `ArrangeOverlay` (D1): drag threshold, lifted card image, target outline (green/orange with text), release, cancel on Escape and on lost capture, auto-scroll
- [x] 2.2 Resize handle and stops (D3)
- [x] 2.3 Dock drop zone, reordering along the dock, dragging off onto the page (D4); properties pane entries; empty slot "Add…" with the palette filtered to 1x1 types
- [x] 2.4 Page list (D5) with thumbnails drawn from the JSON, reorder, inline rename, the ⋯ menu
- [x] 2.5 Keyboard moves (D6) with automation notifications
- [x] 2.6 Move a page widget to another page: `PageEditing.MoveWidgetToPage` (keeps size, first free space, one undo step; tests) and the pane's "Move to page" menu. Dragging across pages by holding at the edge is a follow-up change
- [x] 2.7 `AddWidget` falls back to the largest declared size that fits when the default size has no room (found in the manual check)

## 3. Verify

- [x] 3.1 `dotnet build urdeck.slnx -c Release` (0 warnings), `dotnet test`, coverage floor, `dotnet format --verify-no-changes`, Linux tests
- [x] 3.2 Owner, manual: drag moves, swaps and blocked drops on the sample page; resize via handle at the right edge; auto-scroll on the tall page; dock on/along/off by mouse and by menu; page list reorder and rename; Ctrl+arrows; undo after each; the panel follows every change
- [x] 3.3 `openspec validate editor-arrange --strict`
