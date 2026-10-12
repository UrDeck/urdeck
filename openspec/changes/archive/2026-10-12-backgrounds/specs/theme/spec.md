## ADDED Requirements

### Requirement: Background Palette
Every loaded theme MUST have a background palette for generated backgrounds (see backgrounds, "Palette"). A theme
MAY define it as `colors.backgroundPalette`, one to four colours; otherwise it is derived.

- The built-in themes each define one
- A theme without one gets a palette derived from its own colours: its background colour mixed towards its accent
  colour by 0%, 20%, 40% and 60%. The palette is NOT taken from the default theme, so a light theme written before the
  palette existed does not receive the dark theme's palette
- A value that is not an array of one to four valid colours is dropped with a logged warning, and the derived palette
  is used
- Transparency in a palette colour is ignored
- The palette is for backgrounds only: it is not part of what a widget receives

#### Scenario: Built-in theme has a palette
- **WHEN** a built-in theme is loaded
- **THEN** it has a background palette

#### Scenario: Older user theme
- **WHEN** a user theme written before the palette existed, with a light background colour, is loaded
- **THEN** it loads without a warning and its palette is derived from its own background and accent colours

#### Scenario: Partial user theme sets a palette
- **WHEN** a user theme sets only `colors.backgroundPalette` to two colours
- **THEN** generated backgrounds use those two colours and every other value comes from the default theme

#### Scenario: Invalid palette
- **WHEN** a user theme sets `colors.backgroundPalette` to `["#102030", "blue"]`
- **THEN** a warning is logged and the derived palette is used
