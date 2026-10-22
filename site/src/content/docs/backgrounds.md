---
title: Backgrounds
description: A moving background or a picture behind the cards.
section: Look
order: 21
---

The deck can show a moving picture or a wallpaper behind its cards. Pages slide over it and translucent cards, such as
the `glass` [theme](/docs/themes/), show it through. A theme can bring a background of its own, and a configuration
without a `background` shows the theme's choice; **`"background": { "type": "none" }` switches every background off**. A
deck with no background costs nothing: no surface, no frames.

```json
"background": { "type": "shader", "name": "aurora", "speed": 0.5 }
"background": { "type": "image", "path": "D:/Pictures/wall.jpg" }
"background": { "type": "none" }
```

| Key | For | Meaning |
|---|---|---|
| `type` | all | `shader`, `image` or `none`. |
| `name` | shader | A built-in name, or a folder in `backgrounds/` next to the executable. |
| `speed` | shader | Multiplies time: 2 is twice as fast, 0 stops it. Default 1, from 0 to 10. |
| `scale` | shader | The resolution it is drawn at, as a fraction of the panel's. Default 0.5, from 0.1 to 1. |
| `colors` | shader | One to four colours replacing the theme's palette. |
| `path` | image | The picture. |

Any other key is a parameter of that shader. A background that is wrong is logged and the deck shows what a failed
background shows: the theme's still background if it has one, else its background colour.

## Theme backgrounds

A theme can choose a default shader, a still background (a gradient, a colour or a picture in the theme's folder) and a
scrim, a veil that keeps text readable over a busy background. The deck shows the first of these that applies:

1. your `background`, `none` included
2. the theme's default shader
3. the theme's still background
4. the theme's background colour

A background that fails is logged once and the next one is tried, so a theme with a still gradient shows the gradient
when its shader cannot run. Choosing a shader or an image yourself shows that one, not the theme's. To switch a theme's
background off, set `"background": { "type": "none" }`; delete the key to get the theme's choice back. The three built-in
themes choose no background, so nothing changes for them. A theme's `motion.level` does not affect the background, but your
own motion setting does: with `"motion": "reduced"` or `"off"` in the configuration, or with Windows' Animation effects switched
off and no `motion` in the configuration, a moving shader is drawn still. `"motion": "full"` brings it back.

## Built-in shaders

Sixteen ship with UrDeck. They draw with the theme's palette, so they follow the theme (`spectrum` is the exception: its
colours are hues). Each has a few settings; the [reference](https://github.com/UrDeck/urdeck/blob/main/docs/backgrounds.md#built-in-shaders)
lists them with their ranges and what the four palette colours mean.

| Name | Looks like |
|---|---|
| `plasma` | slow warped fields of colour |
| `aurora` | three glowing curtains swaying along the longer side |
| `gradient` | a soft gradient whose bands drift |
| `julia` | a Julia set whose constant circles slowly (the heaviest of the first four) |
| `horizon` | a retro striped sun over a neon grid rolling toward you |
| `spectrum` | three soft lights walking the colour wheel, RGB-keyboard style |
| `contour` | topographic contour lines of a slowly changing landscape |
| `nebula` | warped colour clouds under an uneven, twinkling star field |
| `streams` | thin packets of light falling in columns, like data |
| `ripple` | sunlight caustics on a pool floor |
| `embers` | sparks lifting off a glowing horizon, dark at the top |
| `plotter` | a drafting grid with a slow scan line and dashed rings |
| `shapes` | flat outlined shapes bobbing and turning, for light, bold themes |
| `warpgate` | falling through a tunnel of starlight streaks, rings and a swirling core |
| `equalizer` | spectrum bars with falling peak caps (it does not listen to audio) |
| `vortex` | light rays spiralling into a glowing core in two opposite layers |

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
