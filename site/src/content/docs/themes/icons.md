---
title: Icon sets
description: Give a theme its own outline icons for the weather card and for shortcuts.
section: Themes
order: 24
---

An icon set is a folder of small SVG outline icons and a `manifest.json` that says which file shows which of the product's icon
names. A theme names one with `icons.set`:

```json
{ "icons": { "set": "outline", "stroke": 0.07, "tint": "accent" } }
```

The [weather](/docs/widgets/weather/) card then draws static outline glyphs for its conditions, and a
[shortcut](/docs/widgets/shortcut/) can show a glyph with `"icon": "theme:app/terminal"`. `icons.stroke` sets the line width as a
fraction of the icon's size, `icons.tint` the colour (`accent`, `accent2`, `text`, `ramp` for the colour of `colors.ramp` that
suits the value shown, or a `#colour`) and `icons.glow` a blurred copy behind the glyph. All the keys are in the
[theme reference](/docs/themes/reference/#icons).

## Where a set comes from

`icons.set` names either a built-in set or a folder `icons/<set>/` inside the theme's folder:

```
themes/my-theme/
  theme.json
  icons/mine/
    manifest.json
    cloud-rain.svg
    bolt-cloud.svg
    house.svg
```

`outline` is built in: [Lucide](https://lucide.dev) (ISC; MIT for the icons derived from Feather). The deck looks a key up in the
theme's set first and then in `outline`, so **a set can have fewer icons than the product asks for**. A widget with an icon of
its own for the case with no set (the weather card's animated icons) uses it only when the theme names no set.

## The manifest

The manifest is required and is the only way a file is found; no file is picked up by its name.

```json
{
  "version": 1,
  "name": "mine",
  "source": "https://example.com/where-the-icons-come-from",
  "license": "ISC",
  "upstreamVersion": "1.0.0",
  "keys": {
    "weather/rain": "cloud-rain.svg",
    "weather/thunderstorms-day": "bolt-cloud.svg",
    "weather/thunderstorms-night": "bolt-cloud.svg",
    "app/home": "house.svg"
  }
}
```

- Several keys may name one file, and a file may keep its upstream name. A folder without a manifest is not a set; the log names
  the file to make.
- A path in `keys` is relative to the set's folder, ends in `.svg` and stays inside it. A `..` segment, an absolute path or a
  backslash that leaves the folder gives that key no icon.
- Say where the icons come from: the checks warn when `source` or `license` is empty or the folder has no licence file.

### The keys

| Group | Keys |
|---|---|
| `weather/` | `clear-day`, `clear-night`, `partly-cloudy-day`, `partly-cloudy-night`, `overcast`, `fog`, `drizzle`, `rain`, `sleet`, `snow`, `thunderstorms-day`, `thunderstorms-night`, `not-available` |
| `app/` | `terminal`, `folder`, `file`, `music`, `gamepad`, `browser`, `mail`, `chat`, `settings`, `code`, `camera`, `image`, `video`, `calendar`, `clock`, `home`, `power`, `lock`, `link`, `cloud`, `download`, `star`, `heart`, `bolt`, `chart` |
| `home/` | `light`, `outlet`, `switch`, `fan`, `lock`, `door`, `window`, `blinds`, `garage`, `motion`, `leak`, `thermometer`, `droplet`, `bolt`, `battery`, `scene`, `script`, `button`, `tv`, `speaker`, `entity` (things in and around a house; the [control](/docs/widgets/control/) tile and the Home Assistant provider use them) |
| `media/` | `play`, `pause`, `previous`, `next` (the [now playing](/docs/widgets/nowplaying/) card's buttons) |

A set may have fewer; the lists only grow.

## The files

An icon file is at most 4 KB, with a root `svg` whose `viewBox` is `0 0 24 24`, made of `path`, `circle`, `ellipse`, `line`,
`polyline`, `polygon` and `rect` (with `rx` and `ry`) inside `svg` and `g`. No `style`, `script`, `image`, `use`, `text`, `defs`,
`transform` or links.

The art is outlines: the deck strokes every shape with the theme's `icons.stroke` and its stroke cap, in the theme's
`icons.tint` (or `glow`), and also fills a shape marked `fill="currentColor"`. Colours and widths in the file are ignored.

The deck reads and parses a set once when it resolves the theme; drawing an icon reads nothing.

## Checking a set

`urdeck-cli theme validate` checks the sets of a theme with the findings `THEME070` to `THEME076`: a missing or invalid
manifest, a file that is missing or breaks the rules above, a key no widget asks for, a missing licence, an `.svg` no entry
names, and the keys the set lacks (which `outline` answers). See the [list of findings](/docs/troubleshooting/#theme-findings).
The [custom icons recipe](/docs/cookbook/custom-icons/) is a complete small set.
