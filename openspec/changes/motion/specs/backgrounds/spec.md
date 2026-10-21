## ADDED Requirements

### Requirement: Reduced Motion Stills The Background
When the user's choice of motion level (the configuration's `motion` value, or Windows' animation setting when it is
absent; see `motion`) is `reduced` or `off`, the deck MUST draw a moving shader background still, exactly as it draws that
background with `speed` 0: once, with no frame source.

- The theme's `motion.level` is not part of this decision: a theme's ceiling never stops the background (see "Theme
  Backgrounds")
- The background is chosen as before (the same candidate, palette, parameters, scale and scrim); only its motion stops
- When the user's choice returns to `full`, the background moves again with time restarted at 0
- A snapshot uses the configuration's `motion` value and ignores Windows' setting, so a snapshot is the same on every
  machine

#### Scenario: Windows animations off
- **WHEN** the configuration has no `motion`, Windows' animation effects are off and the background is a moving shader
- **THEN** the shader is drawn once and no background frames are produced

#### Scenario: User wants motion
- **WHEN** the configuration has `"motion": "full"`, Windows' animation effects are off and the background is a moving
  shader
- **THEN** the shader moves

#### Scenario: Theme ceiling only
- **WHEN** the configuration has `"motion": "full"`, the theme sets `motion.level` to `off` and its default background is
  a moving shader
- **THEN** the shader moves, and the theme's animated border (if any) is still

#### Scenario: Snapshot with reduced motion
- **WHEN** a snapshot is taken with `"motion": "off"` in the configuration and a moving shader background
- **THEN** the background in the picture is the one `speed` 0 draws
