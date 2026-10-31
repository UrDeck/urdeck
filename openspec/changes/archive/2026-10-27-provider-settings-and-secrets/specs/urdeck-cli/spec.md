## MODIFIED Requirements

### Requirement: Exporting The Catalog
`urdeck-cli catalog export [-o <file>]` MUST write, as one JSON document (to the file, or to standard output), what the
product knows about what it ships:

- `version`: `1`
- `themes`: each theme's name, display name, description, author, mode, tags, swatch colours, default background and the
  backgrounds it pairs with
- `backgrounds`: each background's name, title, description, author, tags, parameters and the themes it pairs with
- `pairings`: for each theme, the backgrounds listed for it, each marked when the pairing is named on both sides
- `widgets`: each first-party widget's type id, name, description, category, declared sizes with their preview flag, its
  default configuration as it would appear in `urdeck-config.json`, and its settings: each setting's name in
  `urdeck-config.json`, its type (text, number, whole number, on/off, list, object, or the values it accepts) and its
  default for a widget written without it
- `providers`: each first-party provider's id, name, whether it carries a sample, and its settings beyond the base ones:
  each setting's name in `urdeck-config.json`, its type, label, help text, default, and whether it is required or a
  secret (a secret's environment variable name is listed; no value ever is)
- `findings`: every theme and background finding code with its severity and a one-line summary of what it means

Built-in themes and backgrounds are always listed; `--theme-dir` and `--background-dir` add a user's folders, as for
`theme validate`. Lists are sorted by name and the same input gives the same bytes, so the output can be compared
between runs.

#### Scenario: What the site reads
- **WHEN** `catalog export -o catalog.json` is run
- **THEN** `catalog.json` lists the eleven built-in themes, the sixteen built-in backgrounds, the four first-party widgets
  with their sizes, default configurations and settings, the first-party providers with their settings and every finding
  code

#### Scenario: A widget's settings
- **WHEN** the catalog is exported
- **THEN** the clock's settings include `format` with the values it accepts and its default, and `showDate` as on/off

#### Scenario: Stable output
- **WHEN** `catalog export` is run twice with the same inputs
- **THEN** both outputs are byte for byte the same

#### Scenario: A provider's secret in the export
- **WHEN** the catalog is exported and a first-party provider declares a secret `token`
- **THEN** the provider's settings list `token` as a secret with its environment variable name, and no value
