## Why

Users with Windows Enterprise or Pro for Workstations can take a monitor off the desktop ("Remove display from
desktop"), which keeps the mouse, windows and games off a case display. Such a "specialized" display is invisible to
the desktop compositor and gets no touch input from Windows, so UrDeck's window cannot go there and its taps would not
arrive. Whether UrDeck can support it at all depends on facts nobody has measured, the first being whether a touch
panel's touches can be read without also acting on another display; the owner decided that touch is required. This
change answers those questions on real hardware before any refactor is made. Background:
`docs/handoff/2026-10-31-specialized-displays.md`.

## What Changes

- Diagnostic commands in the host, for developers only, not documented for users and not in the tray or settings:
  - `UrDeck.Host.exe --direct list` logs every display target Windows knows (usage kind, name, model, device path,
    adapter, preferred modes, whether it is on the desktop) and every touch digitizer, then exits.
  - `UrDeck.Host.exe --direct probe [--target <path>] [--seconds N] [--raster]` acquires one specialized display, sets
    a mode, shows a test pattern (orientation marks, a grid, a frame counter, the last touch points), reads the
    digitizers, logs what happens, and releases the display on exit.
- `docs/perf/specialized-display.md`: the answers to the seven spike questions (`design.md`), measured on the
  implementer's PC, ending in a go or no-go decision for `deck-session` and `specialized-display-output`.
- No user-visible behaviour changes; the normal start and `--snapshot` are untouched.

## Capabilities

### New Capabilities

None. The commands are throwaway diagnostics; the behaviour they inform is specified in `specialized-display-output`.

### Modified Capabilities

None.

## Impact

- `src/UrDeck.Host`: a `Direct/` folder with the probe (Display.Core, D3D interop, Raw Input), reached from
  `Program.Main` before the instance guard, like `--snapshot`. The code may be reused by `specialized-display-output`
  or rewritten; it is not a public API.
- Dependencies: one package for Direct3D 11/12 and DXGI interop in the host only (see `design.md`, D2), added to
  `Directory.Packages.props` and the third-party notices.
- Docs: `docs/perf/specialized-display.md` (new), `docs/BACKLOG.md` (a new item), `docs/ROADMAP.md` (listed under
  later work).
- CI: builds and formats the new code; nothing in it runs in CI (no specialized display there).
