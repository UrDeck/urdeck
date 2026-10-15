## ADDED Requirements

### Requirement: Pairing Data
The engine MUST read the recommendations that a theme and a background MAY each make of the other. A recommendation is
data for a person choosing, and for the editor that will help them; it MUST NOT decide what the deck shows.

- A theme names backgrounds in `meta.pairsWith.backgrounds`, best first
- A background names themes in its manifest's `pairsWith.themes`, best first
- A name is not checked when it is read: a theme may name a background that is not installed and a background may name
  a theme that does not exist, and each is skipped when recommendations are made
- A theme's default background (`background.default.name`) is expected to be in its own list; a later validator will
  warn when it is not, and nothing else depends on it

#### Scenario: Theme recommends a background
- **WHEN** a theme's `meta.pairsWith.backgrounds` is `["contour", "streams"]`
- **THEN** the pairing index lists both pairs for that theme, `contour` first

#### Scenario: Background recommends a theme
- **WHEN** a background's `pairsWith.themes` is `["carbon"]` and the theme `carbon` does not list the background
- **THEN** the pairing index lists the pair

#### Scenario: Name that does not exist
- **WHEN** a theme names a background that is not installed
- **THEN** it is skipped when recommendations are made and nothing is logged

### Requirement: Pairing Index
The engine MUST provide a pairing index over the themes and backgrounds it is given, that joins both sides of the data:

- A pair of a theme and a background is **listed** if either side names the other, and **mutual** if both do
- The index never needs the shipped data to be symmetric: a user's theme can list built-in backgrounds, and a
  downloaded background can list built-in themes
- It answers, for a theme, the backgrounds listed with it, each with whether the pair is mutual, and the other way
  round for a background
- A name is compared as written, exactly, so `Carbon` does not match `carbon`
- It reads only the data it is given; it loads no fonts, compiles no shaders and logs nothing

#### Scenario: One-sided pair
- **WHEN** only the theme names the background
- **THEN** the pair is listed and not mutual

#### Scenario: Mutual pair
- **WHEN** the theme names the background and the background names the theme
- **THEN** the pair is listed and mutual

#### Scenario: Unrelated
- **WHEN** neither names the other
- **THEN** the pair is not listed

### Requirement: Recommendations
The engine MUST provide a recommendation for a theme from the backgrounds it is given, a list of at most `limit`
backgrounds each with the reason it was picked. The order is fixed, and the same input gives the same list:

1. The theme's default background (reason `ThemeDefault`)
2. Mutual pairs, in the theme's own order (`Mutual`)
3. Pairs only the theme lists, in the theme's order (`ThemeListed`)
4. Pairs only the background lists, in the order of the backgrounds given (`BackgroundListed`)
5. Backgrounds with no pairing data at all that share at least one tag with the theme's `meta.tags` (`TagMatch`)

A background appears once, with the reason of the first rung it meets. Nothing else is recommended: a theme with no
data gets an empty list, which an editor shows as "browse all".

#### Scenario: Theme default first
- **WHEN** the theme's default is `contour` and it also lists `streams`
- **THEN** the list starts with `contour` for `ThemeDefault`, then `streams`

#### Scenario: Mutual before one-sided
- **WHEN** the theme lists `a` and `b`, `b` also lists the theme and `a` does not
- **THEN** `b` comes before `a`

#### Scenario: Limit
- **WHEN** seven backgrounds are listed and the limit is 3
- **THEN** the first three in the order above are returned

#### Scenario: Tags only for backgrounds with no data
- **WHEN** a background lists no themes, no theme lists it, and it shares the tag `calm` with the theme
- **THEN** it is recommended for `TagMatch`; a background that has pairing data about other themes is not

#### Scenario: Nothing to recommend
- **WHEN** the theme has no data and no background names it
- **THEN** the list is empty

### Requirement: Theme And Background Catalogs
The engine MUST list the themes and backgrounds that exist, cheaply, for the settings editor and the command line tool:

- The theme catalog lists the built-in and the user themes: the name, the `meta` values (display name, description,
  author, mode, tags, pairings, samples), the colours for a swatch (background, card fill, accent, text) and the name
  of the theme's default background. A user theme with the name of a built-in replaces it, as it does when loaded
- The background catalog lists the built-in and the user backgrounds: the name, the manifest's title, description,
  tags, pairings, parameters and samples, and whether it is built in
- Listing loads no fonts and compiles no shader, and one unreadable theme or background is left out with a logged
  warning without affecting the others
- Neither lists a theme or background twice

#### Scenario: Listing themes
- **WHEN** the catalog is read with the three built-in themes and one user theme
- **THEN** four entries are returned, each with a name and swatch colours, and no font was loaded

#### Scenario: User theme replaces a built-in
- **WHEN** a user theme is named `glass`
- **THEN** the catalog has one `glass`, the user's

#### Scenario: Broken theme file
- **WHEN** one user theme's settings cannot be read
- **THEN** it is not listed, a warning is logged, and the others are listed

#### Scenario: Listing backgrounds
- **WHEN** the catalog is read
- **THEN** every built-in background is listed with its manifest values and without having been compiled
