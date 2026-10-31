## Why

A case display that is a desktop monitor collects the mouse, stray windows, pop-ups and games. Windows Enterprise,
Pro for Workstations and IoT Enterprise can take such a display off the desktop ("Remove display from desktop"),
making it a specialized display that only a dedicated app draws on; users of those editions asked for UrDeck to be
that app. Today the deck simply disappears when the user does it. This change lets the same UrDeck host draw the deck,
with touch, on a specialized display. It follows `specialized-display-spikes` (whose answers it relies on, see
`docs/perf/specialized-display.md`) and `deck-session` (the presenter seam). Background:
`docs/handoff/2026-10-31-specialized-displays.md`.

## What Changes

- **Direct output.** When the deck's display is a specialized display, the host acquires it, sets its native mode,
  and draws the deck into it on the GPU, frame by frame, with no window: the same page, dock, indicator, pattern,
  background, scrim, moving borders, press feedback and page swipe as on a desktop monitor. Nothing is drawn while
  nothing changes; motion runs at 30 frames per second.
- **The same host, chosen by the display.** No mode setting: a desktop monitor gets the window, a specialized display
  gets direct output. Removing the deck's display from the desktop while UrDeck runs moves the deck to direct output
  without a restart, and putting it back moves it back to the window.
- **Touch.** Taps, presses and swipes on the panel work as on a desktop monitor, read from the panel's digitizer, and
  never act on another display.
- **Power.** The panel goes dark when Windows switches the displays off, while the session is locked, during sleep and
  after sign-out, and comes back with current values.
- **Losing the display.** Unplugged, put back on the desktop, taken by another app, or unavailable over Remote Desktop:
  the deck waits as it does for an absent display, says why in the tray and the log, and returns by itself.
- **Choosing and identifying.** Specialized displays appear in the monitor picker with a badge and can be chosen; a
  specialized display can be rotated from the picker (Windows' orientation setting does not apply there). Identify
  shows the number on the deck's specialized display too. With no display remembered, a specialized display is
  preferred.
- **Hosted views.** A widget that needs the desktop compositor (none today; a future web view) shows a placeholder on
  direct output.
- **Diagnostics.** Log lines for every step, and a frame dump for developers (`URDECK_DIRECT_DUMP`), since a
  specialized display cannot be captured with a screenshot.
- A site page ("Specialized displays") and the README.

## Capabilities

### New Capabilities

- `direct-output`: drawing the deck on a specialized display: when it is used, acquiring and releasing the display,
  mode and orientation, what is drawn and when, touch, power, losing and regaining the display, hosted views,
  diagnostics.

### Modified Capabilities

- `display-targeting`: specialized displays as deck displays: listing, matching a remembered display across the
  desktop/specialized toggle, preference when nothing is remembered, absence reasons.
- `settings-window`: specialized displays in the monitor picker, rotation, Identify on the deck's specialized display.

## Impact

- `src/UrDeck.Host`: a `Direct/` presenter (Display.Core, Direct3D interop, Skia on the GPU, a pacing thread, Raw
  Input touch, session and power notifications), built from the spike's probe; the presenter choice in the host's
  startup and on display changes; the picker and Identify.
- `src/UrDeck.Engine`: `DirectOutputController` (acquire, apply, verify, retry, release, waiting reasons) tested with
  scripted target sequences like `PlacementController`; specialized displays in the display model and matching;
  touch samples mapped to deck coordinates (rotation, range) for the existing `GestureRecognizer`.
- Configuration: `display` gains `kind` (desktop or specialized, as last seen), `rotation` (used only on a specialized
  display) and an optional `touch` (a digitizer, only when several could match); written by the host. Schema in `docs/schemas/` and the site's reference.
- Dependencies: the Direct3D binding package chosen in the spikes, host only (already added there).
- Tests: engine tests on Linux for the controller, matching and touch mapping; no specialized display in CI. Manual
  checks on the implementer's PC, recorded in `docs/perf/specialized-display.md`.
- Site: "Specialized displays" page (what it is, editions, how to turn it on and off, touch, what is not available),
  the configuration reference, the troubleshooting page's log lines. README feature list.
