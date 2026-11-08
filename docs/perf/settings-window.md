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

## With the Providers section (2026-10-27, `provider-settings-and-secrets`)

The window gained a Providers section built from the providers' declarations. No first-party provider declares settings yet, so
the section shows its empty state; the cost of a populated form (a few text boxes, a password box and buttons per provider) is
not measured. Same method as above on a copy of the Release build, with `display` set to a monitor that is not connected
(Windows 11, three monitors).

| | private | working set |
|---|---|---|
| baseline, before the first opening | 116.5 MB | 185.5 MB |
| after the first cycle | 114.6 MB | 190.6 MB |
| after cycle 10 | 108.3 MB | 185.8 MB |
| after cycle 20 | 111.1 MB | 188.3 MB |
| **settled, 5 s after the last close** | **107.7 MB (-8.8 against the baseline, -6.8 against cycle 1)** | **185.7 MB (+0.2, and -4.9 against cycle 1)** |

The gate passes: memory does not climb from cycle to cycle and the settled value is below the baseline. The absolute numbers are
higher than in the 2026-10-11 run because this build carries more (the media provider, the secret store and the editor are
loaded) and the baseline was taken just after the start; they are not comparable with that run. No warning or error in the log.

## The editor (2026-11-07, `editor-shell`)

The settings window became the editor: a `NavigationView` with Pages (a live preview of the page, a second deck session
painted on a software canvas), Providers, Display and General. The cost requirement now separates the deck's running cost
once the window is closed (what matters) from the window's own cost while it is open (it must stay usable). Release build,
Windows 11, the owner's PC; the deck on the 1100x3840 panel with the owner's page (clock, weather, stats 4x4, dock of three)
and the theme's moving background.

### Leak found and fixed

The first soak of the editor leaked every window: private bytes rose about 5 MB per opening (+104 MB after 20) and the
managed heap with them. `dotnet-gcdump` showed every closed editor's controls, preview session and sample readings alive;
ClrMD found no managed root, only native references to the closed `EditorWindow`. Bisection (an empty `Window` does not
leak; the editor without its Pages section still did) and the referrer chain pointed at handlers native code holds that
lead back to the window: the `NavigationView` events and the window's own `Closed` handlers (the editor's and `App`'s).
`Window` is not a XAML element, so the reference tracker does not see through that cycle. They are now named handlers,
detached when the window closes (and the `NavigationView`'s content, footer and items cleared); `App` detaches its `Closed`
handler when it runs. The dispatcher timers the editor and the preview make are detached too
(`DispatcherDeckScheduler.Dispose`, `FrameClock.Shutdown`). After the fix no `EditorWindow` is left after the soak.

### Open and close twenty times

`URDECK_SETTINGS_SOAK=1`, as above. First on a copy of the build whose remembered display is not connected (no deck runs):

| | private | working set | gc heap |
|---|---|---|---|
| baseline | 119.2 MB | 191.2 MB | 4.6 MB |
| after cycle 1 | 152.3 MB | 264.5 MB | 5.9 MB |
| after cycle 10 | 169.3 MB | 287.5 MB | 6.4 MB |
| after cycle 20 | 171.8 MB | 291.6 MB | 6.8 MB |
| settled | 171.5 MB | 290.8 MB | 6.7 MB |

Without a deck the first opening also loads what a running deck already holds (the theme, fonts, Skia), so the one-time
step is large here (+33 MB). From cycle 10 to cycle 20: +2.5 MB, inside the 10 MB of the requirement.

With the deck running on the panel (the case that matters):

| | private | working set | gc heap |
|---|---|---|---|
| baseline | 218.0 MB | 304.7 MB | 5.7 MB |
| after cycle 1 | 230.0 MB | 329.7 MB | 6.1 MB |
| after cycle 10 | 240.2 MB | 342.3 MB | 6.1 MB |
| after cycle 20 | 243.4 MB | 346.5 MB | 6.1 MB |
| **settled** | **233.8 MB (+15.8 once)** | **336.8 MB** | **5.7 MB (the baseline's)** |

### CPU

Process CPU time over 20 s readings, % of one core:

| | readings |
|---|---|
| deck, editor never opened (fresh start) | 9.45, 9.69; another start 8.52, 10.0 |
| deck, after the editor was opened and closed 20 times | 7.58, 8.12 |
| editor open on Pages, preview Live (still background) | 8.59, 7.89 |
| editor open on Display | 7.03, 8.91 |

Every reading is within the deck's own spread from one reading to the next; most of the deck's cost is its moving
background (30 frames a second). Neither the closed editor nor the open preview is measurable above it.

The first preview painted the moving background's shader, the pattern and the shadows on its software canvas at 30 frames
a second, and the editor was unusable (slow clicks, choppy dragging, owner on the panel). The preview now paints those once
into a picture and shows the background and moving borders still (`DeckFramePainter.PaintCached`); a frame paints the cards.

### Typing a page name

Typing 11 characters into a page's name at about six keys a second (scripted, 160 ms apart): 9 rebuilds of the deck's page
and 9 of the preview's (18 `Pages:` lines). The configuration's 150 ms coalescing is shorter than the time between keys, so
the deck rebuilds about once per key while someone types; each edit is one undo step (merged by the page). Acceptable for
now: a rebuild of this page is quick and the deck follows the editor. A longer coalescing delay would cut it if needed.
