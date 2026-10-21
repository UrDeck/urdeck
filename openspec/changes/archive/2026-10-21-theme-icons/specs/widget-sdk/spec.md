## ADDED Requirements

### Requirement: Theme Carries The Icon Values
The `Theme` a widget receives MUST carry the theme's icon values as read-only data: the resolved icon set, whether the theme
names a set, the stroke, the tint and the glow, with sizes in pixels. A theme that sets none gives values that change nothing
for a widget that checks whether the theme names a set.

- The resolved set is the answer chain of "Resolution Chain" (icon-sets) behind one lookup by key, so a widget asks once and
  never knows which set answered
- The set is shared and not owned by the `Theme`: the host builds a `Theme` per surface size and disposes it when the theme
  or the display changes, and the set outlives each of them; the icon values that hold a cache (the glow) are owned by the
  `Theme` and disposed with it
- "Names a set" is true when the theme's `icons.set` was found; a widget that has its own icons for the case with no set
  (weather) checks it, and a widget that always wants a glyph (a shortcut's `theme:` icon and its fallback) does not
- The stroke is a fraction of the icon's size, 1.6 in 24 (0.0667) when the theme sets none, and the tint is `accent` when the
  theme sets none
- The glow, when the theme sets one, has a blur in pixels, an optional colour and an opacity
- The theme may be built by a widget's tests without a host: a default `Theme` names no set and its lookup answers every key
  with nothing

#### Scenario: Theme names a set
- **WHEN** a theme sets `icons.set` to `outline`
- **THEN** the widget's theme reports that it names a set and the lookup answers `weather/rain`

#### Scenario: Theme names none
- **WHEN** a theme sets no `icons.set`
- **THEN** the widget's theme reports that it names no set, and the lookup still answers `app/file` from the built-in set

#### Scenario: Surface sizes
- **WHEN** a theme is resolved at two surface sizes
- **THEN** the two `Theme` objects share one icon set, and disposing one leaves the set usable by the other

#### Scenario: Sizes are pixels
- **WHEN** a theme sets `icons.glow.blur` to 0.05 and the surface's grid cell is 275 pixels
- **THEN** the blur the theme reports is 13.75 pixels
