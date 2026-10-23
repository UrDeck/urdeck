---
title: Motion
description: What moves in a theme, how much the deck lets it, and how to turn it off.
section: Themes
order: 25
---

A theme can move two things: the border of its cards, and (through its default [background](/docs/backgrounds/#theme-backgrounds)) a
shader behind them. How much of that happens is the deck's **motion level**: `full`, `reduced` or `off`.

## The border

`card.borderAnimate` moves a card's border. `"mode": "spin"` turns a conic `borderGradient` once round the card every `period`
seconds, `"mode": "pulse"` breathes its opacity.

```json
{
  "card": {
    "borderWidth": 0.012,
    "borderGradient": { "kind": "conic", "colors": ["#ff3df5", "#19e6ff", "#7a5cff", "#ff3df5"] },
    "borderAnimate": { "mode": "spin", "period": 7 }
  }
}
```

The compositor draws it in 15 steps a second over a card that is never repainted for it, and it moves only while the display is
on and the deck is shown. A `dashed` border stays still (finding `THEME057`), and so does one without a width or, for `spin`,
without a conic gradient (`THEME050`, `THEME051`). More than one continuous animation in a theme is allowed; each costs CPU while
it runs, and `theme validate` remarks on it (`THEME090`).

## The level

- You set it with `"motion"` in `urdeck-config.json`: `"full"`, `"reduced"` or `"off"`.
- Without it the deck follows Windows' *Animation effects* setting (Settings, Accessibility, Visual effects): off means `off`, on
  means `full`, and a change of that setting applies at once.
- `"motion": "full"` overrides Windows: the deck moves even with its animations off.
- A theme's `motion.level` is a **ceiling**: the level that applies is the lower of the two.

Below `full`, the border is drawn still, like a border without `borderAnimate`, and a moving shader is drawn still too, as
`speed` 0 draws it, at its first frame. At `reduced` and `off` the deck behaves alike in this version. The page slide and the
press feedback are not governed by the level.

The theme's ceiling does not touch the background: a moving default moves. The user's level does.

The log says `Motion: <level> (<what decided it>)` at startup and whenever the level changes.

## Pictures and snapshots

A snapshot (`UrDeck.Host.exe --snapshot`, `urdeck-cli theme render`) always draws the border still, at its starting angle and
full opacity. `urdeck-cli preview render --frames N` moves it in the same steps from there, composed the way the compositor
composes it (the same colours and ring, not the same bytes); `--motion reduced` or `off` keeps it still.
