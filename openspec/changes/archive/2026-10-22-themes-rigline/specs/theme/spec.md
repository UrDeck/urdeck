## ADDED Requirements

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
