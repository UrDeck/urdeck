---
title: Themes
description: The three built-in themes and how to make your own.
section: Look
order: 20
---

A theme decides how every card on a page looks; widgets only draw content. Three themes are built in:

- `default-dark`, the default
- `default-light`
- `glass`, frosted translucent cards over a blue background, made to show a [background](/docs/backgrounds/) through them

Choose one with the `theme` key in `urdeck-config.json`. Saving the file applies it; no restart.

## Your own

A theme is a folder next to `UrDeck.Host.exe` holding one settings file and, optionally, font files:

```
themes/
  my-theme/
    theme.json
    MyFont.ttf        (optional, named in theme.json)
```

Every value in `theme.json` is optional. Anything you leave out comes from `default-dark`, so a theme can be a few lines:

```json
{
  "colors": { "background": "#0f0f1a", "accent": "#4a9eff" },
  "card": { "radius": 0.08 },
  "typography": { "font": "MyFont.ttf" }
}
```

After editing a theme, save the config file to apply it. A value that does not parse is replaced by the default's and a
warning goes to `urdeck.log`.

Colours are `#RRGGBB` or `#AARRGGBB` (alpha first). Sizes are fractions of one grid cell, so a theme looks the same on any
display. A theme can set the colours, the card (radius, border, gap, padding), the typography, the shape of gauges, the
page indicator, how a card looks while pressed, the dock's size, and the palette the built-in backgrounds draw with.

The complete list of settings, with the range of each, is the
[theme reference on GitHub](https://github.com/UrDeck/urdeck/blob/main/docs/themes.md).
