## MODIFIED Requirements

### Requirement: Background Scrim
The deck MUST draw a theme's `background.scrim`, a colour and an alpha from 0 to 1, between the background and the
cards, so that text keeps its contrast over a bright or busy background. The configuration MAY override how strong it is
with `background.dim`, a number from 0 to 0.7.

- It is drawn over the shader, the picture and the still background of whichever of them is shown, whoever chose it
- It is not drawn over the plain background colour, and not when the configuration has `"type": "none"`
- The colour's own transparency multiplies the alpha
- With `background.dim` set, the scrim is drawn in the theme scrim's colour (black when the theme has no scrim) with
  `dim` as its alpha; `dim` 0 draws no scrim. A value outside 0 to 0.7 is clamped; a value that is not a number logs a
  warning and is ignored
- It is a veil over the whole deck and is drawn in the background's surface; it creates no layer of its own and takes
  no pointer input

#### Scenario: Scrim over a shader
- **WHEN** a theme's scrim is black at an alpha of 0.2 and a shader is shown
- **THEN** the shader is drawn 20% darker, in the deck and in a snapshot

#### Scenario: Scrim with no background
- **WHEN** the configuration has `"type": "none"`
- **THEN** no scrim is drawn

#### Scenario: No scrim
- **WHEN** a theme has no scrim
- **THEN** the background is drawn as it is

#### Scenario: Dim set by the user
- **WHEN** the theme's scrim is black at 0.2 and the configuration has `"background": { "type": "shader", "name": "nebula", "dim": 0.5 }`
- **THEN** the shader is drawn 50% darker

#### Scenario: Dim without a theme scrim
- **WHEN** the theme has no scrim and the configuration's background has `"dim": 0.3`
- **THEN** the background is drawn 30% darker with black
