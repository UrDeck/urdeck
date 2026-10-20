---
title: Themes
description: The five built-in themes and how to make your own.
section: Look
order: 20
---

A theme decides how every card on a page looks; widgets only draw content. Five themes are built in:

- `default-dark`, the default
- `default-light`
- `glass`, frosted translucent cards over a blue background, made to show a [background](/docs/backgrounds/) through them
- `carbon`, near-black with hairline cards and corner marks, mono numerals and one phosphor accent. It uses Instrument Sans and JetBrains Mono and defaults to the `contour` background.
- `paper`, the daylight option: warm off-white cards with soft shadows and one vermilion accent. It uses Instrument Sans and JetBrains Mono and defaults to the `contour` background.

Choose one with the `theme` key in `urdeck-config.json`. For example `"theme": "carbon"` or `"theme": "paper"`. Saving the file applies it; no restart.

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
cannot make is written to `urdeck.log` once. The gauge styles `meter`, `tach`, `marker` and `thickbar` and the gauge values (`sweep`, `segments`, `ticks`, `scale`, `redline`, `marker`, `gradient`, `glow`, `track`, `outline`, `layout`, `readoutGap`, `labelPosition`) are drawn too. The card's shape, fill and border are drawn today too: chamfered corners
(`card.shape`, `cut`, `cutCorners`), a gradient fill (`card.fillGradient`), a colour per card from `colors.cardTints`
(`card.tint: "cycle"`, row by row on each page, by position in the dock), and dashed or gradient borders (`card.borderStyle`,
`borderDash`, `borderGradient`, linear or conic); a dashed border has a dash on every corner. So is the light and the marks on
a card: a highlight along the top or bottom edge and a radial sheen (`card.highlight`, `card.sheen`), a lit strip along one
edge in a colour per card (`card.accentStrip`), and registration marks at the corners (`card.cornerMarks`), on or outside the
card. Outside the card, a drop shadow (`card.shadow`, soft or a hard offset) and a glow (`card.glow`) are drawn under all the
cards, so they never cover a card's content; a pressed card's shadow shrinks with it. On a translucent card, a shadow or glow
reaches no further than the gap between cards, so it never shows through the neighbouring card. Small cards, the dock's slots and
any card of one grid cell, can look different: `card.compact` gives them their own radius, border width and shape (including a
`circle`), and a strip or marks of their own merged over the page's. A page pattern (`pattern`) is drawn too: one texture over the whole window, above the background and under the cards, the dock and the page indicator, that stays in place when the pages slide. `kind` is `dots` (radius `thickness`), `grid` (lines, with every `majorEvery`-th one in the `major` style), `hatch` (parallel lines at `angle`, 45 degrees rising to the right by default) or `scanlines` (horizontal lines); `spacing` and `thickness` are fractions of a grid cell, marks sit on whole pixels (the spacing is rounded, lines are at least one pixel wide and a hatch's angle moves by under a degree so its lines meet), and without a `color` it is the muted text colour at a sixteenth of its opacity. `angle` is used by `hatch` only, `majorEvery` and `major` by `grid` only. The `lines` kind and `pattern.scroll` are not available yet. The other values are checked and kept today and drawn by later releases; a file
that uses none of them is unaffected. A theme's [background](/docs/backgrounds/#theme-backgrounds), a default shader, a
still gradient, colour or picture and a scrim, is drawn today when your configuration has no `background` of its own, and
`meta.pairsWith.backgrounds` lists the backgrounds that suit the theme, for a settings page to recommend. The
[JSON Schema](/schemas/v2/theme.json) lists them for editor completion.

The complete list of settings, with the range of each, is the
[theme reference on GitHub](https://github.com/UrDeck/urdeck/blob/main/docs/themes.md).
