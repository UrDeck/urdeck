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

Format 2 adds optional values for card chrome (chamfered corners, shadows, dashed or gradient borders), a page pattern, per-role
fonts, more gauge styles, icons and a background of its own. The typography values are drawn today: a font per role (values,
units, labels) with a fallback to the theme's font, the optical size of fonts that have one (`"opticalSize": "auto"`), upper- or
lower-case labels and titles, letter spacing, slanted values, a gradient fill and a glow behind values. A weight the font
cannot make is written to `urdeck.log` once. The other values are checked and kept today and drawn by later releases; a file
that uses none of them is unaffected. A theme's [background](/docs/backgrounds/#theme-backgrounds), a default shader, a
still gradient, colour or picture and a scrim, is drawn today when your configuration has no `background` of its own, and
`meta.pairsWith.backgrounds` lists the backgrounds that suit the theme, for a settings page to recommend. The
[JSON Schema](/schemas/v2/theme.json) lists them for editor completion.

The complete list of settings, with the range of each, is the
[theme reference on GitHub](https://github.com/UrDeck/urdeck/blob/main/docs/themes.md).
