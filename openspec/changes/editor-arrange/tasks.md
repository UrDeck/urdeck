Before starting: `editor-shell` must be applied. `editor-forms` is not needed. Read `proposal.md`, `design.md` (D1–D6),
the spec here and `docs/handoff/editor-design/Arrange.dc.html`. Model: Sonnet (ask for an Opus review if pointer
capture and auto-scroll in the `ScrollViewer` fight each other).

## 1. Engine

- [ ] 1.1 `PageEditing.DropTarget` (Free, Swap, Blocked with the blocker or past the bottom), `Swap`, `ResizeStops` (each declared size: fits or blocker), `MoveToDock`, `MoveInDock`, `MoveToPage`; all as document transactions with the labels in the design
- [ ] 1.2 Tests on Linux: free move, swap only for equal sizes, blocked, past the bottom, resize stops at the right edge, dock full, a non-1x1 type refused by the dock, a widget returned from the dock keeps its earlier size when it fits, one undo per operation

## 2. Host

- [ ] 2.1 `ArrangeOverlay` (D1): drag threshold, lifted card image, target outline (green/orange with text), release, cancel on Escape and on lost capture, auto-scroll
- [ ] 2.2 Resize handle and stops (D3)
- [ ] 2.3 Dock drop zone, reordering along the dock, dragging off onto the page (D4); properties pane entries; empty slot "Add…" with the palette filtered to 1x1 types
- [ ] 2.4 Page list (D5) with thumbnails drawn from the JSON, reorder, inline rename, the ⋯ menu
- [ ] 2.5 Keyboard moves (D6) with automation notifications

## 3. Verify

- [ ] 3.1 `dotnet build urdeck.slnx -c Release` (0 warnings), `dotnet test`, coverage floor, `dotnet format --verify-no-changes`, Linux tests
- [ ] 3.2 Owner, manual: drag moves, swaps and blocked drops on the sample page; resize via handle at the right edge; auto-scroll on the tall page; dock on/along/off by mouse and by menu; page list reorder and rename; Ctrl+arrows; undo after each; the panel follows every change
- [ ] 3.3 `openspec validate editor-arrange --strict`
