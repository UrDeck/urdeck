# pages-and-pager measurements

## Focus (task 1.3)

Panel: Y70, 1100x3840. `WS_EX_NOACTIVATE` plus `WM_MOUSEACTIVATE` -> `MA_NOACTIVATE` on the top-level window only.

- Tap, drag and long-press on the panel with a non-fullscreen application focused: it keeps focus and stays in front
  (owner, 2026-10-09).
- The same with a fullscreen game (ARC Raiders): no focus was stolen.
- The XAML child window did not need covering (task 1.4 not needed).
- The window has no taskbar button on the owner's machine (extended style `0x08000100`, no `WS_EX_TOOLWINDOW`); not caused
  by this change and not needed.

## Slide spike (tasks 2.1 to 2.5)

- Mechanism: the pointer's dx sets each page host's composition `Offset`; the settle is a 250 ms composition animation
  (ease-out). No `InteractionTracker`.
- By touch on the panel (owner, 2026-10-09): the swipe feels right, the bounce at both ends behaves, dot taps work, and
  values on the incoming page persist (last value shown, no dashes seen).
- The constants kept: slop 0.04 of a cell, commit at half a page or 0.6 pages per second.
