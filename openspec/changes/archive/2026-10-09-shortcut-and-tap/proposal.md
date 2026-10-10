## Why

A deck you can tap is one you reach for, and today a widget receives no touch at all: the pager sees every pointer
and only the page indicator reacts to a tap. The owner's Nexus page has four launchers (two web pages, Windows
Terminal, Steam), and replacing Nexus (milestone 1, "daily driver") needs them. This change is the first half of
backlog items 7, 8 and 10: taps reach widgets, and the first widget that uses them is a shortcut. The dock follows as
its own change and reuses everything built here.

## What Changes

- **Tap input for widgets.** A widget may opt in to taps through an optional SDK interface. The host hit-tests the
  widget under the pointer, shows press feedback while the finger is down, and delivers the tap on release. Widgets
  that do not opt in are untouched and pay nothing. Swipe and tap stay separated as they are today: a gesture that
  became a swipe never taps.
- **Press feedback belongs to the host and the theme.** While a tappable widget is pressed, the host scales and dims
  that widget's layer on the compositor. The widget draws nothing for it, no frame is rendered for it, and the amounts
  are theme values.
- **An image tile component** in the SDK: draws an image into a rectangle (fitted, centred, never blown up beyond what
  looks acceptable), an optional label, and a themed placeholder when there is no image yet.
- **A launch service** on the host services a widget receives: launch a file, a folder, an application, a `shell:`
  item or a URL the way a desktop shortcut does. One place for the launch policy and the log line.
- **An icon service** on the host services: a widget asks for the icon of a source and gets an image, or nothing yet
  and a repaint when it is ready. Sources are a local image file, a shell item (executables, `.lnk`, packaged apps,
  folders, URL schemes) and a web address (the site's own icon, fetched once and cached on disk, with the shell icon
  as the fallback).
- **A 1x1 shortcut widget** (`urdeck.widgets.shortcut`): shows the icon of its target and launches it on tap.
  Settings: `target`, `arguments`, `icon`, `label` (the label is hidden by default at 1x1).
- **A check on the panel is a task, not an assumption.** A spike on 2026-10-09 launched the owner's four targets from
  a window that never takes focus: seven of eight launches came to the front, one (a malformed URL from a faulty spike
  script) did not and could not be reproduced. The real host repeats that check before the change is done.

Deliberately not in this change: the dock (the next change: a fixed row of 1x1 widget slots in the bottom band, so it
needs no item type of its own); scroll and pan routing, long press, and input for a hosted web view (the interface is
shaped so they can be added beside it); actions that are not launches, such as a Home Assistant button (a later
widget built from the same tile and tap, plus a provider command path that does not exist yet); focusing an
already-running application instead of asking Windows to open the target; moving launched windows to a chosen
monitor; a confirmation before launching; settings metadata for the editor (item 15); SVG icons; a user-wide motion
level.

## Capabilities

### New Capabilities

- `widget-input`: how the host routes a press and a tap to the widget under the pointer, press feedback, and how this
  coexists with page swipes and the page indicator.
- `launcher`: the launch service: what a target may be, how it is classified and started, what is refused, and what
  is logged.
- `icons`: the icon service: sources, the order they are tried in, site icon discovery and its network behaviour, the
  caches, and what it costs.
- `widget-shortcut`: the shortcut widget: identity, configuration, composition, tap behaviour and repaint rules.

### Modified Capabilities

- `widget-sdk`: adds the optional tap interface, and launching and icons to the host services.
- `components`: adds the image tile.
- `theme`: adds the press feedback values to a theme's contents.

## Impact

- `sdk/UrDeck.Sdk`: a tap interface (new `Input` namespace), `IWidgetHost` gains `Launch` and `Icons` as default
  members, `ImageTile` in `Components`. All additive, so `AssemblyVersion` stays 0.3.0.0 (as `weather` did); the
  package version moves to 0.7.0.
- `src/UrDeck.Engine`: the gesture recognizer raises press and press-cancelled; hit testing of a page's widgets; the
  launch target parser and launcher; the icon service (shell icons through `IShellItemImageFactory`, site icon
  discovery, memory and disk cache); press values in the theme definition and the three built-in themes; each widget
  gets its own host-services object so a request can be tied to the widget that made it.
- `src/UrDeck.Host`: `PagerController` routes press, cancel and tap to the page; `PageHost` finds the view under a
  point; `WidgetView` plays the press animation on its composition visual and repaints when the engine asks.
- `widgets/UrDeck.Widgets.Shortcut` (new, GPL, references only the SDK), added to `urdeck.slnx` and the
  `UrDeckPlugin` list in `UrDeck.Host.csproj`.
- Config: a widget object with `typeId` `urdeck.widgets.shortcut`. The top-level `dock` field is not touched here.
- Network: the engine itself makes requests for the first time. Only for a shortcut whose icon source is an
  `http(s)` address, at most once per address per 30 days, to that site and to where its icon links point; no
  cookies, no credentials. A shortcut with a local `icon` file makes none.
- Disk: an icon cache folder next to the executable (it moves with the per-user data folder of item 15).
- Security: launch targets come from the user's own configuration, the same trust as a `.lnk` on the desktop. Targets
  are handed to the shell, never to a command interpreter, and never elevated. Noted for the future "shared pages"
  import (item 15): an imported page must not bring launch targets in silently.
- Tests: gesture press and cancel sequences, hit testing, target classification, site icon discovery against recorded
  pages (a clean site, and a site that answers every request with a sign-in page), cache behaviour, the image tile,
  the shortcut widget with fake services.
- Docs: `README.md` (the widget and its settings, the network note), `docs/themes.md` (press values),
  `CONTRIBUTING.md` (tap input for authors), `docs/perf/shortcut-and-tap.md` (the spikes and the cost),
  `docs/BACKLOG.md` and `docs/ROADMAP.md`; `docs/handoff/2026-10-09-shortcut-and-dock.md` stays until the dock change
  is delivered.
