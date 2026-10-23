---
title: Pages and the dock
description: Several pages, the page indicator, and the row of up to four widgets kept at the bottom.
section: Start
order: 1
---

## Pages

A configuration can hold several pages. Swipe left or right on the deck (mouse, touch or pen) to change page; the page
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
page slides. It is the `dock` list of the file. An entry is a widget like those on a page, without a position: its place
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

