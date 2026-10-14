# 00 Overview

## Why

UrDeck replaces HYTE Nexus with something lighter and more customisable. Functionally it is close. Visually the three
built-in themes (default-dark, default-light, glass) are rounded translucent cards, which is exactly what Nexus looks
like. Two audiences should each see something made for them:

- **Gamers with RGB** (case displays, secondary monitors): colour, glow, motion, instrument-cluster and
  sim-racing cues.
- **Business and IT** (NOC walls, desk panels): calm, dense, legible, technical or executive.

Both should say "modern, professional, clearly not a Nexus clone" in a screenshot on the landing page.

## What good looks like

A viewer of the site's theme gallery sees eight decks that differ in *structure*, not just palette: chamfered cards,
dashed outlines with registration marks, hard-shadow flat colour, gradient borders that travel, glass with a specular
edge, hairline gold on black. Each deck has a matching moving background, and the config editor can recommend one.

## Principles

1. **Themes are data.** All of the above is `theme.json` plus (for fonts and icons) files in the theme folder. A user
   can author a theme without writing C#. This is already how UrDeck works; do not break it.
2. **Widgets draw content; the host draws the card.** The new card chrome (gradient borders, glow, shadow, chamfer,
   corner marks) belongs in `WidgetPainter` / the host, not in widgets. Widgets only learn *how to draw their content
   with theme knobs*: gauge variants, sparklines, status chips, themed icons.
3. **Additive, backward compatible.** Every v1 key keeps its meaning. A theme without new keys renders pixel-identical
   to today (this is a test, see `09`, PR 1). A v1 host given a format-2 theme ignores the new keys.
4. **Cheap by default.** The panel is a secondary display driven by a small CPU budget. Each widget is its own
   CPU-raster Skia surface that repaints only when needed. Continuous animation (spinning borders, scrolling
   patterns) is opt-in per theme, bounded by `motion.level`, and budgeted (see `03`).
5. **Degrade, never fail.** Invalid values are replaced by defaults with a warning (v1 behaviour). A missing font, icon
   or image falls back to the default. A theme can never make a deck blank or crash a widget.
6. **Constrain, do not expose raw power.** No arbitrary shaders on cards, no HTML/CSS. Each knob is a named, ranged,
   documented value with a sensible default.

## Scope

In scope: everything in the README list. Out of scope (non-goals):

- **Real backdrop blur** for glass (`card.blur`). The key is reserved and ignored. See decision D1 in `10`.
- Audio-reactive backgrounds. Equalizer is free-running; a host audio-level uniform is a later spec.
- The config editor UI itself. This package only defines what it will need (`07`).
- New widgets. Existing four (Clock, Stats, Weather, Shortcut) gain options only.
- A theme marketplace, theme import/export, or per-widget theme overrides.
- Per-card animated shaders.

## Glossary

| Term | Meaning |
|---|---|
| Cell | One square grid cell, a quarter of the screen width. Sizes in themes are fractions of a cell. |
| Card | The host-drawn rounded surface behind a widget. |
| Compact card | A card of 1x1 cell or smaller, or a dock slot. Gets the `card.compact` override. |
| Bleed | Drawing outside a card's rectangle (shadow, glow, outside corner marks). Needs a surface larger than the card. |
| Palette | The four `iColor0..3` a background shader receives; from `colors.backgroundPalette`. |
| Palette roles | What each of the four colours means to one particular shader (documented in each shader's header). |
| Pairing | A recommendation link between a theme and a background. |
| Static background | A theme's still background (image, gradient or colour) used when no animated background is selected. |
| Format 2 | This package's proposed extension of the theme schema. |
| Mock | The HTML/CSS design board in `assets/design-board`. Its pixel values are the intent; its CSS is not the implementation. |

## Conversion rule used throughout

The mocks are 440px-wide decks with a 110px nominal cell. **Mock px / 110 = cell fraction.** Example: a 16px chamfer is
`0.145`. Mock paddings are roomier than the v1 default (0.065), so the draft themes set `card.padding` explicitly.
Treat the draft values as first guesses to be tuned on the real 1100x3840 panel by eye, as the v1 `dock.height` was.
