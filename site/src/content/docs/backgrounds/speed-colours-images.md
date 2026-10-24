---
title: Speed, colours and images
description: What a moving shader costs, how it gets its colours, and how to show a picture instead.
section: Backgrounds
order: 31
---

## Speed, scale and cost

A moving shader is drawn on the GPU about 30 times a second, at `scale` times the panel's size in each direction and stretched
smoothly to fill it. At the default half size the measured cost on the panel UrDeck was made for is about 3 % of one CPU core and
under 1 % of the GPU, plus about 25 MB of memory.

- **`scale` is the remedy for a heavy shader.** If a shader is too heavy for your GPU, or a game beside the deck stutters, lower
  it (`0.25`). The shader never sees the real resolution, only `iResolution`, so nothing else changes. There is no automatic
  guard yet.
- Frames stop while the deck is hidden (its display is disconnected) and while Windows has switched the display off, and
  continue from where they stopped. `urdeck.log` says `Background frames started (...)` and `Background frames stopped (...): N
  frames in S s`.
- A background that does not move (an image, `speed` 0, a shader that does not use `iTime`) is drawn once and costs nothing
  afterwards.
- Time wraps to 0 after four hours of scaled time (`speed` times the seconds shown), so motion stays smooth after weeks of uptime.
  A shader that is not periodic over that span jumps once at the wrap: the first four built-ins are slow enough that it is hard to
  see, and the other twelve repeat exactly. Time restarts at 0 only when the background itself changes, not on an unrelated
  config save.

## Colours

Every shader gets four opaque colours, `iColor0` to `iColor3`, from the first of these that applies:

1. `background.colors` in the configuration (one to four `#RRGGBB` colours; fewer than four repeat the last).
2. The theme's `colors.backgroundPalette` (see the [theme reference](/docs/themes/reference/#the-background-palette)).
3. Derived from the theme: its background colour mixed towards its accent by 0, 20, 40 and 60 %.

An invalid colour drops the whole `colors` value with a warning. Transparency in a palette colour is ignored. A shader of your own
may ignore the palette and use its own colours.

## Images

`"type": "image"` shows a picture file covering the whole panel: scaled to the smaller size that fills both directions, centred,
the overflow cropped. A 3840x2160 picture on the 1100x3840 panel is scaled to the panel's height and its left and right parts are
cropped.

```json
"background": { "type": "image", "path": "%USERPROFILE%/Pictures/wall.jpg" }
```

- `path` is absolute, or relative to the data folder (**Open data folder** in the tray menu). Environment variables are expanded.
- PNG, JPEG, BMP and WebP files work, and the first frame of a GIF, up to 8192x8192 pixels. A photo's stored orientation
  (rotation) is applied.
- The picture is decoded once at the panel's size; the decoded original is released. While it loads the theme colour shows. Edit
  the file and save the config to load it again.
- Keys other than `type` and `path` are logged and ignored.
