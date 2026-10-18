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
cannot make is written to `urdeck.log` once. The card's shape, fill and border are drawn today too: chamfered corners
(`card.shape`, `cut`, `cutCorners`), a gradient fill (`card.fillGradient`), a colour per card from `colors.cardTints`
(`card.tint: "cycle"`, row by row on each page, by position in the dock), and dashed or gradient borders (`card.borderStyle`,
`borderDash`, `borderGradient`, linear or conic); a dashed border has a dash on every corner. So is the light and the marks on
a card: a highlight along the top or bottom edge and a radial sheen (`card.highlight`, `card.sheen`), a lit strip along one
edge in a colour per card (`card.accentStrip`), and registration marks at the corners (`card.cornerMarks`), on or outside the
card. Outside the card, a drop shadow (`card.shadow`, soft or a hard offset) and a glow (`card.glow`) are drawn under all the
cards, so they never cover a card's content; a pressed card's shadow shrinks with it. On a translucent card, a shadow or glow
reaches no further than the gap between cards, so it never shows through the neighbouring card. The other values are checked and kept today and drawn by later releases; a file
that uses none of them is unaffected. A theme's [background](/docs/backgrounds/#theme-backgrounds), a default shader, a
still gradient, colour or picture and a scrim, is drawn today when your configuration has no `background` of its own, and
`meta.pairsWith.backgrounds` lists the backgrounds that suit the theme, for a settings page to recommend. The
[JSON Schema](/schemas/v2/theme.json) lists them for editor completion.

The complete list of settings, with the range of each, is the
[theme reference on GitHub](https://github.com/UrDeck/urdeck/blob/main/docs/themes.md).
