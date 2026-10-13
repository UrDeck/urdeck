---
title: Backgrounds
description: A moving background or a picture behind the cards.
section: Look
order: 21
---

The deck can show a moving picture or a wallpaper behind its cards. Pages slide over it and translucent cards, such as
the `glass` [theme](/docs/themes/), show it through. Without a `background` setting nothing of this exists: no surface,
no frames, no cost.

```json
"background": { "type": "shader", "name": "aurora", "speed": 0.5 }
"background": { "type": "image", "path": "D:/Pictures/wall.jpg" }
```

| Key | For | Meaning |
|---|---|---|
| `type` | both | `shader` or `image`. |
| `name` | shader | A built-in name, or a folder in `backgrounds/` next to the executable. |
| `speed` | shader | Multiplies time: 2 is twice as fast, 0 stops it. Default 1, from 0 to 10. |
| `scale` | shader | The resolution it is drawn at, as a fraction of the panel's. Default 0.5, from 0.1 to 1. |
| `colors` | shader | One to four colours replacing the theme's palette. |
| `path` | image | The picture. |

Any other key is a parameter of that shader. A background that is wrong is logged and the deck shows the theme's
background colour.

## Built-in shaders

All four draw with the theme's palette, so they follow the theme.

| Name | Looks like |
|---|---|
| `plasma` | slow warped fields of colour |
| `aurora` | three glowing curtains swaying along the longer side |
| `gradient` | a soft gradient whose bands drift |
| `julia` | a Julia set whose constant circles slowly (the heaviest) |

## Cost

A moving shader is drawn on the GPU about 30 times a second, at `scale` times the panel's size, stretched to fill it. At
the default half size that was about 3% of one CPU core and under 1% of the GPU on the panel UrDeck was made on. If a
shader is too heavy for your GPU, or a game beside the deck stutters, lower `scale` to `0.25`. Frames stop while the deck
is hidden and while Windows has switched the display off. A background that does not move (a picture, `speed` 0) is drawn
once and costs nothing afterwards.

## Your own

A background is a folder `backgrounds/<name>/` next to the executable, holding a `manifest.json` and a shader written in
SkSL, the shading language of Skia. UrDeck ships a command line tool, `urdeck-cli`, that makes the folder
(`shader init`), checks it (`shader validate`) and renders a picture of it (`shader render`). The manifest has a schema, so
an editor can check it as you type:

```json
{ "$schema": "https://urdeck.app/schemas/v1/shader-manifest.json" }
```

Everything about writing one, the manifest reference and the checks the tool makes is in the
[backgrounds reference on GitHub](https://github.com/UrDeck/urdeck/blob/main/docs/backgrounds.md).
