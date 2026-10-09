## Why

The deck window never takes focus and has no taskbar button, so the only way to close UrDeck is to end the process.
The same gap will matter more once the config editor exists: the user needs one always-available handle on a running
app, and a notification-area icon is the native place for it. Backlog item 12 already plans it.

## What Changes

- A notification-area (tray) icon, shown while the host runs, with the tooltip "UrDeck" and the application icon.
- Right-clicking the icon opens a native popup menu. Version 1 has one entry, **Quit**, which closes the host cleanly.
  Open config folder, open log and reload were considered and dropped: the user sets the config up rarely, and the
  editor will own log access.
- The menu is an ordered list of entries (label and action), so a later entry such as "Open editor" or a monitor
  picker is one more item in the list and no change to the tray code.
- Left-click and double-click do nothing yet; they are reserved for opening the config editor.
- The icon is re-added when Explorer restarts, so it does not vanish after a shell crash.
- `host-shell`: the "closed by ending the process" wording is replaced by "closed from the tray icon". The
  `URDECK_ACTIVATABLE=1` override and Escape in that mode are unchanged.
- Out of scope: a single-instance guard (its own backlog item), dark-mode menu theming, notifications and balloons,
  and the config `icon` field.

## Capabilities

### New Capabilities
- `tray-icon`: the notification-area icon, its menu model and the Quit entry, and its behaviour across Explorer restarts.

### Modified Capabilities
- `host-shell`: the host window requirement says the window is closed from the tray icon instead of by ending the process.

## Impact

- `src/UrDeck.Host` only: a new tray class and menu model, `App.xaml.cs`/`MainWindow` wiring, and the existing
  `urdeck.ico`. No new package, no change to the Engine or the SDK, so the license boundary is untouched.
- Windows 11 puts a new tray icon in the overflow flyout until the user pins it; the app cannot change that, so Quit
  is one click further away at first. The README says so.
- Manual verification only for the Win32 parts (the Host has no test project); the menu model is plain data and is
  kept free of Win32 types.
