---
title: Configuring UrDeck
description: Where the config file is, what it holds, pages, the dock and the monitor.
section: Start
order: 0
---

UrDeck is configured by one JSON file, `urdeck-config.json`, which sits next to `UrDeck.Host.exe`. There is no editor
yet (it is planned); you edit the file in any text editor. UrDeck notices a saved change after about a second and applies
it without a restart. A file that is not valid JSON is ignored and the last good configuration stays in use; the reason is
written to `urdeck.log`, next to the file. A missing file is created with defaults.

## The file

```json
{
  "pages": [
    {
      "name": "Default",
      "widgets": [
        { "typeId": "urdeck.widgets.clock", "col": 0, "row": 0, "width": 4, "height": 2, "format": "24h", "showDate": true }
      ]
    }
  ],
  "activePage": 0,
  "dock": [
    { "typeId": "urdeck.widgets.shortcut", "target": "https://github.com", "label": "GitHub" }
  ],
  "theme": "default-dark",
  "monitorName": "tallest"
}
```

| Field | Meaning |
|---|---|
| `pages` | The pages. Each has a `name` and a list of `widgets`. |
| `pages[].widgets[]` | A widget. `typeId` says which one; `col` (0 to 3), `row`, `width` (1 to 4) and `height` are in grid cells. A value out of range is clamped and a warning is logged; an unknown `typeId` leaves its place empty. Every other property is a setting of that widget (see the widget pages). |
| `activePage` | The index of the page shown at startup. Swiping does not change the file. |
| `dock` | Up to four widgets kept at the bottom of every page. See [the dock](#the-dock). |
| `theme` | `default-dark` (the default), `default-light`, `glass`, or the name of a folder under `themes/`. See [Themes](/docs/themes/). |
| `background` | A moving background or a picture behind the cards. See [Backgrounds](/docs/backgrounds/). |
| `monitorName` | Which monitor to choose when none is remembered yet. See [The monitor](#the-monitor). |
| `providers` | Optional settings per data provider, for example `"providers": { "system": { "intervalMs": 500 } }` reads the `system` provider every 500 ms instead of every 2000 ms. A value below the provider's minimum is raised to it. |
| `pager` | How the page indicator shows, see [Pages](#pages). |
| `motion` | How much the deck may move: `"full"`, `"reduced"` or `"off"`. Without it the deck follows Windows' Animation effects setting (off means `off`, on means `full`) and a change of that setting applies without a restart. `"full"` overrides Windows: the deck moves even with its animations off. A theme can lower the level further. Below `full`, a theme's animated border and a moving background shader are drawn still; the page slide and the press feedback are not affected. The log says `Motion: <level> (<what decided it>)`. |

## The grid

The screen is a grid of four columns of square cells; a cell is a quarter of the screen's width. On the 1100 by 3840
panel a cell is 275 pixels and a 4x2 widget is 1100 by 550. Nothing in the file is in pixels, so a page looks the same
on any display; the number of rows is what the height allows.

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

## The monitor

UrDeck stays on one physical monitor. The first time it starts it chooses one by `monitorName` and remembers it as
`display` in the file, so the monitor you plug in later, or the order Windows numbers them in, does not move the deck.

`monitorName` is one of `"primary"`, `"tallest"` (the tallest portrait monitor, the default), `"widest"`, `"largest"`
(most pixels), or a device name such as `"DISPLAY1"`. `monitor` is a 1-based index used when the name matches nothing.

You do not edit `display`. To make UrDeck choose again, for example because the first start picked the wrong monitor
while yours was switched off, remove `display` from the file or use **Choose automatically** in Settings.

Left-click the tray icon, or right-click it and choose **Settings**, to open the settings window. It lists every
connected monitor with its name, resolution, orientation and a number, and the one the deck is on says "Deck is here".
Click a monitor to move the deck there and keep it there. **Identify** shows each monitor's number in its corner for two
seconds.

After sleep, a monitor being plugged in or out, or a change of resolution or scaling, UrDeck waits for the monitors to
settle and puts the window back on its monitor. While that monitor is not connected the deck is hidden, its widgets stop,
and the tray icon says it is waiting; it comes back by itself.

## Closing and starting

The deck window never takes focus, so touching it does not interrupt a game or another application. As a consequence
Escape does not close it: right-click the tray icon and choose **Quit**. On Windows 11 a new icon may sit in the overflow
flyout (the `^` arrow) until you drag it to the taskbar. **Start with Windows** in the same menu starts UrDeck at sign-in.

`urdeck.log` is the place to look when something is not as you expected. Each line starts with the date and time, and at
5 MB the file becomes `urdeck.log.1`.
