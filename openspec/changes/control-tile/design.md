## Context

Widgets read through `IReadingSource` (value and description), act through `IActionInvoker` (`provider-actions`), take
taps through `ITapTarget` (`CanTap`, `OnTap(point)`; the host draws the press, the widget draws no pressed state), and
draw with the SDK components and the theme. After `homeassistant-provider`, a description carries `Action` (a path under
the same provider) and `Icon` (a canonical key), and sample readings carry them too. The pager owns horizontal drags
(`GestureRecognizer`: past the slop a move becomes a swipe), so a widget cannot have a drag slider today. The theme has
`Accent`, `AccentDim`, `TextMuted` and `Critical`, and no on/off colours of its own. Since `provider-settings-and-secrets`,
an action starts a provider that is not running.

## Goals / Non-Goals

**Goals:** one widget for every provider's on/off-and-act readings; zero configuration beyond the reading id for a Home
Assistant light; honest feedback for a slow or failed action; nothing provider-specific in the widget.

**Non-Goals:** drag input; numeric or text states as their own styles; per-state icons; theme values for controls.

## Decisions

1. **The description drives the defaults.** `action` absent → `Describe(reading).Action` (prefixed with the reading's
   provider id); `label` absent → the description's label; `icon` absent → the description's icon. A text is used as given
   (an `action` with no `provider:` prefix is taken under the reading's provider; an `icon` is a canonical key or a local
   image file, as the shortcut's). `""` turns each off. This is the stats widget's label rule applied to all three.
   Rejected: the widget assuming `<reading>/toggle` (a Home Assistant convention inside a generic widget, wrong for media).

2. **What counts as on.** An on/off value is on when on. A text value is on when it is one of `on`, `open`, `unlocked`,
   `playing`, `home` (case-insensitive), so a media status or a lock state reads sensibly without a style of its own. A
   number is on when it is above zero. No reading → neither: the tile is neutral (an action-only scene button). These
   rules live in the widget, documented in its spec, not in providers.

3. **Colours from the theme, no new values.** On: the icon and state text in `Accent`. Off: `TextMuted`. Neutral: `Text`.
   Pending: `AccentDim` and a small progress mark (static for `motion.level` reduced). Failed: `Critical` for 2 seconds.
   A theme that wants different control colours is a later theme-format change, not this one.

4. **Pending, and no queue.** After a tap the widget invokes the action and is pending: `CanTap` returns false, so the host
   shows no press and delivers no tap. It leaves pending when the reading's value changes, when the action completes done
   and 1 second passes with no change (a scene, a button), or when it completes failed (failed look, 2 seconds) or when
   the provider's action time limit passes. The completion arrives on a worker thread; the widget only sets a flag and
   requests a repaint, as now playing does. On the dimmer's bar, a tap while pending is remembered (the latest one only)
   and sent when pending ends, so a burst of taps sends at most two actions.

5. **`dimmer`: tap to set.** At 2x1 the left part is a square on/off button (the `switch` drawing) and the rest a level bar
   drawn with the bar gauge component from the level reading. A tap on the bar at fraction f sends the level action with
   the argument round(100·f) (snapped to 0 and 100 within the outer 4 % so the ends are reachable). The level reading is
   the setting `level` (a reading id, for Home Assistant `homeassistant:light.desk/brightness`); the level action is
   `levelAction`, which follows the rule in decision 1 against the level reading's description (the Home Assistant
   provider names `light.desk/turn_on` as the natural action of a light's brightness). No default level is derived from
   `reading`: that would put a provider's path convention into the widget. Without `level`, or at 1x1, `dimmer` draws as
   `switch` (logged once when `level` is missing). Rejected for now: `IDragTarget` (needs gesture arbitration in the host;
   backlog), segment buttons (coarse).

6. **Sizes.** 1x1 (also in the dock) and 2x1. `switch` at 2x1 lays icon, label and state in a row. Layout uses only the
   content rectangle; the label shrinks to fit and is dropped before the state when the tile is very small.

7. **Refresh.** `[RefreshOnData]`; repaints when the reading's value or description changes, on pending entry and exit,
   and when the failed look ends (a one-shot timer request, as the animation frames allow). Nothing at rest.

## Risks / Trade-offs

- [Tap-to-position is less natural than a drag] → accepted by the owner; `IDragTarget` is in the backlog.
- [A text "on" list is a guess] → small, documented, and a provider can publish on/off instead.
- [Accent for "on" may be weak in some themes] → goldens in a dark and a light theme; a theme value can come later.
- [An action-only tile on a provider whose readings nobody shows] → actions start the provider since
  `provider-settings-and-secrets`, so it works.
