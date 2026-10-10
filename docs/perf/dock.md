# dock measurements

## Layout on the panel (2026-10-09)

1100x3840, built-in themes: a cell is 275 pixels and 13 rows end at 3575, which leaves 265.

| Band | Theme value | Pixels | From | To |
|---|---|---|---|---|
| Grid | | 3576 | 0 | 3576 |
| Page indicator | `indicator.bandHeight` 0.25 | 69 | 3576 | 3645 |
| Dock | `dock.height` 0.71 | 195 | 3645 | 3840 |

- The two bands use 264 of the 265 spare pixels, so the grid keeps its 13 rows. A dock height of 0.72 is 198 pixels and
  costs the thirteenth row.
- A slot is 195 pixels square and its card 183 (the slot inset by half the theme's gap of 0.06, 11.7 pixels). Four
  slots are 780 pixels wide, centred with 160 on either side.
- The dock height is the value the change proposed. The owner approved it on the panel (task 7.1, below): **0.71**
  stays the value of the three built-in themes.

## Snapshot (task 5.2, 2026-10-09)

`UrDeck.Host.exe --snapshot --size 1100x3840`, `default-dark`, two pages of shortcuts with widgets in rows 0, 6 and 12:

- With four docked shortcuts: the widgets in row 12 are drawn (13 rows), the indicator's pill and dot are in their band
  directly above the dock, and the dock shows four centred cards of equal size with their icons. Looked at once.
- With `dock` empty: the PNG is byte-identical (same SHA-256) to one written by a build of the commit before this
  change from the same configuration.

## Cost at rest (task 6.1, 2026-10-09)

Release build, Y70 panel, `default-dark`. Page 1: a Clock, a 4x4 Stats widget and a Weather widget. Page 2: one Clock.
The four shortcuts (Explorer, Notepad, the command prompt, the user folder) are either on page 1 in row 8 with no dock,
or in the dock. Fresh start for each run, sampled 30 s after start; CPU over 20 s, GPU as the sum of the process's
`GPU Engine` readings over 10 samples. Two runs of each, alternating. Another UrDeck instance was running on the same
panel underneath during these runs, so the absolute GPU figures are higher than in `shortcut-and-tap.md` and only the
comparison between the two columns means anything.

| At rest | Four shortcuts on the page, no dock | Four shortcuts in the dock |
|---|---|---|
| Private bytes | 132 MB, 129 MB | 129 MB, 132 MB |
| Working set | 184 MB, 182 MB | 186 MB, 184 MB |
| CPU (of one core) | 0.92%, 1.55% | 1.68%, 1.08% |
| GPU | 1.99, 1.23 | 1.40, 1.26 |

- The idle cost does not rise: no figure differs between the columns by more than the two runs of one column differ
  from each other. CPU and GPU at rest are the Stats gauges and the 2 s system provider, which are on both.
- The second theme (a second set of typefaces, cloned from the same variable font, for the slot size) is not visible
  in private bytes: its cost is below the 3 MB spread between two runs of the same configuration. The docked cards are
  also smaller surfaces (183 pixels square against 259), which works the other way.
- The log of a start with a dock has `Pages: 2, showing 'Home'; indicator Always, 13 grid rows` and
  `Dock: 4 slot(s) shown`. A start without one has no `Dock:` line: nothing is created for it.

## Page changes with the dock shown (task 6.1, owner, 2026-10-09)

The in-place Release build on the panel, the owner's configuration (page 1: Clock, 4x4 Stats, Weather; page 2: one
Clock) with four shortcuts in the dock, by touch. One process throughout.

| Seconds after start | What happened before | Private bytes |
|---|---|---|
| 90 | 35 page changes and one launch | 146 MB |
| 143 | nothing | 147 MB |
| 227 | 28 more page changes, and a reload of an edited `dock` | 154 MB |
| 277, 317, 357 | one more reload (dock and indicator mode), then nothing | 159 MB, 159 MB, 159 MB |

- The dock was built once per layout and never for a page change: the log has one `Dock: 4 slot(s) shown` line per
  start or configuration reload, and none among the 63 `Page ... shown` lines.
- Private bytes rose from 146 to 154 MB while the pages were changed and settled at 159 MB after the last reload. The
  swipes fell inside the first four minutes after the start, in which the host's memory climbs untouched as well
  (`shortcut-and-tap.md`: 132 to 157 MB with four shortcuts on the page), so this run does not separate the two. It
  shows no growth beyond that climb and a flat figure afterwards. A run of page changes after the plateau was not
  made; the owner accepted the result as it is.

## On the panel (task 7, owner, 2026-10-09)

Y70 panel, `default-dark`, by touch on the real host. The owner's verdict for the whole list: it looks and behaves as
it should, and the sizes are right.

- Four shortcuts are a centred group under the page indicator, with cards of equal size; the log reports 13 grid rows.
- The dock stays where it is while pages slide.
- A tap on a docked shortcut launches it (`Launched 'shell:AppsFolder\...WindowsTerminal...' in 28 ms`).
- Editing `dock` in the file while the host runs: with five entries (two shortcuts, a clock, a CPU stat and a fifth
  shortcut) the host reloaded without a restart, logged `The dock has 5 entries and 4 slots; the entries after the
  first 4 are ignored.` and showed four slots. A second edit, made while the owner was on the second page, reloaded
  with `Pages: 2, showing 'Plain'`: the page that was shown stayed.
- The `fade` indicator mode was switched on for the last minutes (`indicator Fade, 13 grid rows`). The owner did not
  comment on it, or on the clock and the stat in their slots, separately from the verdict above.
