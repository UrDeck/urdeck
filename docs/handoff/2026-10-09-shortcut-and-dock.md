# Handoff: the dock (2026-10-09)

Starting point for the `/opsx:explore` session on the dock (backlog item 10). The first half of the original handoff,
tap input and the shortcut widget, was delivered as the OpenSpec change `shortcut-and-tap`; what follows is what that
change settled and what is left for the dock. This note is scaffolding: it is deleted in the PR that delivers the
dock, once that change's `design.md` holds the decisions. Read `AGENTS.md`, `README.md` (the "Shortcut widget" and
"Pages" sections), `docs/ROADMAP.md` and `docs/BACKLOG.md` (items 7, 8, 10, 11, 15) first.

## Start here

1. Finish `shortcut-and-tap` if it is not archived yet: its tasks 9.1 to 9.5 are checks on the panel with the owner
   (press feedback and the press values, launching the four targets by touch, icons, the cost). The dock builds on all
   of them, so a problem found there (a launch that stays behind, a press that looks wrong) is fixed first.
2. Branch off `main` after `shortcut-and-tap` has merged (`feat/dock`). One PR, title like
   `feat(dock): a row of shortcut slots on every page`.
3. `/model opus`, then: "Read `docs/handoff/2026-10-09-shortcut-and-dock.md` and explore this with me."
4. `/opsx:explore`, `/opsx:propose` (suggested name `dock`), `/opsx:apply`, verify on the panel, `/opsx:archive`,
   update `docs/BACKLOG.md` (Current state, item 10) and `docs/ROADMAP.md` (milestone 1), delete this handoff.

## What `shortcut-and-tap` settled (do not design these again)

All of it is in `openspec/changes/shortcut-and-tap/design.md`, with the alternatives that were weighed.

- **Tap input.** `UrDeck.Sdk.Input.ITapTarget` (`CanTap(point)`, `OnTap(point)`), optional and beside `IWidget`.
  Implementing it is the capability declaration. The gesture recognizer raises `Pressed` and `PressCancelled`; the
  engine's `WidgetHitTest` maps a point to the widget whose card contains it. Scroll and long press will be further
  interfaces beside it and were left out.
- **Press feedback is the host's.** The widget's composition visual is scaled and dimmed with the theme's `press`
  values (`PressStyle`); no repaint, no frame clock, no pressed state in a widget or in the image tile.
- **Per-widget host services.** `WidgetServices` (engine) is created per widget by the registry, carries readings, log,
  `Launcher` and `Icons`, raises `RepaintRequested`, and is disposed by the view. `WidgetView` owns all of that
  plumbing: a `WidgetView` works anywhere it is placed, not only on a page.
- **Launching.** `ILauncher.Launch(target, arguments)` on the host services; `LaunchTarget.Parse` in the SDK classifies
  a target. One launcher for every widget: shell execute, default verb, never elevated, a one-second repeat guard, a
  "before launch" seam for a foreground fix.
- **Icons.** `IIconSource.GetIcon(source, pixelSize)` on the host services: local image files, shell icons through
  `IShellItemImageFactory` on an STA worker, site icons with discovery and a 30 day disk cache in `cache/icons`.
  Reference-counted per `WidgetServices`, so an icon lives while a view that asked for it does.
- **The image tile** (`UrDeck.Sdk.Components.ImageTile`) and **the shortcut widget** (`urdeck.widgets.shortcut`, 1x1,
  settings `target`, `arguments`, `icon`, `label`).
- **Security stance.** Launch targets are the user's own configuration, trusted like a `.lnk`; no confirmation. Noted
  for item 15: an imported page must not bring targets in silently.

## Direction for the dock (from the same exploration)

The dock is **a fixed row of 1x1 widget slots in the bottom band, present on every page**. An entry of the `dock`
config field is a widget object, normally a shortcut; the dock has no item type, drawing, press or launch code of its
own. This is why nothing in `shortcut-and-tap` assumes that a tappable thing is on a page: page-specific code is in
`PagerController` and `PageHost`, the rest is reusable.

## What is left to decide

- **Slot size.** A full grid cell (275 pixels on the panel: large for a dock, and it costs a whole row), or a smaller
  cell that fits the band. A smaller cell means the widgets are painted at a size the grid never produces: check that
  the theme resolves sensibly for it (padding, radius and label size are fractions of a grid cell) and that the
  shortcut's composition holds. The owner's Nexus page is the reference for how big dock items feel.
- **The band.** The page indicator already reserves or floats over a band at the bottom (`ChromeLayout`,
  `pager.indicator`). Decide how the dock and the indicator share the bottom of the screen: one band holding both, two
  bands, or the indicator above the dock. `ChromeLayout.Compute` is where the grid's height is decided, and the layout
  must reserve the dock's space the same way. A widget that no longer fits is not placed and an error is logged; a dock
  that takes a row from an existing page needs a clear message.
- **Number of slots and overflow.** Four across like the grid, or more, smaller ones. What happens with more entries
  than slots: ignored with a warning (simplest), or the dock scrolls (needs pan routing, which does not exist).
- **Where the views live.** The dock is the same on every page, so its `WidgetView`s should not be disposed and rebuilt
  on a swipe, and should not slide with the page. That suggests a host-level strip in the overlay beside the indicator,
  with its own hit test before the page's (the order in `PagerController.Tap` and `Press` is: indicator, then page).
  The engine's `WidgetHitTest` takes any list of layout items, so a one-row layout for the dock can reuse it.
- **Swipes that start on the dock.** A horizontal swipe on a dock item: page change (consistent with a shortcut on a
  page) or nothing. State it.
- **Config shape.** `dock` is an array today (`[]` in the sample). Entries as widget objects without `col`/`row` (the
  position is the index), or with a slot index. Keep it flat and typed for the editor (item 11). Should any 1x1 widget
  be allowed in a slot (a clock, a single stat), or only shortcuts? Allowing any costs nothing if a slot is a real cell.
- **Snapshot.** `--snapshot` should draw the dock (`PageRenderer` has the chrome for the indicator; the dock is drawn
  the same way).
- **Cost.** The dock's icons are alive on every page for as long as the app runs. Measure against the same page
  without a dock; the idle cost must stay about zero (`docs/perf/shortcut-and-tap.md` has the shortcut figures once
  task 9.4 is done).

## Verification expectations

`AGENTS.md` commands, 0 warnings, `openspec validate --all --strict`. On the real panel: the dock is there on every
page and does not move during a swipe, a tap on a dock item launches it and it comes to the front, the indicator and
the dock do not overlap, and a page's widgets never go under either. Prefer asking the owner to look over automated
pixel loops.

## Not in scope

The editor, settings metadata attributes, scroll routing, the web page widget, backgrounds, display targeting, focusing
a running application instead of launching, moving launched windows to a chosen monitor.
