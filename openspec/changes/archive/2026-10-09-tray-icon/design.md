## Context

See `proposal.md` for the scope. What the code does today:

- `App.OnLaunched` creates `HostContext` and `MainWindow` and disposes the host on `MainWindow.Closed`. Closing the
  window is already the clean shutdown, so Quit only has to call `MainWindow.Close()`.
- `WindowFocus` already uses raw P/Invoke and `SetWindowSubclass`, and sets `WS_EX_NOACTIVATE`. The window has no
  taskbar button (noted in the `pages-and-pager` design; cause not found), so nothing else offers a way to close it.
- `AppWindow.SetIcon` loads `urdeck.ico`, copied beside the executable by the csproj.
- The Host has no test project; the Engine does, with a coverage floor. This change adds nothing to the Engine.

## Goals / Non-Goals

**Goals:**
- One native tray icon with a Quit entry, no new dependency, in the style of `WindowFocus`.
- A menu that is data (a list), so later entries need no tray changes.
- Survive an Explorer restart; leave nothing in the tray after exit.

**Non-Goals:**
- Dark-mode menu theming, balloons, a status tooltip, left-click behaviour, the config `icon` field.
- A single-instance guard (separate backlog item).

## Decisions

### 1. Raw `Shell_NotifyIcon` and a native popup menu

`Shell_NotifyIconW` with `NIM_ADD`/`NIM_DELETE`, `NIF_MESSAGE | NIF_ICON | NIF_TIP`, and `NOTIFYICON_VERSION_4` so the
callback carries the event in the low word of `lParam`. The menu is `CreatePopupMenu` + `AppendMenu` + `TrackPopupMenuEx`
with `TPM_RETURNCMD`, so the chosen command id is the return value and no `WM_COMMAND` plumbing is needed.

*Alternatives:* H.NotifyIcon (a dependency and its own popup windows for a one-item menu), WinForms `NotifyIcon`
(pulls WinForms into a self-contained app). Both rejected under "native, simple".

### 2. A hidden message-only window owns the icon

A message-only window (`HWND_MESSAGE` parent, a small registered class and `WndProc`) receives the tray callback
message and `TaskbarCreated`. It is separate from the deck window because that window is `WS_EX_NOACTIVATE`, and
`TrackPopupMenu` needs its owner in the foreground (`SetForegroundWindow(owner)` before, a `WM_NULL` post after, or the
menu does not dismiss on an outside click). The owner is invisible and never shown, so the deck window is not
disturbed, and the user's foreground window loses focus only to the menu while it is open. The window and its `WndProc`
delegate are kept in fields for the tray's lifetime so the delegate is not collected.

### 3. The menu is an ordered list

```csharp
internal sealed record TrayMenuEntry(string Label, Action Run);
```

`TrayIcon` takes an `IReadOnlyList<TrayMenuEntry>`. Each entry gets command id `base + index`; `TrackPopupMenuEx`'s
return value is mapped back to the list and `Run` is called. A separator, submenus and check marks are not built:
the first entry that needs one adds it then. `App` builds the list (`Quit` => `window.Close()`), so the tray class
knows nothing about the application.

### 4. Lifetime and ordering

`App.OnLaunched` creates the tray after the window, passing the window's `Close`. `MainWindow.Closed` disposes the tray
first (`NIM_DELETE`, destroy the message window, unregister the class), then the host, as today. The tray is created on
the UI thread, so its `WndProc` and `Quit` run there, which `Window.Close()` needs. With `URDECK_ACTIVATABLE=1` the tray
still exists; Escape keeps working beside it.

### 5. Explorer restart

Register `TaskbarCreated` (`RegisterWindowMessageW`) and, when the `WndProc` sees it, call `NIM_ADD` again (and re-set
the version). A failed `NIM_ADD` is logged as a warning, not thrown: Explorer may not be ready, and the next
`TaskbarCreated` retries.

### 6. Icon size

Load `urdeck.ico` with `LoadImageW(LR_LOADFROMFILE)` at `GetSystemMetrics(SM_CXSMICON)` and `SM_CYSMICON`. The icon is
loaded once at the primary DPI; a later DPI change leaves the Explorer-scaled icon, which is acceptable for a rarely used
icon. If the file is missing, the tray uses `IDI_APPLICATION` and logs a warning, so Quit is still reachable.

### 7. No taskbar change

The window already has no taskbar button, so `WS_EX_TOOLWINDOW` is not added. The task list below verifies this on a
real run and only revisits it if a button appears.

## Risks / Trade-offs

- **Hidden by default on Windows 11** -> new icons start in the overflow flyout. The app cannot pin itself; the README
  tells the user to drag the icon out. Quit still works from the flyout.
- **The menu does not follow our theme** -> the native menu looks light unless the system opts it into dark mode through
  an undocumented uxtheme call; accepted for now.
- **Foreground rules** -> `SetForegroundWindow` on the owner can be refused in odd cases; the menu then still shows but
  may not dismiss on an outside click. Verified manually with another application focused.
- **No automated test of the Win32 layer** -> verified by hand; the entry list is plain data kept apart from Win32 types.

## Open Questions

- None blocking. The single-instance guard and the editor's own entry are later changes.
