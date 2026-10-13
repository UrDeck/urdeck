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

The card itself (its fill, corners and font) comes from the [theme](/docs/themes/); the widget draws only the time.
