## Why

With `homeassistant-provider` a deck can show a lamp's state and call the service that toggles it, but no widget does
both: stats shows a value and cannot act, the shortcut launches programs, now playing is for media. GitHub issue #119
asks for a Home Assistant toggle tile and wonders whether the shortcut could do it. Neither is the right shape: the
widget should not know Home Assistant at all, because the reading contract now carries what a tap does and which icon
fits (`ReadingDescriptor.Action` and `.Icon`), so one widget serves Home Assistant, MQTT later, and the media provider
today. The decisions are in `docs/handoff/2026-10-26-home-assistant.md`.

## What Changes

- **A control widget** (`urdeck.widgets.control`, category "Control"): one tile that shows a reading's state and performs
  an action when tapped, for any provider. 1x1 (and in the dock) and 2x1.
- **Configuration with one rule for defaults**: `reading`, `action`, `label`, `icon` and `style`. An absent `action`,
  `label` or `icon` comes from the reading's description (its natural action, its label, its icon), a text is used as
  given, and `""` means none. A tile with no action is display-only and takes no taps.
- **Styles**: `switch` (default; icon, label and the state, tinted by on or off) and `dimmer` (2x1: an on/off button and a
  level bar set by tapping a position, for a light's brightness). Unknown styles fall back to `switch`.
- **Feedback for a tap**: a pending look until the state changes or the action ends, taps ignored meanwhile (no queue of
  thirty presses), a failed look for a moment when the action fails.
- **`home/` icons** in the built-in icon set (Lucide), added to the canonical keys.

Not in this change: dragging the bar (needs `IDragTarget`, a backlog item: a horizontal drag is the page swipe); icons
that change with the state; colour and colour temperature; more styles (a big button, an indicator, a numeric readout);
new theme values for on/off (the theme's accent and muted text are used).

## Capabilities

### New Capabilities

- `widget-control`: the control widget: identity, configuration and its defaults, styles and sizes, what a tap does, the
  pending and failed looks, repaint rules.

### Modified Capabilities

- `icon-sets`: the `home` group of canonical keys and the built-in set's icons for it.

## Impact

- `widgets/UrDeck.Widgets.Control` (new, GPL, SDK only), added to the host's `UrDeckPlugin` list and the preview's widget
  set; uses `ThemedIcon`, `TextLine` and the bar gauge component; draws no card and names no font.
- `src/UrDeck.Engine/Icons/Builtin/outline`: about 22 Lucide files and their manifest keys (the README's update procedure;
  the set must stay free of findings).
- `src/UrDeck.Preview`: the sample page gains control tiles on Home Assistant sample entities.
- Goldens: switch on, off, pending, failed and display-only; dimmer at three levels; in a dark and a light theme.
- Site: a widget page (configuration table generated, examples for Home Assistant and media); the catalog export lists the
  widget; site media for the new widget.
- Closes #119.
