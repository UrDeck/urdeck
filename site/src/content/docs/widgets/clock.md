---
title: Clock
description: The clock widget and its settings.
section: Widgets
order: 10
---

`urdeck.widgets.clock` shows the time, and optionally the date. It is the first widget UrDeck shipped and the simplest
one to start from.

```json
{ "typeId": "urdeck.widgets.clock", "col": 0, "row": 0, "width": 4, "height": 2, "format": "24h", "showDate": true, "style": "flap" }
```

| Setting | Meaning |
|---|---|
| `format` | `"24h"` or `"12h"`. |
| `showDate` | `true` shows the date under the time. |
| `style` | `"simple"` (the default) or `"flap"`, a split-flap display whose digits flip when the minute changes. |
| `textColor` | A colour as hex, `#RRGGBB` or `#AARRGGBB`, replacing the theme's text colour. |
| `fontSize` | A multiplier of the theme's size; `1.0` is unchanged. |
| `header` | `"date"`, `"label"` or `"none"`. Absent: the date sits under the time. A header puts a row of small text above the time and no date below it: `"date"` has the date at the left and the week (`showWeek`) or the `subtitle` at the right, `"label"` has the `subtitle` at the left and the date at the right, `"none"` has neither. |
| `subtitle` | Text for the header, for example `"Rig online"`. |
| `showWeek` | `true` shows the ISO week (`Wk 42`) in the header. |
| `showSeconds` | `true` draws the seconds small after the time. |
| `blinkSeparator` | `true` blinks the colon once a second. |

The header, the seconds and the blinking colon are all off unless set. With `showSeconds` or `blinkSeparator` the card is
drawn once a second instead of once a minute (about 0.2 ms a second on the development machine); without them nothing changes.

The card itself (its fill, corners and font) comes from the [theme](/docs/themes/); the widget draws only the time.
