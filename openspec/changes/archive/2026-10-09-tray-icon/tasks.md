## 1. Tray icon

- [x] 1.1 `TrayMenuEntry` record and `TrayIcon` class in `src/UrDeck.Host`: message-only window, registered class, kept `WndProc` delegate, `Dispose` that deletes the icon and destroys the window (SPDX header, P/Invoke style as in `WindowFocus.cs`)
- [x] 1.2 `Shell_NotifyIcon` add/delete with `NOTIFYICON_VERSION_4`, tooltip "UrDeck", icon from `urdeck.ico` at `SM_CXSMICON` with the `IDI_APPLICATION` fallback and a warning
- [x] 1.3 Right-click opens the popup menu built from the entry list (`TrackPopupMenuEx` with `TPM_RETURNCMD`, `SetForegroundWindow` on the owner before and `WM_NULL` after) and runs the chosen entry; left click and double click do nothing
- [x] 1.4 Re-add the icon on `TaskbarCreated`; a failed add logs a warning and does not throw

## 2. Wiring

- [x] 2.1 `App.OnLaunched` creates the tray with one entry, `Quit`, running `MainWindow.Close()`; `MainWindow.Closed` disposes the tray before the host
- [x] 2.2 Log "Tray icon added" and the menu's entry count at startup, and the warnings from tasks 1.2 and 1.4, in `urdeck.log`

## 3. Verification (manual, on the panel machine)

- [x] 3.1 Icon and tooltip appear on start; right-click shows Quit; Quit closes the window and the process, and the icon is gone
- [x] 3.2 With another application focused, open and dismiss the menu by clicking elsewhere and by Escape: the menu closes, the host keeps running, and that application keeps focus afterwards
- [x] 3.3 Restart Explorer (`taskkill /f /im explorer.exe`, then start it): the icon returns
- [x] 3.4 Confirm the deck window still has no taskbar button or Alt-Tab entry; if one appears, set `WS_EX_TOOLWINDOW` and update the design
- [x] 3.5 `URDECK_ACTIVATABLE=1`: Escape still closes the window, and the tray's Quit works

## 4. Docs

- [x] 4.1 `README.md` and `AGENTS.md`: close the app from the tray icon, and that Windows 11 may hide it in the overflow flyout until pinned; update the `WindowFocus` log line that says "end the process to close it"
- [x] 4.2 `docs/BACKLOG.md` item 12: tick the tray icon (Quit only) and note that the single-instance guard and the editor entry remain; `docs/ROADMAP.md` line about the tray (quit, reload, open the log) matches what shipped
- [x] 4.3 `openspec validate --all --strict`, `dotnet build urdeck.slnx -c Release` (0 warnings) and `dotnet format urdeck.slnx --severity warn`; archive the change when everything above is verified
