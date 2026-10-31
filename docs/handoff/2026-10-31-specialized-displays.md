# Handoff: specialized displays (direct output on a display removed from the desktop)

**For:** whoever implements the three changes below, on a PC with Windows 11 Pro for Workstations, Enterprise or IoT
Enterprise and a touch case display (the owner's friend, working with an agent). **Date:** 2026-10-31.
**Order:** (A) `specialized-display-spikes`, then, only if A says go, (B) `deck-session` and (C)
`specialized-display-output`. One pull request per change.
**Read first:** `AGENTS.md`, `openspec/specs/display-targeting/spec.md`, `openspec/specs/settings-window/spec.md`,
`openspec/specs/widget-input/spec.md`, `src/UrDeck.Host/MainWindow.cs`, `src/UrDeck.Engine/Rendering/PageRenderer.cs`,
`src/UrDeck.Engine/Display/PlacementController.cs`, `src/UrDeck.Engine/Input/GestureRecognizer.cs`.

Nothing here is built. This page records what the exploration decided with the owner, so the implementer does not
start from zero. The owner cannot run any of A or C: their Windows edition has no specialized displays. Everything in A
and C is verified on the implementer's PC, and the results are written down in the repository (see "Reporting").

## What a specialized display is

Windows 10 2004 and later (Enterprise, Pro for Workstations, IoT Enterprise) let a user take a monitor off the desktop:
Settings > System > Display > Advanced display > "Remove display from desktop". Microsoft's pages:
[specialized monitors](https://learn.microsoft.com/en-us/windows-hardware/drivers/display/specialized-monitors),
[building a custom compositor](https://learn.microsoft.com/en-us/windows-hardware/drivers/display/specialized-monitors-compositor),
[the sample](https://github.com/microsoft/Windows-classic-samples/tree/main/Samples/DisplayCoreCustomCompositor).

What Windows does to such a display, and what it means for UrDeck:

| Windows | Consequence for UrDeck |
|---|---|
| The desktop compositor (DWM) ignores it: no windows, no XAML, no taskbar | The deck window cannot go there. UrDeck draws the frames itself through `Windows.Devices.Display.Core` |
| The OS does not power it on while the PC is in use | UrDeck sets the mode, and UrDeck has to blank it on display off, lock and sleep (burn-in on a case LCD) |
| It receives no touch or mouse input | Touch has to be read from the digitizer directly, and must not also land on another display |
| Only Display.Core lists it (`DisplayTarget.UsageKind` = special design) | The monitor picker and placement do not see it today |
| An app acquires it for dedicated control | One owner at a time; another app (or a second UrDeck) may hold it |
| Indirect display drivers (USB display adapters, Miracast) are not addressable | Only displays on a real GPU output work |

## Why the owner wants it

Two reasons, from the owner: keep the desktop off the panel (the mouse, windows, pop-ups and games never land on the
case display), and users with such editions asked for it. Not reasons: lower overhead, cleaner sleep (welcome if they
come).

## Decisions taken

- **One host.** The XAML application keeps running for the tray, the settings window and the coming config editor
  (milestone 3). On a specialized display the deck has no window; a second way of presenting the deck draws it. Which
  one is used follows from the deck's display: a desktop monitor gets the window, a specialized display gets direct
  output. No separate executable, no new setting for the mode.
- **Touch is required.** If the spikes show that touches on the panel cannot be read, or cannot be kept from also
  acting on another display, the feature stops after A (no-go), and the decision is recorded. A view-only deck is not
  shipped.
- **A gap in hosted views is accepted.** Direct output draws everything `PageRenderer` draws (widgets, dock, indicator,
  pattern, background, scrim, moving borders) plus press feedback and the page swipe. Hosted views that need the
  desktop compositor (a future web view) may not be available there; a widget that needs one shows a placeholder and
  the site says so. Nothing in the SDK may come to assume a window.
- **Spikes first, as their own change.** A is small and answers the questions that can kill the feature (touch) or
  change the design (rendering path, rotation, identity, access) before anything is refactored.
- **The refactor is separate and verifiable by the owner.** B moves the deck's orchestration out of `MainWindow`
  without changing behaviour, so the owner checks it on the Y70 as a desktop monitor, and the goldens and tests guard it.
- **The proposals are batched** in one docs pull request (with this page); apply makes one pull request per change.

## The changes

**A. `specialized-display-spikes`.** Diagnostic commands in the host (`--direct list`, `--direct probe`), never shown
to users, that answer seven questions (S1 to S7 in its `design.md`): where touches go, whether the digitizer can be
read, which rendering path works, rotation, identity across the toggle, access and events, and power. The result is
`docs/perf/specialized-display.md` with a go or no-go.

**B. `deck-session`.** `MainWindow` today both decides what the deck shows (config reloads, theme, background plan,
motion, pages, layout, readings to repaint, widget lifetimes) and shows it in XAML. B splits the first half into a
`DeckSession` that knows nothing of XAML (engine side where it can be, tested on Linux) and an `IDeckPresenter` seam
with one implementation, the window. No behaviour change.

**C. `specialized-display-output`.** The direct presenter: acquiring the display, mode and rotation, drawing the page
on the GPU into the display's surfaces, pacing, touch, power, losing and regaining the display, the picker's rows,
Identify, the logs, the site page.

## How to work on it

- Branches and pull requests as in `AGENTS.md` (never `main`; conventional commit titles; squash merge). The
  implementer's agent reads `AGENTS.md`; point it at this page and the change's folder.
- Build and test commands are in `AGENTS.md`. Everything must build with 0 warnings and pass `dotnet format`.
- The owner reviews and merges. Manual checks the owner can run (B, and C's desktop path) are marked in the tasks.
- `urdeck.log` is the main evidence. A screenshot does not capture a specialized display, so C has a frame dump
  (`URDECK_DIRECT_DUMP`) and phone photos fill the rest.

## Reporting

Spike results go in `docs/perf/specialized-display.md`, one section per question, with: the PC (Windows edition and
build, GPU and driver, the display's name and how it is connected), what was run, what was seen (log lines, photos,
numbers), and the answer. The file ends with "Decision: go" or "Decision: no-go, because ...". C adds its
measurements (idle and moving CPU and GPU, memory) to the same file, against the window path on the same PC.
