# Themes

A theme is a folder holding one settings file and, optionally, font files. The theme decides how every card on a page
looks; widgets only draw content. Three themes are built in (`default-dark`, `default-light` and `glass`, frosted translucent cards on a blue background). Your own go in a `themes`
folder next to `UrDeck.Host.exe`:

```
themes/
  my-theme/
    theme.json
    MyFont.ttf        (optional, referenced from theme.json)
```

Select a theme with the `theme` key in `urdeck-config.json`. Themes are re-read whenever the config is reloaded, so
after editing a theme, save the config file to apply it. A theme with the same name as a built-in one replaces it.

## Settings file

Every value is optional. Anything you leave out comes from `default-dark`, so a theme can be a few lines. An invalid
value (for example a colour that does not parse) is replaced by the default's value and a warning is written to
`urdeck.log`; a file that is not valid JSON makes the theme unavailable, and the default is used.

```json
{
  "colors": {
    "background": "#0f0f1a", "cardFill": "#1a1a2e", "cardBorder": "#00000000",
    "text": "#ffffff", "textMuted": "#b3ffffff",
    "accent": "#4a9eff", "accentDim": "#664a9eff",
    "good": "#3ddc84", "warning": "#ffb020", "critical": "#ff5a4d"
  },
  "card": { "radius": 0.08, "borderWidth": 0, "gap": 0.06, "padding": 0.065 },
  "typography": {
    "font": "InterVariable.ttf",
    "weights": { "value": 600, "unit": 500, "label": 400, "body": 400, "title": 300 },
    "labelSize": 0.091, "bodySize": 0.0845, "titleSize": 0.11,
    "unitRatio": 0.4
  },
  "stroke": { "thickness": 0.08, "cap": "round" }
}
```

- **Colours** are `#RRGGBB` or `#AARRGGBB` (alpha first).
- **Sizes** (`card.*`, `typography.*Size`) are fractions of one grid cell (a quarter of the screen width), so a theme
  looks the same on any display. `card.radius` is limited to half of a card's smaller side; `0` gives flat tiles.
  `card.gap` is the space between neighbouring cards.
- **`typography.font`** is either an installed font family name or the name of a `.ttf`/`.otf` file in the theme folder.
  A path that leaves the theme folder is rejected. Weights (1 to 1000) are applied through the variable-font weight
  axis; a static font is used as it is.
- **`typography.unitRatio`** is the size of a readout's unit as a fraction of its value's size.
- **`stroke`** is for components that draw lines: thickness as a fraction of the drawn element and `round` or `square`
  ends.

## Bundled font

The built-in themes use Inter (variable), SIL Open Font License 1.1; its licence text is in
`src/UrDeck.Engine/Themes/Builtin/Inter-OFL.txt`.
