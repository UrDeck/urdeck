## MODIFIED Requirements

### Requirement: Theme Backgrounds
The deck MUST show the background that a theme chooses with its `background` group (see theme, "Theme Value Groups"),
unless the configuration chooses one:

- `default.name` is the name of a shader background that the deck shows when the configuration has no `background`
- `static` is a still background: a `gradient` (linear with an `angle`, or radial with a centre and a radius), a
  `color`, or an `image`, a picture file inside the theme's folder drawn scaled to cover the deck. A picture path that
  leaves the theme's folder is ignored with a logged warning; the limits and formats of "Image Backgrounds" apply
- A theme may define either, both or neither

What is shown is the first of these that applies:

1. The configuration's `background`, `none` included
2. The theme's `default`
3. The theme's `static`
4. The theme's background colour

and a background that fails passes to the next in the list (see "Background Failures"). A shader the theme names uses
the theme's background palette, as any shader does (see "Palette"); a shader the user chooses keeps that palette too,
unless the user gives `colors`. The theme's default and still background are the theme's choice only: they are not shown
when the user's configuration selects another background, or `none`.

- A theme's `default` name that no background has is a failed background, with the warning of "Background Failures"
- The effective background is logged once whenever it changes, with its kind, its name and where the choice came from:
  `configuration`, `theme default`, `theme still background` or `theme colour`
- A still gradient, colour or picture is drawn once, and again when the panel's size, the background or the theme
  changes; it creates no frame source and no GPU rendering context
- The theme's `motion.level` does not affect the background

#### Scenario: Theme default
- **WHEN** the theme's default is `horizon` and the configuration has no `background`
- **THEN** `horizon` is shown in the theme's palette and the log says it came from the theme default

#### Scenario: User choice beats the theme
- **WHEN** the theme's default is `horizon` and the configuration selects `plasma`
- **THEN** `plasma` is shown, in the theme's palette

#### Scenario: Still gradient
- **WHEN** a theme defines no default and its still background is a radial gradient
- **THEN** the gradient is drawn once behind the cards, and no frames are produced

#### Scenario: Still background while a default fails
- **WHEN** a theme's default shader does not compile and the theme has a still colour
- **THEN** the log has the warning and the deck shows the colour

#### Scenario: Theme changed
- **WHEN** the theme is changed from one with a default background to one without any
- **THEN** the background of the first stops and the second theme's background colour is shown, without a restart

#### Scenario: Unrelated change
- **WHEN** the configuration is saved with only a widget's setting changed
- **THEN** the background is not restarted

#### Scenario: Picture outside the theme folder
- **WHEN** a theme's still picture is `../secret.png`
- **THEN** the picture is ignored, one warning says so, and the next thing in the list is shown

#### Scenario: Reduced motion does not stop the background
- **WHEN** a theme sets `motion.level` to `off` and its default is a moving shader
- **THEN** the shader still moves
