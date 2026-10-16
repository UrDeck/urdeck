## MODIFIED Requirements

### Requirement: Fonts
A theme's font family MUST be either the name of an installed font or a font file bundled in the theme folder. The
weight of each text role MUST be honoured when the font supports it. A theme MAY name a different font for the value,
unit and label roles (`typography.fonts`); a role without one uses `typography.font`. When a font cannot be loaded, the
role falls back along this chain, one step at a time: the role's font, `typography.font`, the default theme's font, the
system default font. A font that cannot be loaded is warned about once for each loaded theme, naming the theme, the
role and the font. A font file whose path leaves the theme folder is refused for every role, as for `typography.font`.

A weight a font cannot make MUST be warned about once for each role of each loaded theme: a weight outside the range of
a variable font's weight axis, which is drawn at the nearest end of the range, and a weight that a static font file does
not have (the file's own weight is drawn). A font installed on the machine, rather than a file, is not checked. A theme
whose weights the font can make logs nothing.

#### Scenario: Weight per role
- **WHEN** a theme sets the value role to a heavy weight and the label role to a regular weight
- **THEN** values are drawn visibly heavier than labels

#### Scenario: Font is unavailable
- **WHEN** a theme names an installed font that is not present on the machine
- **THEN** text is drawn with the default theme's font and a warning is logged

#### Scenario: Different font for the values
- **WHEN** a theme sets `typography.font` to one family and `typography.fonts.value` to a file in its folder
- **THEN** values are drawn in the file's font and the unit, label, body and title in the family

#### Scenario: A role font is missing
- **WHEN** a theme sets `typography.fonts.label` to a file that does not exist
- **THEN** labels are drawn in `typography.font`, the other roles are unaffected, and one warning names the theme, the
  label role and the file, however many times the theme is resolved

#### Scenario: Role font leaves the theme folder
- **WHEN** a theme sets `typography.fonts.unit` to `../other/font.ttf`
- **THEN** the unit is drawn in `typography.font` and a warning says the path is not allowed

#### Scenario: Weight beyond the font's range
- **WHEN** a theme sets the label weight to 300 and the font's weight axis runs from 400 to 700
- **THEN** labels are drawn at weight 400 and one warning names the theme, the label role, the weight and the range

#### Scenario: Weight a static font does not have
- **WHEN** a theme sets the body weight to 500 and its font is a single file of weight 700
- **THEN** body text is drawn at that file's weight and one warning says the weight has no effect

#### Scenario: Version 1 themes are unchanged
- **WHEN** `default-dark`, `default-light` or `glass` is loaded and resolved
- **THEN** no new warning is logged and every typeface is the one it was before

## ADDED Requirements

### Requirement: Optical Size
A theme MAY set `typography.opticalSize` to `off` or `auto`; absent means `off`. The host MUST leave a font's optical size alone with `off`: a font's optical size axis
(`opsz`) stays at the font's own default, as it always has. With `auto`, for each role whose font has an `opsz` axis:

- the value and unit roles use the top of the axis, the display cut, because numerals are drawn large
- the label, body and title roles use the role's size in pixels on the surface being drawn, limited to the axis range

A font with no `opsz` axis is unaffected and nothing is logged. An unknown value is invalid and handled as any
other invalid format 2 value.

#### Scenario: Off keeps the font as it was
- **WHEN** a theme does not set `typography.opticalSize` and its font has an optical size axis
- **THEN** the axis is at the font's default and the text is drawn exactly as before this requirement

#### Scenario: Numerals take the display cut
- **WHEN** a theme sets `opticalSize` to `auto` and its value font has an `opsz` axis from 6 to 96
- **THEN** the value role is drawn at an optical size of 96

#### Scenario: Labels follow their size
- **WHEN** a theme sets `opticalSize` to `auto` and its label size resolves to 30 pixels on a font whose `opsz` axis runs from 12 to 96
- **THEN** labels are drawn at an optical size of 30, and at 12 when the size is 10 pixels

#### Scenario: A font without the axis
- **WHEN** a theme sets `opticalSize` to `auto` and its font has no `opsz` axis
- **THEN** the font is drawn as without the key and no warning is logged

#### Scenario: Unknown value
- **WHEN** a theme sets `typography.opticalSize` to `large`
- **THEN** the key is cleared as `off` and one warning names the theme, the key and the value
