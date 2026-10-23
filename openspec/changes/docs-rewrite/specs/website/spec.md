## MODIFIED Requirements

### Requirement: Configuration Pages
The site MUST hold the user documentation for configuring UrDeck by hand: the config file and where it is, pages and the
dock, each first-party widget and its settings, themes (the complete theme format, fonts, icon sets, motion, checking a
theme and every finding), and backgrounds (the setting, the built-ins, pairing, writing one, the manifest and every
finding). It is the only home of that documentation: the repository's `docs/` folder holds contributor and design notes
and links to the site for anything a user needs. Each page is reachable from a navigation that every documentation page
shows.

Reference that the product already defines MUST be built from it, never typed by hand:

- The theme format's keys (name, type, allowed values or range, default, description) from the published theme schema
- The background manifest's keys from the published manifest schema
- Each first-party widget's settings (name, type, default) from the product's catalog, with a description kept beside
  the page
- The theme and background finding codes (code, severity, what it means) from the product's catalog
- The lists of built-in themes, backgrounds and widgets from the product's catalog, with their pictures from the media
  manifest in the chosen theme

The site's build MUST fail when a schema key, a widget setting or a finding code has no description, or when a built-in
theme, background or widget, or a finding code, does not appear in the documentation.

#### Scenario: Finding a widget's settings
- **WHEN** a visitor opens the configuration section and chooses a first-party widget
- **THEN** the page lists that widget's settings with their types, defaults and descriptions, an example, and the
  widget at each of its sizes in the chosen theme

#### Scenario: From the README
- **WHEN** a reader follows the README's link for configuration, themes or backgrounds
- **THEN** they arrive at that section of the site

#### Scenario: A new theme key
- **WHEN** a key is added to the theme schema with a description
- **THEN** the theme reference on the site lists it after the next build, with no page edited

#### Scenario: A key without a description
- **WHEN** a key is added to the theme schema without a description
- **THEN** the site's build fails, naming the key

#### Scenario: A new finding code
- **WHEN** the product gains a finding code and the catalog is regenerated
- **THEN** the troubleshooting page lists it, or the build fails when it has no description

## ADDED Requirements

### Requirement: Theme Cookbook
The site MUST hold a theme authoring cookbook: recipes for chamfered cards, a gradient border, a hard shadow, LED bars, a
tach with a redline, a dotted page, a circular dock and custom icons. Each recipe is a theme folder kept in the repository
(`site/cookbook/<recipe>/`): its page shows the folder's `theme.json` exactly as it is in the repository and a picture
rendered from that folder by the product. Every recipe MUST pass `urdeck-cli theme validate` without errors in CI, so a
recipe that stops working fails a pull request.

#### Scenario: A recipe's JSON and picture agree
- **WHEN** a recipe's `theme.json` changes and the media are refreshed
- **THEN** its page shows the new JSON and the new picture

#### Scenario: A broken recipe
- **WHEN** a pull request renames a theme key a recipe uses
- **THEN** CI fails on that recipe's validation
