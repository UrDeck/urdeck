---
title: Fonts and licences
description: How a theme names fonts, what the built-in themes use, and what to include when you share a theme.
section: Themes
order: 23
---

A theme names its fonts in `typography`: `typography.font` is the default for every role, and `typography.fonts.value`, `unit` and
`label` override it for readout values, units and labels. Each is either **an installed font family** (`"Segoe UI"`) or **the
name of a `.ttf`, `.otf` or `.ttc` file in the theme folder** (`"MyFont.ttf"`).

## Use a file, not a family

A family name only works where that font is installed. A theme you share, or move to another machine, should carry its fonts:

```
themes/
  my-theme/
    theme.json
    MyFont.ttf
    MyFont-OFL.txt
```

- A path that leaves the theme folder is refused (`THEME020`).
- A font named by family is used only where it is installed, and `urdeck-cli theme validate` says so (`THEME022`).
- A role whose font cannot be loaded falls back one step at a time: the role's font, `typography.font`, the default theme's font
  (Inter), the system font. Each missing font is one warning in `urdeck.log` per loaded theme.
- Weights, 1 to 1000, are applied through a variable font's weight axis; a static font is used as it is. A weight the file
  cannot make is drawn at the nearest weight it has, with one warning (`THEME023`).
- A font with an optical size axis (`opsz`) can be set by the theme with `typography.opticalSize` `auto`.

## Licences

A font file is someone's work, and almost every font that can be shared comes with a licence file. Keep it beside the font
(`*-OFL.txt`, `LICENSE*` or `*-LICENSE*`): `theme validate` warns when a font has none (`THEME021`), because without it the theme
cannot be shared. The same warning is given for a font file over 3 MB; subset it to the characters you need.

## What the built-in themes use

All of these are under the SIL Open Font License 1.1, and their licence texts ship with the application.

| Theme | Fonts |
|---|---|
| `default-dark`, `default-light`, `glass` | Inter (variable) |
| `carbon`, `paper` | Instrument Sans (variable) for text, JetBrains Mono (variable) for numerals |
| `prism` | Bricolage Grotesque (variable) |
| `obsidian` | Bodoni Moda (variable) for values, Jost (variable) for labels and text |
| `ember` | Barlow Condensed Bold (static) |
| `blueprint` | IBM Plex Mono Regular (static) |
| `pop` | Archivo (variable; weight 900 for numbers) |
| `rigline` | Chakra Petch Bold (static) |

The licence texts are in the repository, in
[`src/UrDeck.Engine/Themes/Builtin/`](https://github.com/UrDeck/urdeck/tree/main/src/UrDeck.Engine/Themes/Builtin), and
[`THIRD-PARTY-NOTICES.md`](https://github.com/UrDeck/urdeck/blob/main/THIRD-PARTY-NOTICES.md) lists them.

The font `theme init --from` copies into your folder comes with its licence file, so a copy of a built-in theme can be shared as
it is (check the licence of the font you add to it).
