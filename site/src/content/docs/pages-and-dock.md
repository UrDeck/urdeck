---
title: Pages and the dock
description: Several pages, the page indicator, and the row of up to four widgets kept at the bottom.
section: Start
order: 1
---

## Editing a page

Open the editor (left-click the tray icon) and choose **Pages**. It opens on the page the deck shows. The preview is drawn by
the deck itself, for the deck's display, so text fits and wraps as it will on the deck; **Fit width** scrolls a column of
comfortable cells, **Whole page** shows everything at once. **Readings** switches the preview between your live values and the
providers' sample values; a provider that is not set up yet shows its sample values in Live, so you see what the widget looks like.

- **Add widget**, or the **+** in an empty cell (with **Cells** on), lists the installed widgets. The new one gets its default
  settings and size, in the cell you clicked when it fits there, otherwise in the first free space from the top left. When the
  page has no room, nothing is added and the editor says so.
- Click a widget (or use the arrow keys in the preview) to select it. The pane on the right shows its type, the sizes it has
  and **Remove** (or press Delete). A size that does not fit says what is in the way and offers to move the widget to the first
  free space of that size; no other widget moves.
- With nothing selected the pane shows the page: its name, whether it is the start page, its order, the page dots and Delete.
- The tabs above the preview are the pages, the start page marked ⌂. **+** adds an empty page after the current one, and
  right-clicking a tab renames, duplicates, moves (left or right), deletes it or makes it the start page; the page pane, with nothing selected, has the same. Deleting a page with widgets asks
  first; deleting the start page makes the next page (or the previous, for the last) the start page.
- Every change is saved as you make it and reaches the deck at once. **Undo** and **Redo** (Ctrl+Z, Ctrl+Y) take changes back
  one at a time, also a change made to the file in a text editor. While the editor is open the deck shows the page you are
  editing; swiping on the deck moves the editor to that page too.

## Pages

A configuration can hold several pages. Each has an `id` that never changes, so the deck stays on a page when you rename or
move it, and `startPage` names the page the deck starts on (without it, the first page). Swipe left or right on the deck (mouse, touch or pen) to change page; the page
follows your finger, and swiping past the first or last page bounces back. Only the page you are on is alive, so more
pages cost nothing while you look at one.

An indicator shows where you are: one dot per page, the current one a pill. Tap a dot to go to that page. Its
`pager.indicator` setting is one of:

- `always`: a band at the bottom is reserved for it and it is always visible.
- `fade`: it floats over the page and fades out a couple of seconds after a swipe.
- `off`: no indicator.
- `auto` (the default): hidden with one page; with several pages `always` when at least four whole rows remain, otherwise `fade`.

```json
{ "pager": { "indicator": "auto" } }
```

## The dock

The dock is a row of up to four widgets at the very bottom of the screen, the same on every page and not moving while a
page slides. In the editor, the dock is the strip under the preview on **Pages**: add a widget there, drag to reorder, select one to change its settings. In the file it is the `dock` list. An entry is a widget like those on a page, without a position: its place
is its index in the list, the first at the left.

```json
{
  "dock": [
    { "typeId": "urdeck.widgets.shortcut", "target": "C:\\Program Files (x86)\\Steam\\steam.exe" },
    { "typeId": "urdeck.widgets.shortcut", "target": "%USERPROFILE%\\Documents" },
    { "typeId": "urdeck.widgets.shortcut", "target": "https://example.com" }
  ]
}
```

- The entries are shown as one group, centred. Entries after the fourth are ignored with a warning; they stay in the file.
- Any widget that has a 1x1 size can sit in a slot: a shortcut, a clock, a single stat. The card is a smaller miniature of
  the widget's 1x1 card. `col`, `row`, `width` and `height` on an entry are ignored.
- A widget type with no 1x1 size, an unknown `typeId` or an entry with `"isVisible": false` leaves its slot empty.
- With no `dock` there is no dock and nothing is reserved. On the 1100 by 3840 panel the dock fits in the space under the
  thirteenth row, so no row is lost; on other displays it may take a row.

