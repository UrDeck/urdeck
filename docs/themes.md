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
  "stroke": { "thickness": 0.08, "cap": "round" },
  "gauge": { "style": "ring" },
  "indicator": {
    "bandHeight": 0.25, "dotSize": 0.05, "pillLength": 0.14, "spacing": 0.05,
    "active": "#ffffff", "inactive": "#59ffffff", "backdrop": "#8c0f0f1a"
  },
  "press": { "scale": 0.96, "opacity": 0.85 }
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
- **`gauge.style`** is the shape a gauge takes when a widget asks for "the theme's" gauge: `ring`, `bar` or `verticalBar`.
  `plain` is not allowed here (a gauge that draws no shape is no gauge); it and any unknown name fall back to `ring`
  with a logged warning. A widget's own explicit style wins over it.

- **`indicator`** is the page indicator (see the README, "Pages"). `bandHeight`, `dotSize`, `pillLength` and `spacing`
  are fractions of one grid cell: `bandHeight` is the height of the band reserved at the bottom of the screen when the
  indicator is always shown, `dotSize` is the diameter of a dot (and the height of the pill), `pillLength` is the length
  of the current page's pill and `spacing` is the gap between neighbouring marks. `active` is the pill's colour,
  `inactive` the other dots', and `backdrop` the translucent pill behind a floating indicator (`fade` mode). A theme
  written before the indicator existed takes all of these from `default-dark`, with no warning.
- **`press`** is how a card looks while a finger is down on a widget that accepts taps (a shortcut): `scale` is the
  card's size and `opacity` its opacity, each as a ratio where `1` means unchanged. The whole card, border included, is
  scaled around its centre and dimmed by the compositor; the widget is not repainted and draws no pressed state of its
  own. `scale` must be between `0.5` and `1` and `opacity` between `0.1` and `1`; a value outside that is replaced by
  the default's with a logged warning. Set both to `1` to switch the feedback off. The built-in themes use `0.96` and
  `0.85`, settled by eye on the Y70 panel. A theme written before these existed
  takes them from `default-dark`, with no warning. How fast the card moves is not a theme value.

A gauge draws its fill in `colors.accent`, or in `colors.warning` or `colors.critical` once its reading has reached
that level, and its track in the same colour at the alpha of `colors.accentDim`. A reading that is not current (stale,
pending, unavailable) uses `colors.textMuted` for both. The number keeps `colors.text`, except where it lies over the
fill of a vertical bar: there it is `colors.text` or `colors.background`, whichever contrasts more with the fill.
Thickness and line ends come from `stroke`.

## Bundled font

The built-in themes use Inter (variable), SIL Open Font License 1.1; its licence text is in
`src/UrDeck.Engine/Themes/Builtin/Inter-OFL.txt`.
