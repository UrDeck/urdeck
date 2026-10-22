## MODIFIED Requirements

### Requirement: Built-in Themes
The product MUST ship these built-in themes: `default-dark`, `default-light`, `glass`, `carbon`, `paper`, `ember` and
`blueprint`. `default-dark` is the default theme. Built-in themes MUST be available regardless of the contents of the user
themes folder.

- A built-in theme MAY name font files that ship with the product; they are found as a user theme's files are found in
  its folder, and with no themes folder at all
- Every built-in theme MUST load without a font or weight warning
- A built-in theme in format 2 MUST set every colour of its own, so nothing of `default-dark`'s palette shows through
  (in particular in the light theme `paper`)
- Every built-in theme MUST validate under `urdeck-cli theme validate` with no error and no contrast finding

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

#### Scenario: Ember in its own font
- **WHEN** `ember` is selected on a machine without Barlow Condensed installed
- **THEN** every text role is drawn in Barlow Condensed Bold, and nothing is logged about fonts or weights

#### Scenario: Blueprint in its own fonts
- **WHEN** `blueprint` is selected on a machine without IBM Plex Mono installed
- **THEN** values, units, labels and body text are drawn in IBM Plex Mono Regular, titles in IBM Plex Mono Medium, and
  nothing is logged about fonts or weights

#### Scenario: Ember's tick colour
- **WHEN** `ember` is loaded
- **THEN** its gauge tick colour is a faint white (alpha 0x1c), not an opaque colour

#### Scenario: Motion levels
- **WHEN** `ember` and `blueprint` are loaded
- **THEN** their motion levels are `full` and `reduced` respectively

#### Scenario: A user theme named like a new built-in
- **WHEN** the themes folder holds a theme folder named `ember`
- **THEN** selecting `ember` loads the user's theme, as for any built-in name

### Requirement: Built-in Theme Metadata
Every built-in theme MUST carry a `meta` group with at least a display name, a description and a mode, so that a
catalog can list it without drawing it.

- `default-dark` is named "Default Dark" with mode `dark`, `default-light` is "Default Light" with mode `light`, and
  `glass` is "Glass" with mode `dark`
- `carbon` is named "Carbon" with mode `dark`, and `paper` is "Paper" with mode `light`; each lists the backgrounds it
  pairs with, best first, and names `contour` as its default background, with a still colour as its static background
- `ember` is named "Ember" with mode `dark`, pairs with `embers`, `warpgate` and `equalizer` (best first), names `embers`
  as its default background and a gradient as its static background; `blueprint` is named "Blueprint" with mode `dark`,
  pairs with `plotter`, `contour` and `streams`, names `plotter` as its default background and a colour as its static
  background
- A built-in theme may carry pairings and tags; the three that exist before the format 2 themes define no background and
  no default, so adding `meta` changes nothing that is drawn

#### Scenario: Built-ins have metadata
- **WHEN** every built-in theme is loaded
- **THEN** each has a display name, a description and a mode

#### Scenario: Nothing drawn changes
- **WHEN** a snapshot of `default-dark`, `default-light` and `glass` is taken before and after this change
- **THEN** the pictures are identical

#### Scenario: New built-ins pair with backgrounds
- **WHEN** a catalog lists the built-in themes and the built-in backgrounds
- **THEN** `ember` recommends `embers` first and `blueprint` recommends `plotter` first, and every background each names
  exists among the built-in backgrounds

#### Scenario: Default background of Ember
- **WHEN** `ember` is selected and the configuration sets no `background`
- **THEN** the page draws the `embers` background in Ember's palette, and with shaders unavailable it draws Ember's
  static gradient

#### Scenario: Default background of Blueprint
- **WHEN** `blueprint` is selected and the configuration sets no `background`
- **THEN** the page draws the `plotter` background, and with shaders unavailable it draws Blueprint's static colour
