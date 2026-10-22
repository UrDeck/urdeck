## Why

A theme can set `card.borderAnimate` (Rigline's turning six-colour border) and `motion.level`, and the deck reads and checks
both, but nothing moves and nothing honours a wish for less motion: the user has no setting, Windows' "Animation effects"
switch is ignored, and a moving shader keeps moving for someone who turned animations off. Rigline cannot ship until its
border turns. This is the last engine item before Rigline in `docs/design/look-and-feel/STATUS.md` ("Plan from here").

## What Changes

- **Motion policy.** One effective motion level, `full`, `reduced` or `off`, decides what may move. The user sets it with a
  new `motion` value in `urdeck-config.json` (`full`, `reduced` or `off`); without it the deck follows Windows' "Animation
  effects" setting (off means `off`, on means `full`), and a change of that setting applies without a restart. The theme's
  `motion.level` is a ceiling: the effective level is the lower of the two. The log says the effective level and why.
- **Animated border.** `borderAnimate` `spin` turns the card's conic border gradient once per `period`; `pulse` breathes
  the border's opacity between 0.55 and 1 once per `period`. Both are drawn by the compositor over the card, in steps of a
  host constant of 15 per second, and the card itself is never repainted for them. The owner watched the spike on the panel
  at 15 and 30 steps per second (2026-10-21) and accepted 15.
- **When it moves.** The border moves only at the effective level `full`, while the display is on and the deck is shown.
  At `reduced` or `off` it is drawn still, exactly as a border without `borderAnimate`. Display off or a hidden deck pause
  it.
- **Snapshot.** A snapshot draws an animated border still, at its starting angle and full opacity, with the same routine as
  a still border, so the goldens are unchanged and show what the panel shows when motion is off.
- **The user's setting reaches the background.** At an effective level below `full` *because of the user or Windows* (not
  the theme's ceiling), a moving shader background is drawn still, as `speed` 0 draws it. The theme's `motion.level` still
  does not touch the background.
- **New finding.** `THEME057` (warning): `borderAnimate` on a `dashed` border; the deck draws the border still (the
  compositor stretches the ring as a nine-grid, which a fitted dash pattern does not survive).

Not in this change: `typography.valueFill.animate` and `pattern.scroll` (still deferred, each needs its own mechanism),
an SDK `Theme.Motion` for widgets (the Weather card keeps its own `motion` setting; page slide and press are unchanged),
shipping Rigline (its theme change), a settings-window control for `motion`.

## Capabilities

### New Capabilities

- `motion`: the effective motion level (the user's `motion` value, Windows' animation setting, the theme's ceiling), when it
  is re-evaluated, what each level allows in this change, and how it is logged.

### Modified Capabilities

- `widget-card`: a border gradient may now move (`Card Border Styles`); a new requirement for the animated border (spin,
  pulse, steps, when it runs, snapshot, cost).
- `backgrounds`: a new requirement that the user's or Windows' reduced motion draws a moving shader still.
- `host-shell`: `Configuration Management` gains the `motion` value.
- `theme`: `Format 2 Values` no longer calls `motion` a group the product does not draw.
- `theme-lint`: `THEME057` in `Findings About Rules Between Values`.

## Impact

- `src/UrDeck.Engine`: a `MotionPolicy` (pure, tested on Linux); the configuration's `motion` value; the resolved border
  animation on `ResolvedCardStyle` (and its compact style); the cached card back leaves out a border the host animates;
  the conic source and ring mask pictures made by the same routine that draws a still border; `BackgroundPlan`/
  `DeckBackground` take the effective speed; `THEME057` in `ThemeCodes` and the rules.
- `src/UrDeck.Host`: Windows' animation setting read and watched (`SPI_GETCLIENTAREAANIMATION`, `WM_SETTINGCHANGE`); a
  border sprite per card as the `WidgetView`'s child visual; one motion gate shared with the `BackgroundLayer` for display
  off and a hidden deck.
- `tests/`: policy truth table, configuration parsing, back cache key, pictures, background speed, lint fixtures for
  `THEME057`. Every golden byte-identical.
- Docs: `docs/themes.md` and the site's theme page (`borderAnimate`, `motion`), the configuration reference (README and
  site), `docs/backgrounds.md`, `docs/perf/theme-look.md` (the eye check) and a perf record for the measured border,
  `docs/design/look-and-feel/STATUS.md`.
