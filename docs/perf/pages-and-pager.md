# pages-and-pager measurements

## Focus (task 1.3)

Panel: Y70, 1100x3840. `WS_EX_NOACTIVATE` plus `WM_MOUSEACTIVATE` -> `MA_NOACTIVATE` on the top-level window only.

- Tapping the panel with another window focused: that window keeps focus (owner, 2026-10-09).
- Fullscreen application: not yet tried.
- The XAML child window did not need covering (task 1.4 not needed so far).
- The window has no taskbar button on the owner's machine (extended style `0x08000100`, no `WS_EX_TOOLWINDOW`); not caused
  by this change and not needed.
