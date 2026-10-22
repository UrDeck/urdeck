## MODIFIED Requirements

### Requirement: Built-in Themes
The product MUST ship the built-in themes that exist at this point: `default-dark`, `default-light`, `glass`, `carbon`,
`paper`, `prism` and `obsidian` (plus any whose change has landed). `default-dark` is the default theme. Built-in themes
MUST be available regardless of the contents of the user themes folder.

- A built-in theme MAY name font files that ship with the product; they are found as a user theme's files are found in
  its folder, and with no themes folder at all
- Every built-in theme MUST load without a font or weight warning
- A built-in theme in format 2 MUST set every colour of its own, so nothing of `default-dark`'s palette shows through
- Every built-in theme MUST validate with no error and no warning (`urdeck-cli theme validate`)

#### Scenario: Themes folder is missing
- **WHEN** the user themes folder does not exist
- **THEN** every built-in theme can still be selected

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
- **WHEN** the themes folder holds a theme folder named `prism`
- **THEN** selecting `prism` loads the user's theme, as for any built-in name

#### Scenario: New built-ins validate clean
- **WHEN** `prism` and `obsidian` are validated
- **THEN** no error and no warning is reported

### Requirement: Built-in Theme Metadata
Every built-in theme MUST carry a `meta` group with at least a display name, a description and a mode, so that a
catalog can list it without drawing it.

- `prism` is named "Prism" with mode `dark`, and `obsidian` is "Obsidian" with mode `dark`; each lists the backgrounds it
  pairs with, best first, names one of them as its default background, and has a still gradient as its static background
- Prism pairs with `nebula`, `vortex`, `horizon`, `spectrum` and `ripple`; Obsidian with `nebula`, `vortex` and
  `warpgate` (the panel look may reorder or change these)
- The metadata of the themes that shipped before is unchanged

#### Scenario: Built-ins have metadata
- **WHEN** every built-in theme is loaded
- **THEN** each has a display name, a description and a mode

#### Scenario: Nothing drawn changes
- **WHEN** a snapshot of `default-dark`, `default-light`, `glass`, `carbon` and `paper` is taken before and after this
  change
- **THEN** the pictures are identical

#### Scenario: New built-ins pair with backgrounds
- **WHEN** a catalog lists the built-in themes and the built-in backgrounds
- **THEN** `prism` and `obsidian` each recommend their first paired background first, and every background they name
  exists among the built-in backgrounds

#### Scenario: Default background of a new built-in
- **WHEN** `prism` is selected and the configuration sets no `background`
- **THEN** the page draws the theme's default background in Prism's palette, and with shaders unavailable it draws
  Prism's static gradient
