# shortcut-and-tap measurements

## Spikes of 2026-10-09 (task 1.1)

Three throwaway spikes on the owner's machine, before the change was proposed. None of their code is in the repository.
No real host names are recorded here: the two sites are "site A" and "site B".

### Shell icon extraction

`SHCreateItemFromParsingName`, then `IShellItemImageFactory.GetImage` at 256 pixels with `SIIGBF_ICONONLY`.

| Target | Result |
|---|---|
| An executable (`...\steam.exe`) | The application's icon, 32 bit with alpha |
| A Start Menu `.lnk` | The target's icon, 32 bit with alpha |
| A packaged app (`shell:AppsFolder\...!App`) | The package's icon, 32 bit with alpha |
| A folder | The folder icon, 32 bit with alpha |
| An `https` address | The default browser's icon; no network request |
| `ms-settings:` | The Settings icon |
| `steam://...` | A generic page, not Steam's icon |

- Time per icon: 10 to 95 ms cold, 1 to 13 ms warm. Too slow for the UI thread when cold, so icons load on a worker.
- Row order: the bitmap is bottom-up (a positive `biHeight` in the DIB header). The loader reads the header and does
  not assume it.
- Alpha: premultiplied for most icons, straight for one. The loader treats the pixels as premultiplied unless a pixel
  has a colour channel above its alpha, then as straight. An image with no alpha at all is opaque.
- COM needs a single-threaded apartment: one worker thread, created on the first request.

### Launching from a window that never takes focus

A topmost test window with the deck's recipe (`WS_EX_NOACTIVATE`, `WM_MOUSEACTIVATE` answered with
`MA_NOACTIVATE`) launched the owner's four targets by touch through `Process.Start` with `UseShellExecute`,
synchronously in the pointer handler, with another application in front on the main monitor.

| # | Target | State before | Came to the front |
|---|---|---|---|
| 1 | Steam | not running | yes |
| 2 | Steam | running | yes |
| 3 | Windows Terminal | not running | yes |
| 4 | Windows Terminal | running | yes (a new window each time) |
| 5 | Web page, site A | browser running | yes |
| 6 | Web page, site A | browser running | yes |
| 7 | Web page, site B | browser running | yes |
| 8 | Web page, site B | browser running, Settings in front | no |

- Seven of eight launches came to the front. Launch 8 was handed a malformed address by a bug in the spike script (not
  a well-formed absolute address) while Settings was the foreground application. With the address corrected it did not
  reproduce. It is discounted for that reason, and the launcher refuses a malformed web address before it reaches the
  shell. The real host repeats the whole table (task 9.2).
- Monitor: nothing opened on the panel. A new window appeared on the monitor of the application in front, or where the
  application had last been.
- The deck window kept its place and did not take focus.

### Site icon probing

Two real sites were probed by hand, to learn what discovery has to survive.

- **Site A** links a web manifest from its page. The manifest lists PNG icons in several sizes up to 1024 pixels, and
  maskable variants beside them. A manifest reader that prefers the smallest icon at or above the wanted size and
  skips maskable ones finds a good icon in one download.
- **Site B** sits behind a sign-in page that answers every path with status 200 and HTML: the page itself, a guessed
  manifest, `/favicon.ico` and `/apple-touch-icon.png` alike. Neither the status nor the declared content type says
  "this is not an icon"; only decoding the bytes does. Such a site yields no icon, the shortcut shows the default
  browser's icon, and the `icon` setting with a local file is the way to give it its own.

The recorded test pages (`tests/UrDeck.Engine.Tests/Fixtures/icons`) are modelled on these two shapes, written for
`example.com` hosts.

## Shell icons as the engine renders them (task 5.3, 2026-10-09)

The spike's target set, plus a site with a manifest, drawn by the real icon service through `UrDeck.Host.exe --snapshot`
(a page of 1x1 shortcuts, 1100 pixels wide, `default-dark`) and looked at once:

| Target | What the card shows |
|---|---|
| `...\steam.exe` | Steam's icon, upright, round edge clean on the card |
| A Start Menu `.lnk` | The target application's icon |
| `shell:AppsFolder\Microsoft.WindowsTerminal_8wekyb3d8bbwe!App` | The Windows Terminal icon |
| `%USERPROFILE%` | The user folder icon |
| `%WINDIR%\explorer.exe` with `label` | The Explorer icon above the label |
| An `https` address whose site offers no icon | The default browser's icon |
| An `https` address whose site has a manifest | The site's 512 pixel manifest icon |
| `ms-settings:` | The Settings gear |
| `steam://open/main` | A generic page (as in the spike: Windows has no better icon for the scheme) |
| A path that does not exist | The placeholder with the file name's first letter; one warning in the log |
| No target | The empty placeholder |

- Every icon is upright and none has a dark or light fringe: the row order from the DIB header and the alpha rule hold
  for this set. Soft shadows (Terminal, the folders) blend into the card as they should.
- One `.lnk` on the machine gave a generic page every time, while two other `.lnk` files gave their application's
  icon. That is what the shell returns for that file (most likely a shortcut whose target is gone; not compared with
  Explorer), so it is not treated as a loader fault.
- The first version of the page scanner kept the first 64 `<link>` tags of a page. A large site has several hundred
  stylesheet and preload links before its icon links, so its manifest was never seen. The scanner now keeps only
  manifest and icon links (test: `OnlyManifestAndIconLinks_AreKept_HoweverManyOthersComeFirst`).

## Snapshot (task 8.2, 2026-10-09)

- A snapshot of the twelve-shortcut page takes about 1.0 s cold, site icon fetched, and 0.8 s with the icon cached.
  The icons are ready well inside the snapshot's wait; no card showed a placeholder for an icon that exists.
- First run: `Site icon for <host> fetched from <icon address>`, three requests (page, manifest, icon), and one PNG of
  25 KB in `cache/icons`, named by the SHA-256 of the address.
- Second run: `Site icon for <host> from the cache, no request`.
- A site without an icon is asked once per run: `No site icon for <host>: the site offers no usable icon. Not asked
  again until the next start.`
- `Icon worker started` appears only when a page asks for a shell icon. A page without shortcuts logs neither that
  line nor any site line.

## Press feedback on the panel (task 9.1, owner, 2026-10-09)

Y70 panel, `default-dark`, by touch on the real host:

- A finger held on a shortcut shrinks and dims the card.
- A sideways drag that starts on a shortcut returns the card to rest and moves the page; nothing is launched.
- A long hold returns the card to rest on release and launches nothing.
- A press on the clock does nothing.
- The look is approved as it is: **scale 0.96, opacity 0.85**, 80 ms in and 160 ms out. These stay the values of the
  three built-in themes.

## Launching on the panel (task 9.2, owner, 2026-10-09)

By touch on the real host, with another application in front on the main monitor. The two web pages were two public
test sites, not the owner's own pages.

| Target | Came to the front | Opened on | `Launch` held the UI thread |
|---|---|---|---|
| Steam (`steam.exe`) | yes | main monitor | 6 to 19 ms |
| Windows Terminal (`shell:AppsFolder\...`) | yes, a new window each time | main monitor | 22 to 29 ms |
| Web page with a site icon | yes | main monitor | 8 to 16 ms |
| Web page without a site icon | yes | main monitor | 10 to 11 ms |

- No launch stayed behind. No foreground step was needed: the "before launch" seam stays empty.
- Nothing opened on the panel.
- A double tap on Terminal opens one new window; a double tap on Steam brings Steam forward. Two taps more than a
  second apart are two launches, as intended.

## Icons on the panel (task 9.3, owner, 2026-10-09)

- Steam and Terminal show their own icons.
- The site that offers a 64 pixel icon shows it at twice its size, smaller than the shell icons beside it (confirmed by
  the owner): fetched once (`Site icon for <host> fetched from ...`), and after a restart
  `Site icon for <host> from the cache, no request`.
- The site that offers no usable icon shows the default browser's icon (confirmed by the owner). It is asked once per
  start.
- With `icon` set to a local `.ico` file on that shortcut, the card shows the file's picture instead of the browser's icon
  (confirmed by the owner), and no request is made for the address.
- Not tried on the panel: a real site behind a sign-in page. It ends in the same fallback as the site without a usable
  icon, and the recorded-page tests cover it.

## Cost on the panel (tasks 9.4 and 9.5, 2026-10-09)

Release build, Y70 panel, `default-dark`. Page 1: a Clock, a 4x4 Stats widget and a Weather widget, with or without
four shortcuts in row 8. Page 2: one Clock. Fresh start for each column, sampled 30 s after start; CPU over 20 s, GPU
as the sum of the process's `GPU Engine` readings over 10 samples.

| At rest | Without shortcuts | With four shortcuts |
|---|---|---|
| Private bytes | 125 MB | 133 MB |
| Working set | 173 MB | 193 MB |
| CPU | 1.22% of one core | 1.32% |
| GPU | 0.27 | 0.06 |

- CPU and GPU at rest do not differ beyond the noise of the Stats gauges and the 2 s system provider, which are on both
  pages. The shortcuts have no timer, are not repainted and are not on the frame clock. They cost about 8 MB: four
  surfaces and four icons.
- Untouched, private bytes climb for the first three minutes with and without shortcuts, then stop:

  | Seconds after start | 30 | 90 | 150 | 210 |
  |---|---|---|---|---|
  | Without shortcuts | 124 MB | 126 MB | 134 MB | 141 MB |
  | With four shortcuts | 132 MB | 138 MB | 149 MB | 157 MB |

  The run with shortcuts then stayed at 152 to 153 MB from minute 3.5 to minute 7.5. The climb is not caused by the
  shortcuts (it is there without them). The gap between the two rows grew from 8 to 16 MB in this one pair of runs;
  whether that is more than the timing of a collection was not settled, and the run without shortcuts was not followed
  to its plateau.
- During use (about 40 s of presses, ten quick page changes and six launches) the process used 5 to 15% of one core
  and the GPU reading stayed below 0.5. This is mostly the page changes; presses alone were not isolated.
- Without shortcuts the log has no `Icon worker started` line and no site icon line, and no `cache` folder is created
  (task 9.5).
- Leaving the page releases its icons: every return to page 1 logs `Icon worker started` again, which only happens
  when the icons are no longer held and must be loaded again. On page 2, private bytes fell from 160 to 149 MB at the
  collection that follows a settled swipe and stayed there with 0.2% CPU and no GPU use.