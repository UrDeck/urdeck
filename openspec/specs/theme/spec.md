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
- Gauge: the default gauge style, one of `ring`, `bar` and `verticalBar` (see components, "Gauge Styles"), or one of
  the format 2 styles `meter`, `tach`, `marker` and `thickbar`
- Page indicator: the height of its band, the diameter of a dot, the length of the current page's pill, the space
  between marks, the colour of the current page's mark, the colour of the other marks, and the colour of the
  translucent pill behind a floating indicator (see page-navigation, "Indicator Mode")
- Press: the scale and the opacity of a card while it is pressed, each a ratio where 1 means unchanged (see
  widget-input, "Press Feedback")
- Dock: the height of the dock's band, which is also the side of a dock slot, as a fraction of a grid cell (see dock,
  "Dock Slots")

A theme MAY also use the values of format 2 (see "Format 2 Values"); a theme never has to.

A theme MUST NOT contain executable code. The built-in themes use `ring` as the default gauge style. A default gauge
style that is not one of the seven is replaced by the default theme's value with a logged warning. A widget that asks
for the theme's default gauge style receives it, whichever of the seven it is. A theme that omits a page indicator value, a press value or the dock height takes the default theme's value
for it, so themes written before these existed keep working. A press scale outside 0.5 to 1 or a press opacity outside
0.1 to 1 is replaced by the default theme's value with a logged warning, and so is a dock height outside 0.2 to 1. The
built-in themes use a dock height of 0.71, which leaves the 1100x3840 panel its 13 rows beside the indicator's band.

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

#### Scenario: Format 2 gauge style
- **WHEN** a user theme sets the default gauge style to `tach`
- **THEN** the theme loads without a warning and keeps `tach`, and a widget that asks for the default gauge style
  receives `tach`

#### Scenario: Theme restyles the indicator
- **WHEN** a user theme sets a longer pill and a different colour for the current page's mark
- **THEN** the indicator is drawn with that pill length and colour

#### Scenario: Older theme without indicator values
- **WHEN** a user theme written before the indicator existed is loaded
- **THEN** it loads without a warning and the indicator uses the default theme's values

#### Scenario: Theme restyles the press
- **WHEN** a user theme sets the press scale to 0.9 and the press opacity to 1
- **THEN** a pressed card shrinks to nine tenths of its size and does not dim

#### Scenario: Theme switches press feedback off
- **WHEN** a user theme sets the press scale and the press opacity to 1
- **THEN** a pressed card does not change

#### Scenario: Older theme without press values
- **WHEN** a user theme written before press feedback existed is loaded
- **THEN** it loads without a warning and a pressed card uses the default theme's values

#### Scenario: Press value out of range
- **WHEN** a user theme sets the press scale to 2
- **THEN** the theme loads with the default theme's press scale and a warning is logged

#### Scenario: Theme resizes the dock
- **WHEN** a user theme sets the dock height to 0.5
- **THEN** the dock's band and its slots are half a grid cell high

#### Scenario: Older theme without a dock height
- **WHEN** a user theme written before the dock existed is loaded
- **THEN** it loads without a warning and the dock uses the default theme's height

#### Scenario: Dock height out of range
- **WHEN** a user theme sets the dock height to 3
- **THEN** the theme loads with the default theme's dock height and a warning is logged

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
The product MUST ship these built-in themes: `default-dark`, `default-light`, `glass`, `carbon`, `paper`, `prism`,
`obsidian`, `ember`, `blueprint` and `pop`. `default-dark` is the default theme. Built-in themes
MUST be available regardless of the contents of the user themes folder.

- A built-in theme MAY name font files that ship with the product; they are found as a user theme's files are found in
  its folder, and with no themes folder at all
- Every built-in theme MUST load without a font or weight warning
- A built-in theme in format 2 MUST set every colour of its own, so nothing of `default-dark`'s palette shows through
  (in particular in the light themes `paper` and `pop`)
- Every built-in theme MUST validate with no error and no warning (`urdeck-cli theme validate`)

#### Scenario: Themes folder is missing
- **WHEN** the user themes folder does not exist
- **THEN** every built-in theme can still be selected

#### Scenario: Carbon in its own fonts
- **WHEN** `carbon` is selected on a machine with neither Instrument Sans nor JetBrains Mono installed
- **THEN** values, units and labels are drawn in JetBrains Mono, body text in Instrument Sans, and nothing is logged
  about fonts or weights

#### Scenario: Paper in its own fonts
- **WHEN** `paper` is selected on a machine with neither font installed
- **THEN** values and body text are drawn in Instrument Sans, units and labels in JetBrains Mono, and nothing is
  logged about fonts or weights

#### Scenario: Paper is light throughout
- **WHEN** `paper` is loaded
- **THEN** its background, card fill, text and muted text are Paper's own colours, none taken from `default-dark`

#### Scenario: Prism in its own font
- **WHEN** `prism` is selected on a machine without Bricolage Grotesque installed
- **THEN** values, units, labels and body text are drawn in Bricolage Grotesque, and nothing is logged about fonts or
  weights

#### Scenario: Obsidian in its own fonts
- **WHEN** `obsidian` is selected on a machine with neither Bodoni Moda nor Jost installed
- **THEN** values are drawn in Bodoni Moda, units, labels and body text in Jost, and nothing is logged about fonts or
  weights

#### Scenario: Obsidian is dark throughout
- **WHEN** `obsidian` is loaded
- **THEN** its background, card fill, text and muted text are Obsidian's own colours, none taken from `default-dark`

#### Scenario: A user theme named like a new built-in
- **WHEN** the themes folder holds a theme folder named `carbon`
- **THEN** selecting `carbon` loads the user's theme, as for any built-in name

#### Scenario: A user theme named like Prism
- **WHEN** the themes folder holds a theme folder named `prism`
- **THEN** selecting `prism` loads the user's theme, as for any built-in name

#### Scenario: New built-ins validate clean
- **WHEN** `prism` and `obsidian` are validated
- **THEN** no error and no warning is reported

#### Scenario: Ember in its own font
- **WHEN** `ember` is selected on a machine without Barlow Condensed installed
- **THEN** every text role is drawn in Barlow Condensed Bold, and nothing is logged about fonts or weights

#### Scenario: Blueprint in its own fonts
- **WHEN** `blueprint` is selected on a machine without IBM Plex Mono installed
- **THEN** values, units, labels, body text and titles are drawn in IBM Plex Mono Regular (title weight 400: titles have no
  font of their own), and nothing is logged about fonts or weights

#### Scenario: Ember's tick colour
- **WHEN** `ember` is loaded
- **THEN** its gauge tick colour is a faint white (alpha 0x1c), not an opaque colour

#### Scenario: Motion levels
- **WHEN** `ember` and `blueprint` are loaded
- **THEN** their motion levels are `full` and `reduced` respectively

#### Scenario: A user theme named like Ember
- **WHEN** the themes folder holds a theme folder named `ember`
- **THEN** selecting `ember` loads the user's theme, as for any built-in name

#### Scenario: Pop in its own font
- **WHEN** `pop` is selected on a machine without Archivo installed
- **THEN** values (weight 900), units, labels and body text are all drawn in the bundled Archivo, and nothing is
  logged about fonts or weights

#### Scenario: Pop is light throughout
- **WHEN** `pop` is loaded
- **THEN** every v1 colour (background, card fill, card border, text, muted text, accent, accent dim, good, warning,
  critical) is the value in Pop's own file, none taken from `default-dark`

#### Scenario: Pop's tinted cards are readable
- **WHEN** `pop` is validated
- **THEN** text on each of its five card tints meets the contrast rule and no finding is reported

#### Scenario: A user theme named like Pop
- **WHEN** the themes folder holds a theme folder named `pop`
- **THEN** selecting `pop` loads the user's theme, as for any built-in name

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

### Requirement: Theming Does Not Cost Idle Resources
Theme handling MUST NOT introduce recurring work. Fonts are loaded when a theme becomes active, not on each paint, and
a theme change causes a single repaint of each widget.

#### Scenario: Idle dashboard with a theme
- **WHEN** the dashboard is idle with a theme active
- **THEN** no work occurs beyond each widget's own refresh timer

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

### Requirement: Theme Format Version
The host MUST load themes of format 1 and format 2 with the same code. A theme MAY declare `"format": 1` or
`"format": 2`; a theme without it is format 1, and a theme need not declare `format` to use format 2 values.

- A `format` greater than the highest the host supports logs one warning naming the theme, the declared and the
  supported format, and the theme still loads
- A `format` that is not a whole number of at least 1 is dropped with a logged warning
- A key the host does not know, at any depth, is ignored without a warning at load time, so a theme written for a newer
  host shows its known values on an older one

#### Scenario: Format 2 theme
- **WHEN** a theme declares `"format": 2`
- **THEN** it loads, and its format 2 values are available on the loaded theme

#### Scenario: Newer format than the host knows
- **WHEN** a theme declares `"format": 3`
- **THEN** it loads with the values the host knows, and one warning says the theme declares format 3 and the host
  supports 2

#### Scenario: Unknown key
- **WHEN** a theme contains a key that is not in the schema, such as one the product has set aside for later
- **THEN** the key is ignored and no warning is logged at load

#### Scenario: Format is not required
- **WHEN** a theme uses `card.shadow` and has no `format`
- **THEN** the shadow is read as a format 2 value

### Requirement: Format 2 Values Are Optional And Additive
Every value of format 2 MUST be optional, and an absent value MUST mean that the feature is off. A theme written before
format 2 MUST load with the same values, the same warnings and the same drawing as before.

- Every format 1 value keeps its name, range and meaning
- A value whose absence means "off" stays absent in the loaded theme: merging a theme over the default theme never
  creates one
- These are never inherited from the default theme, so a theme that does not define them does not get them: the
  background palette, `colors.ramp`, `colors.cardTints`, `pattern`, `background`, `icons`, and every card chrome,
  typography effect and gauge modifier group of format 2
- `meta` is never inherited either: a theme without it has none
- The values that are inherited are the format 1 values and any format 2 value the default theme defines; the default
  theme defines none of the groups above

#### Scenario: Theme without format 2 values
- **WHEN** a user theme written before format 2 is loaded
- **THEN** it loads without a warning and none of the format 2 groups are present on it

#### Scenario: Built-in themes are unchanged
- **WHEN** `default-dark`, `default-light` and `glass` are loaded
- **THEN** they load without a warning, define none of the groups that are never inherited, and everything drawn
  with them is as before

#### Scenario: Partial group
- **WHEN** a theme sets `card.shadow` with only `blur` and `color`
- **THEN** the shadow's offset and spread take their documented defaults, and no other group appears

### Requirement: Theme Metadata
The host MUST read an optional `meta` group that describes a theme and MUST NOT let it affect drawing: `name`,
`description`, `author`, `license`, `version`, `mode` (`dark` or `light`), `tags`, `pairsWith.backgrounds` (background
names, best first) and `samples`.

- A theme without `meta.name` is shown by its folder name
- A value of the wrong type is dropped with a logged warning; a name in `pairsWith.backgrounds` is not checked against
  the backgrounds that exist
- `mode` is informational: it does not change a colour and does not switch themes with the system setting

#### Scenario: Described theme
- **WHEN** a theme sets `meta.name` to "Carbon" and `meta.mode` to `dark`
- **THEN** the loaded theme reports that name and mode

#### Scenario: No metadata
- **WHEN** a theme in the folder `mine` has no `meta`
- **THEN** its display name is `mine` and it has no mode, tags or pairings

#### Scenario: Unknown mode
- **WHEN** a theme sets `meta.mode` to `sepia`
- **THEN** the value is dropped with a logged warning and the theme loads

### Requirement: Format 2 Values
The host MUST accept the following groups in a theme, each documented key by key, with its range and default, in the
theme reference and in the published schema. A value is read, checked, merged and kept on the loaded theme by this
capability; what is drawn with it is specified by the capabilities that draw it.

- Colours: `accent2`, `ramp` (two to five colours), `cardTints` (one to eight colours), `ink`
- Card chrome: `shape` (`round` or `chamfer`), `cut`, `cutCorners`, `fillGradient`, `borderStyle` (`solid`, `dashed`
  or `none`), `borderDash`, `borderGradient`, `borderAnimate`, `cornerMarks` (`bracket` or `tick`), `shadow`, `glow`,
  `highlight`, `sheen`, `accentStrip`, `tint` (`none` or `cycle`), and `compact`, a partial override of the card values
  for cards of one grid cell or less and for dock slots
- Page: `pattern` (`dots`, `grid`, `hatch` or `scanlines`, with its spacing, thickness, colour and, for a grid, the
  heavier lines)
- Section parts: `divider`, `chip` and `well`
- Typography: per-role fonts for `value`, `unit` and `label`, `valueScale`, `case`, `tracking`, `slant`, `valueFill`
  and `valueGlow`
- Gauge: the styles `meter`, `tach`, `marker` and `thickbar`, `sweep`, `segments`, `outline`, `ticks`, `scale`,
  `redline`, `gradient`, `glow`, `marker`, `crosshair`, `track`, `labelPosition`, `layout` (`auto` or `stacked`),
  `colorBy`, `valueTint`, `sparkline` and `status`
- Icons: `set`, `stroke`, `tint` (`accent`, `accent2`, `text`, `ramp`, `none` or a colour) and `glow`; there is no
  `animated` value, icons from a set are still pictures
- Background: `default` (a background's name), `static` (a gradient, a colour or a picture file) and `scrim`
- Motion: `level`, the ceiling for animation a theme drives (see `motion`)

The product has set some values aside for a later change; they are listed with their definitions in
`docs/design/theme-format-2-deferred-keys.md` and are not part of this requirement.

#### Scenario: Card chrome values
- **WHEN** a theme sets `card.shape` to `chamfer`, `card.cut` to 0.14 and `card.cutCorners` to `["tr", "bl"]`
- **THEN** the loaded theme has those values, and the cards are drawn with them as `widget-card` specifies

#### Scenario: Compact override
- **WHEN** a theme sets `card.compact.shape` to `circle`
- **THEN** the loaded theme has a compact override with that shape and the other compact values absent

#### Scenario: Icon values
- **WHEN** a theme sets `icons.set` to `outline`, `icons.stroke` to 0.066 and `icons.tint` to `accent`
- **THEN** the loaded theme has those values and the weather and shortcut cards draw with them as `icon-sets` and
  `widget-weather` specify

#### Scenario: A value that was removed
- **WHEN** a theme sets `icons.animated`
- **THEN** the key is reported as an unknown key, as any other unknown key is, and nothing else changes

#### Scenario: Motion ceiling
- **WHEN** a theme sets `motion.level` to `reduced`
- **THEN** the value is kept, no warning is logged, and it caps the deck's motion as `motion` specifies

### Requirement: Format 2 Validation
An invalid format 2 value MUST be handled as a format 1 value is: it is cleared, the loaded theme takes the default
(which for an optional value is "absent"), and one warning names the theme, the key and the value.

- A number outside its range, a colour that does not parse and a value that is not one of an enumeration's names are
  invalid; enumeration names are read case-insensitively
- An array outside its length limits, or one with an invalid element, is dropped whole
- A value of the wrong type is invalid
- A time is at least 1 second
- Rules between values are checked after each value on its own. A rule that fails clears or downgrades only what it
  names and logs one warning:
  - `borderStyle`, `borderGradient` or `borderAnimate` with a border width of 0: warned, nothing has an effect
  - `borderAnimate` with `mode` `spin` and a `borderGradient` whose kind is not `conic`: the animation is cleared
  - `pattern` marks closer than three pixels at the 1100 pixel panel width (`spacing` / `thickness` below 3): warned
  - `card.cornerMarks.inset` below 0 with `card.gap` smaller than its size: warned
  - `card.shape` `chamfer` with `card.compact.shape` `circle`: allowed; the circle applies to compact cards only
- A theme that turns on more than one continuous animation is allowed and logged at information level

#### Scenario: Out-of-range value
- **WHEN** a theme sets `card.shadow.blur` to 5
- **THEN** the theme loads with no `blur` for the shadow and one warning names the theme, `card.shadow.blur` and the
  range

#### Scenario: One bad colour in an array
- **WHEN** a theme sets `colors.cardTints` to three colours and one is not a colour
- **THEN** `cardTints` is dropped whole, one warning is logged, and the other values load

#### Scenario: Enumeration in another case
- **WHEN** a theme sets `card.borderStyle` to `Dashed`
- **THEN** it is read as `dashed` without a warning

#### Scenario: Spinning border without a conic gradient
- **WHEN** a theme sets `card.borderAnimate.mode` to `spin` and `card.borderGradient.kind` to `linear`
- **THEN** the animation is cleared, the gradient is kept, and one warning says so

#### Scenario: Border style with no border
- **WHEN** a theme sets `card.borderStyle` to `dashed` and leaves the border width at 0
- **THEN** the theme loads and one warning says the style has no effect with a border width of 0

#### Scenario: Every value invalid
- **WHEN** a theme sets every format 2 value to something invalid
- **THEN** it loads with the default theme's values, no format 2 group is present, and there is one warning per key

### Requirement: Published Theme Schema
The product MUST publish the theme's format 2 schema as a JSON Schema in the repository, `docs/schemas/v2/theme.json`,
and a theme MAY point at it with `$schema` for completion in an editor. The schema MUST describe exactly the values
the theme model reads, and a test MUST fail when the two differ.

- The schema lists each key with its type, range, default and a one-line description
- A key the product has set aside for later is not in the schema
- `docs/themes.md` and the schema describe the same keys

#### Scenario: Draft themes satisfy the schema
- **WHEN** each drafted theme is validated against the schema
- **THEN** it is valid

#### Scenario: Schema and model drift
- **WHEN** the model gains a value that the schema does not describe
- **THEN** the schema test fails and names the value

#### Scenario: Drafted themes load clean
- **WHEN** each of the eight drafted themes is loaded
- **THEN** it loads with no warning

### Requirement: Built-in Theme Metadata
Every built-in theme MUST carry a `meta` group with at least a display name, a description and a mode, so that a
catalog can list it without drawing it.

- `default-dark` is named "Default Dark" with mode `dark`, `default-light` is "Default Light" with mode `light`, and
  `glass` is "Glass" with mode `dark`
- `carbon` is named "Carbon" with mode `dark`, and `paper` is "Paper" with mode `light`; each lists the backgrounds it
  pairs with, best first, and names `contour` as its default background, with a still colour as its static background
- `prism` is named "Prism" with mode `dark`, and `obsidian` is "Obsidian" with mode `dark`; each lists the backgrounds it
  pairs with, best first, names one of them as its default background, and has a still gradient as its static background
- Prism pairs with `nebula`, `vortex`, `horizon`, `spectrum` and `ripple`; Obsidian with `nebula`, `vortex` and
  `warpgate` (the panel look may reorder or change these)
- `ember` is named "Ember" with mode `dark`, pairs with `embers`, `warpgate` and `equalizer` (best first), names `embers`
  as its default background and a gradient as its static background; `blueprint` is named "Blueprint" with mode `dark`,
  pairs with `plotter`, `contour` and `streams`, names `plotter` as its default background and a colour as its static
  background
- `pop` is named "Pop" with mode `light`; it pairs with `shapes` then `ripple`, names `shapes` as its default
  background, and has a still colour as its static background
- A built-in theme may carry pairings and tags; the three that exist before the format 2 themes define no background and
  no default, so adding `meta` changes nothing that is drawn

#### Scenario: Built-ins have metadata
- **WHEN** every built-in theme is loaded
- **THEN** each has a display name, a description and a mode

#### Scenario: Nothing drawn changes
- **WHEN** a snapshot of `default-dark`, `default-light`, `glass`, `carbon` and `paper` is taken before and after this
  change
- **THEN** the pictures are identical

#### Scenario: New built-ins pair with backgrounds
- **WHEN** a catalog lists the built-in themes and the built-in backgrounds
- **THEN** `carbon` and `paper` each recommend `contour` first, and every background they name exists among the
  built-in backgrounds

#### Scenario: Prism and Obsidian pair with backgrounds
- **WHEN** a catalog lists the built-in themes and the built-in backgrounds
- **THEN** `prism` and `obsidian` each recommend their first paired background first, and every background they name
  exists among the built-in backgrounds

#### Scenario: Default background of a new built-in
- **WHEN** `carbon` is selected and the configuration sets no `background`
- **THEN** the page draws the `contour` background in Carbon's palette, and with shaders unavailable it draws Carbon's
  static colour

#### Scenario: Default background of Prism
- **WHEN** `prism` is selected and the configuration sets no `background`
- **THEN** the page draws the theme's default background in Prism's palette, and with shaders unavailable it draws
  Prism's static gradient

#### Scenario: Ember and Blueprint pair with backgrounds
- **WHEN** a catalog lists the built-in themes and the built-in backgrounds
- **THEN** `ember` recommends `embers` first, `blueprint` recommends `plotter` first, and every background they name
  exists among the built-in backgrounds

#### Scenario: Default background of Ember
- **WHEN** `ember` is selected and the configuration sets no `background`
- **THEN** the page draws the `embers` background in Ember's palette, and with shaders unavailable it draws Ember's
  static gradient

#### Scenario: Default background of Blueprint
- **WHEN** `blueprint` is selected and the configuration sets no `background`
- **THEN** the page draws the `plotter` background, and with shaders unavailable it draws Blueprint's static colour

#### Scenario: Pop pairs with backgrounds
- **WHEN** a catalog lists the built-in themes and the built-in backgrounds
- **THEN** `pop` recommends `shapes` first, and every background it names exists among the built-in backgrounds

#### Scenario: Default background of Pop
- **WHEN** `pop` is selected and the configuration sets no `background`
- **THEN** the page draws the `shapes` background in Pop's palette, and with shaders unavailable it draws Pop's static
  colour

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

### Requirement: Findings Are Structured
Everything the deck reports about a theme when it loads one MUST be available as a finding as well as a log line:

- A finding has a code of the `theme-lint` capability, a severity, the key it is about when it is about one, and the
  message
- The log line for a finding is the line the deck has always written for it, at the level it has always had
- A finding is reported once for each load of a theme, as the log line is
- Findings that the deck acts on (a value cleared, an animation switched off) are acted on as before; reporting them in
  another form changes nothing the deck draws

#### Scenario: Log unchanged
- **WHEN** a theme with an out-of-range value is loaded
- **THEN** the deck's log has the same single warning as before, and the finding of the same message exists

#### Scenario: Once per load
- **WHEN** a theme is resolved for a page, a dock and a display change after one load
- **THEN** each finding is reported once

#### Scenario: Drawing unchanged
- **WHEN** the built-in themes are drawn
- **THEN** the pictures are those of the reference pictures

### Requirement: Rigline Built-in Theme
The product MUST ship `rigline` as a built-in theme, in addition to the other built-in themes, available regardless of the
contents of the user themes folder. It is named "Rigline" with mode `dark`, lists the backgrounds it pairs with (best first,
all existing built-in backgrounds), names `horizon` as its default background, and has a still gradient as its static
background. It bundles its font file, Chakra Petch Bold, and draws values, units, labels, titles and body text from it.

- The theme MUST load without a font or weight warning (each role names a weight the file has)
- The theme MUST validate with no finding at error or warning level under `urdeck-cli theme validate`
- A user theme named `rigline` takes precedence over the built-in, as for any built-in name

#### Scenario: Rigline in its own font
- **WHEN** `rigline` is selected on a machine without Chakra Petch installed
- **THEN** every text role is drawn in Chakra Petch Bold, slanted by the theme's `slant`, and nothing is logged about fonts
  or weights

#### Scenario: Metadata and pairings
- **WHEN** a catalog lists the built-in themes and the built-in backgrounds
- **THEN** `rigline` is listed with its name and mode, recommends `horizon` first, and every background it names exists

#### Scenario: Default background
- **WHEN** `rigline` is selected and the configuration sets no `background`
- **THEN** the page draws `horizon` in Rigline's palette, and with shaders unavailable it draws Rigline's static gradient

#### Scenario: Validates clean
- **WHEN** `urdeck-cli theme validate` runs on the built-in theme
- **THEN** it reports no error and no warning, including no contrast finding

#### Scenario: Version 1 themes unchanged
- **WHEN** snapshots of `default-dark`, `default-light` and `glass` are taken before and after this change
- **THEN** the pictures are identical

