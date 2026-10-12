# Settings window: cost

The gate of the `settings-window` change ("Cost Of The Settings Window"): opening and closing the window must not raise
the host's idle cost. After 20 open and close cycles and a garbage collection, private bytes are within 10 MB of the
start and do not keep rising.

## Method

`URDECK_SETTINGS_SOAK=1` (see `SettingsSoak`): five seconds after start the host takes a baseline, then opens the
settings window, closes it 0.6 s later, waits 0.6 s, forces two full garbage collections and logs the process's private
bytes and working set. It does this 20 times and then logs one more measurement five seconds after the last close
("settled"). The lines are `Soak ...` in `urdeck.log`.

Run from a copy of the Release build output (not the daily copy), with `display` in its config set to a monitor that is
not connected, so the deck is hidden and no page, widget or provider runs: what is measured is the window on top of the
bare host (WinUI loaded, tray icon, no deck). The window opened on the display under the pointer each time.
Windows 11, one NVIDIA GPU, three monitors (the owner's PC), 2026-10-11.

## Result

| | private | working set |
|---|---|---|
| baseline, before the first opening | 95.4 MB | 123.5 MB |
| after the first cycle | 95.5 MB | 131.9 MB |
| after cycle 10 | 99.3 MB | 138.2 MB |
| after cycle 16 | 99.1 MB | 138.0 MB |
| after cycles 17 to 19 | 106.6 to 107.6 MB | 145.8 to 146.0 MB |
| after cycle 20 | 100.8 MB | 139.3 MB |
| **settled, 5 s after the last close** | **97.3 MB (+1.9 against the baseline, +1.8 against cycle 1)** | **136.2 MB (+12.7, and +4.3 against cycle 1)** |

An earlier run of the same measurement (before the soak stopped itself at cycle 20 and measured a settled value)
ended at +3 MB private and +14 MB working set against its baseline, between 97 and 108 MB along the way.

- **The gate passes on the settled value:** +1.9 MB private, far inside 10 MB. Memory does not climb from cycle to
  cycle: it drifts up by about 0.2 MB a cycle through the first sixteen and the settled value is back within 2 MB of
  the first cycle.
- **The first opening costs a one-time step of about 8 MB of working set** (WinUI creates the controls, text and font
  caches once); private bytes do not move at that point.
- **During the run the numbers wander by several MB.** In both runs private bytes jumped to about 107 MB around cycles
  17 to 19 (+11 or +12 MB over the baseline) and were back at 100 MB by cycle 20 and 97 MB after settling. That is
  outside the 10 MB band while it lasts. It is transient (it did not survive the next collection and the five seconds),
  and it was the same cycles in both runs, so something is scheduled at about that time; it was not looked into. The gate is
  written for the settled value for that reason.
- The log has no warning or error line from either run.

## Idle CPU and GPU, with the full deck running

The real page (clock, stats 4x4, weather 4x2, one shortcut in the dock) on the panel, Release build, 20 s per reading
(process CPU time, and the GPU Engine counters of the process summed over all engines):

| settings window | CPU, % of one core | GPU, average / peak |
|---|---|---|
| closed (first run) | 3.83 | 1.07 / 1.62 |
| closed (second run, same state) | 1.02 | 1.06 / 1.30 |
| open on the monitor page (`URDECK_SETTINGS_SOAK=hold`, a restarted host) | 2.03 | 1.03 / 1.29 |

The two closed readings differ from each other by more than either differs from the open one, so the window's own
cost is below what this method can see: the CPU figure of the running deck moves by a few percent from one 20 s
reading to the next (probably the animated weather icon and the readings that arrive every 2 s; not checked). The window has no timer while nothing
changes (the refresh timer runs once, 400 ms after a display change, and the Identify timer only while markers show),
which is the reason to expect no cost, and the readings do not contradict it. The open reading was taken on a host
restarted for it, so private bytes (131.5 MB open, 146 MB and 146 MB closed) are not comparable between the rows.
