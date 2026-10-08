# Theme Specification

## Purpose

Defines what a theme is, how themes are packaged, discovered and selected, and how theme values reach widgets, so that
the look of every widget on a page comes from one shared, user-selectable set of values.
## Requirements
### Requirement: Theme Contents
A theme MUST define all of the following values:

- Colours, each with transparency: background, card fill, card border, text, muted text, accent, dimmed accent, good,
  warning and critical
- Card: corner radius, border width, gap between cards and inner padding
- Typography: a font family, a weight for each text role (value, unit, label, body, title), a size for each of the
  three small-text steps (label, body, title), and the ratio of a unit's size to its value's size
- Stroke: line thickness as a ratio of the drawn element's size, and round or square line ends
- Gauge: the default gauge style, one of `ring`, `bar` and `verticalBar` (see components, "Gauge Styles")

A theme MUST NOT contain executable code. The built-in themes use `ring` as the default gauge style. A default gauge
style that is not one of the three is replaced by the default theme's value with a logged warning.

#### Scenario: Theme provides every value
- **WHEN** a theme is loaded
- **THEN** every value listed above is defined for it

#### Scenario: Colour carries transparency
- **WHEN** a theme sets the card fill to a colour with 80% opacity
- **THEN** cards are drawn with that opacity over the window background

#### Scenario: Theme changes the default gauge style
- **WHEN** a user theme sets the default gauge style to `bar`
- **THEN** a widget that asks for the theme's default gauge style receives `bar`

#### Scenario: Plain is not a default gauge style
- **WHEN** a user theme sets the default gauge style to `plain`
- **THEN** the theme loads with `ring` and a warning is logged

### Requirement: Resolution-Independent Sizes
All theme sizes other than ratios MUST be stored as fractions of the grid cell size. Widgets MUST receive theme sizes
as finished pixel values for the surface they draw on and MUST NOT need the cell size, the display resolution or the
display scaling to use them.

#### Scenario: Same theme on a wider display
- **WHEN** the same theme is used on a display 1100 pixels wide and on one 2200 pixels wide
- **THEN** the corner radius, gap, padding and small-text sizes in pixels are twice as large on the wider display

#### Scenario: Widget reads a size
- **WHEN** a widget reads the theme's padding or label size during render
- **THEN** it receives a pixel value it can use directly on its surface

### Requirement: Theme Folder Format
A theme MUST be packaged as a folder whose name is the theme's name, containing one settings file and, optionally, font
files that the settings file refers to by file name. A font reference that points outside the theme's folder MUST be
rejected. Built-in themes and user themes MUST use the same format.

#### Scenario: Theme with a bundled font
- **WHEN** a theme folder contains a font file and its settings name that file as the font
- **THEN** text drawn under that theme uses the bundled font without the font being installed on the system

#### Scenario: Font reference escapes the folder
- **WHEN** a theme's settings refer to a font file outside the theme's own folder
- **THEN** the reference is ignored, a warning is logged and the default theme's font is used

### Requirement: Built-in Themes
The product MUST ship at least two built-in themes, `default-dark` and `default-light`, that differ in colour.
`default-dark` is the default theme. Built-in themes MUST be available regardless of the contents of the user themes
folder.

#### Scenario: Themes folder is missing
- **WHEN** the user themes folder does not exist
- **THEN** both built-in themes can still be selected

### Requirement: User Themes
The host MUST discover user themes in a `themes` folder next to the executable.

- A user theme MAY omit values; omitted values are taken from the default theme
- A user theme with the same name as a built-in theme takes precedence over it
- A user theme whose settings file cannot be read is skipped with a logged warning naming the theme, and does not
  affect other themes
- An individual value that is invalid (for example an unparseable colour) is replaced by the default theme's value
  with a logged warning

#### Scenario: Partial user theme
- **WHEN** a user theme defines only the accent colour and the corner radius
- **THEN** it can be selected, and every other value matches the default theme

#### Scenario: Broken theme file
- **WHEN** a user theme's settings file contains invalid content
- **THEN** that theme is not available, a warning is logged, and other themes remain selectable

#### Scenario: Invalid single value
- **WHEN** a user theme sets the text colour to a string that is not a colour
- **THEN** the theme loads, the default theme's text colour is used, and a warning is logged

### Requirement: Theme Selection
The active theme MUST be selected by the `theme` key in the configuration and applied without restarting.

- User themes are re-read whenever the configuration is reloaded
- An unknown theme name selects the default theme and logs a warning naming the missing theme
- Changing the theme repaints every widget once and recomputes the layout if the gap changed

#### Scenario: Switch theme at runtime
- **WHEN** the user changes `theme` from `default-dark` to `default-light` and saves the config
- **THEN** the window background and all widgets show the light theme after the config reload, without a restart

#### Scenario: Unknown theme name
- **WHEN** the config names a theme that does not exist
- **THEN** the default theme is used and a warning naming the missing theme is logged

#### Scenario: Edited user theme
- **WHEN** the user edits the active user theme's settings and then saves the config file
- **THEN** the edited values are applied after the config reload

### Requirement: Fonts
A theme's font family MUST be either the name of an installed font or a font file bundled in the theme folder. The
weight of each text role MUST be honoured when the font supports it. When the font cannot be loaded, the default
theme's font is used and a warning is logged; when that also fails, the system default font is used.

#### Scenario: Weight per role
- **WHEN** a theme sets the value role to a heavy weight and the label role to a regular weight
- **THEN** values are drawn visibly heavier than labels

#### Scenario: Font is unavailable
- **WHEN** a theme names an installed font that is not present on the machine
- **THEN** text is drawn with the default theme's font and a warning is logged

### Requirement: Theming Does Not Cost Idle Resources
Theme handling MUST NOT introduce recurring work. Fonts are loaded when a theme becomes active, not on each paint, and
a theme change causes a single repaint of each widget.

#### Scenario: Idle dashboard with a theme
- **WHEN** the dashboard is idle with a theme active
- **THEN** no work occurs beyond each widget's own refresh timer

