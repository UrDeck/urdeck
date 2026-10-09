# pages-and-pager measurements

## Focus (task 1.3)

Panel: Y70, 1100x3840. `WS_EX_NOACTIVATE` plus `WM_MOUSEACTIVATE` -> `MA_NOACTIVATE` on the top-level window only.

- Tap, drag and long-press on the panel with a non-fullscreen application focused: it keeps focus and stays in front
  (owner, 2026-10-09).
- The same with a fullscreen game (ARC Raiders): no focus was stolen.
- The XAML child window did not need covering (task 1.4 not needed).
- The window has no taskbar button on the owner's machine (extended style `0x08000100`, no `WS_EX_TOOLWINDOW`); not caused
  by this change and not needed.
