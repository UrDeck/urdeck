# display-recovery checks

## Display identity from the display configuration API (task 1.1, 2026-10-11)

`QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS)` with `DisplayConfigGetDeviceInfo` (source name and target name) on the
owner's PC, next to `EnumDisplayMonitors` and `GetDpiForMonitor(MDT_EFFECTIVE_DPI)`:

| GDI device name | Bounds | Scaling | Friendly name | EDID ids (raw) | Model | Device path |
|---|---|---|---|---|---|---|
| `\\.\DISPLAY1` | 1100x3840 at 3840,-3573 | 100% | `HYTE Y70Touch` | `0x844A` / `0x1100` | `RTD1100` | `\\?\DISPLAY#RTD1100#5&abf4c77&0&UID4353#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}` |
| `\\.\DISPLAY2` (primary) | 3840x2160 at 0,0 | 150% | `LG ULTRAGEAR` | `0x6D1E` / `0x5CE2` | `GSM5CE2` | `\\?\DISPLAY#GSM5CE2#5&abf4c77&0&UID4356#{...}` |
| `\\.\DISPLAY3` | 1080x1920 at -1080,-12 | 100% | `HP Z23n G2` | `0x0E22` / `0x347B` | `HPN347B` | `\\?\DISPLAY#HPN347B#5&abf4c77&0&UID4355#{...}` |

- All three calls return 0, and every target has the flags `friendlyNameFromEdid` and `edidIdsValid` (`0x5`).
- The name, the model and the path are the ones in the change's `design.md`. Decision 3 stands as written.
- The manufacturer id is the EDID's two bytes read as a little-endian number, so the bytes are swapped before the three
  five-bit letters are taken (`0x844A` -> `0x4A84` -> `RTD`). The product code is used as it comes, as four hex digits.
  The result equals the model segment of the device path for all three displays.
- Each source name equals a device name from `EnumDisplayMonitors`: the join is one to one.

## Host checks without touching a cable (2026-10-11)

Release build, the working configuration next to the executable (`monitorName: "tallest"`, no `display`), read from
`urdeck.log`:

- **First start:** `Display chosen with 'tallest': HYTE Y70Touch (RTD1100) ...`, the window is shown on the panel,
  placement is verified 270 ms later without moving the window, and the page is built once. The file gains `display`
  (`HYTE Y70Touch`, `RTD1100`, the path above, selector `tallest`) and the host does not reload because of that save.
  One screen capture: the page is drawn on the panel (the window is shown with `AppWindow.Show(false)` and never
  activated).
- **Second start:** `Display matched by device path`; nothing is written.
- **Absent, simulated** by editing `display` to a model and path that are not connected while the host runs: the deck is
  hidden 12 ms after the reload, `Provider 'weather' stopped` and `Provider 'system' stopped` follow after the 5 s linger
  period, and the frame clock stays stopped. Restoring the entry shows the deck again, with one page build.
- **Started absent** (same edit, `URDECK_ACTIVATABLE=1`): `Placement: Starting -> Absent`, the process has no visible
  window, the tray icon is there. Restoring the entry shows the deck; it has the foreground and Escape closes it.
- **`--snapshot out.png --size 1100x3840`** and `--snapshot out.png`: exit code 0, 1100x3840, and
  `urdeck-config.json` has the same hash before and after, with and without a `display` entry in it.

The tooltip text while absent was not looked at here (it needs a pointer on the tray icon); it is part of 6.3.

## On the panel (section 6, owner), 2026-10-11

- **6.1 first start:** with `display` removed the host chose the Y70 from `tallest` and wrote `HYTE Y70Touch` /
  `RTD1100` / selector `tallest`; the next start logged `Display matched by device path`.
- **6.2 sleep and resume, five times** (Start menu, Sleep, wake by key, sign in): each time one `power mode Suspend`,
  one `Resume`, `Placed -> Settling -> Placed` with `window not moved; rebuilding the page`, one page build, no
  placement on another monitor, no `no longer holds` line. Found on the way: `SystemEvents` subscribed from the UI
  thread delivered nothing, not even Suspend (a standalone probe on the same machine received both); subscribing from
  the thread pool fixes it (see `AGENTS.md`). The first three sleeps ran before the fix and left no trace in the log.
- **6.3 unplug and replug:** the deck is hidden 170 ms after the display-change event, with the connected displays in the
  log; the providers stop 5 s later (the linger period) and start again with the deck. The tray tooltip read "UrDeck:
  waiting for HYTE Y70Touch" while hidden and "UrDeck" again afterwards. On return: `Display matched by device path`,
  one verified placement without moving the window, one page build, the weather provider fetched again. The log shows
  two short cycles (the cable made contact twice, 17:22:31 for 5 s and 17:23:07 for 49 s); both behaved the same.
- **Unexplained, at 17:15:34, no cable or setting touched:** `display settings changed` reported the Y70 at `0,0` for
  about a second (the window sat at `3,3` at 150%), then at `3840,-3573` again. Placement took two attempts to follow
  the first report and one to come back, and verified at 100% about 3 s after the first event without a page rebuild.
  Windows re-enumerated the displays by itself; it is the case the retries are for.

- **6.4 screens off and on without the PC sleeping** (the Y70 has no power control: it is powered from an internal USB
  port; the Windows screen timeout was set to 1 minute instead, the screens went dark for about 10 s and were woken):
  Windows did **not** report it as a disconnect or as a display change. The host log has no `display settings
  changed`, no `Deck hidden`, no placement line and no rebuild for the whole period; the deck looked right afterwards.
  So a display that is merely off is not "absent" for the host, and nothing has to be done for it (backlog item 15 may
  still want to stop drawing for a display nobody can see).

- **6.5 started with the Y70 unplugged:** the log lists only the HP and the LG, then `Deck hidden: waiting for 'HYTE
  Y70Touch'` and `Placement: Starting -> Absent` 400 ms after startup; the process has no window, nothing is shown on
  either remaining monitor, and the tray icon is there with the tooltip "UrDeck: waiting for HYTE Y70Touch". Plugged
  in 70 s later: `display settings changed`, `Display matched by device path`, the deck is shown and placement is
  verified within 0.6 s without moving the window, one page build, both providers started with it.

- **6.6 another output:** not tried. The graphics card has no spare output for the Y70's cable. The path re-pin is
  covered by `DisplaySelectorTests.MovedToAnotherOutput_IsFoundByModel_AndThePathIsRePinned` and
  `PlacementControllerTests.Unplugged_TheDeckHidesAtOnce_AndReturnsOnAnotherOutput`, not by hardware.
- **6.7 scaling changes while running** (Settings, a few times each): changing the primary's scaling produced a
  `display settings changed`, a placement verified at the Y70's 100% with the window not moved, and `page kept`: no
  rebuild, no visible change. Changing the Y70's own scaling (125%, 150%, 100%) was verified on the first attempt each
  time, with one page build each, the content area 1100x3840 physical throughout, and a correct look.

- **6.8 idle:** CPU 0.02% of the machine over 30 s, and no placement, `Windows:` or `Deck` lines in the log while
  nothing changed (after the two confirm checks the controller has no timer pending). GPU in Task Manager: 0.2 to 0.7%,
  mostly 0.2 to 0.3%, against 0% in `render-host-baseline.md`. That baseline ran one Clock; this configuration also has
  the stats widget, whose animation starts the frame clock for about 0.3 s after each 2 s data refresh (the log shows
  it), plus the weather widget and the dock. The difference is the widget set, not this change, which does nothing at rest.
(CPU at rest was 0.02% over 30 s, with no placement lines in the log).