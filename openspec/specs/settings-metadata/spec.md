# settings-metadata Specification

## Purpose
How a widget or a data provider declares the settings a user can change, so that the editor can build a form for any
plugin and the site can document it, without the plugin shipping any user interface.

## Requirements

### Requirement: Declared Settings
A widget's configuration type and a provider's settings type MUST declare each setting a user can change, on the
property that holds it, with a label and a help text. The declaration states the setting's kind, from which the editor
chooses the control:

- Toggle (on/off), Choice (one of declared options, each with a label), Text, Number (optional minimum, maximum, step,
  whole numbers only, and a unit), Range (a bounded number chosen by sliding), Reading (a reading id, optionally only
  readings that have a range or an action, or of one provider), Action (an action of the reading chosen in another
  setting), Unit (a display unit of the reading chosen in another setting), Place (a place found by a provider's
  search, stored as its name and coordinates), Launch target (an application, file, folder or address, optionally
  limited to some of them), Icon (automatic, a glyph of the theme's icon set, or a file), Secret (providers only; see
  provider-secrets) and List of positions (see "Positions")
- The kind is taken from the property's type when that is unambiguous (on/off is a Toggle, text a Text, a number a
  Number, text with declared options a Choice) and from an explicit kind otherwise
- A declared option MAY have an empty value, meaning "the key is absent"; the editor then removes the key instead of
  writing it
- Properties that are not declared are still read from and written to the configuration file as today, but the
  editor does not show them
- Labels and help texts are plain English text

#### Scenario: A toggle from a bool
- **WHEN** a widget configuration has a declared `bool` property `showSeconds` labelled "Show seconds"
- **THEN** its schema lists a setting with key `showSeconds`, kind toggle, label "Show seconds", and the type's default

#### Scenario: A choice
- **WHEN** a text property declares the options `simple` "Simple" and `flap` "Split-flap"
- **THEN** its schema lists a choice with those two options in that order

#### Scenario: Undeclared property
- **WHEN** a widget configuration has a public property without a declaration and the file sets it
- **THEN** the widget receives the value, the schema does not list it, and the value is still in the file after a save

### Requirement: Setting Modifiers
Any setting MAY carry modifiers, which the schema MUST expose:

- **Group and order**: a heading the setting is shown under, and its position within the group; settings without a
  group come first, and settings with equal order keep their declaration order
- **Sizes**: the widget sizes the setting applies at. At another size the setting is still listed, marked as applying
  only at those sizes
- **Shown when**: the setting applies only while another setting of the same object has one of the given values
- **Fallback**: what an empty value means, as words ("None", "Automatic", "Off") or as a source the editor resolves:
  the chosen reading (its label, unit, range or levels), the user's region, or the theme
- **Allows none**: for text, an empty value is meaningful and different from an absent one (absent uses the fallback,
  empty shows nothing)
- **Unit from a reading**: for a number, its unit is the display unit of the reading chosen in another setting

A modifier that names another setting MUST name one that exists on the same object; otherwise the plugin's type is
rejected when it loads, with a logged reason, as for any invalid plugin type.

#### Scenario: Only at one size
- **WHEN** Weather's `days` declares the size 4x4
- **THEN** the schema lists `days` with sizes ["4x4"]

#### Scenario: A modifier naming a missing setting
- **WHEN** a widget's setting is shown when `mode` is `advanced` and the type has no `mode` setting
- **THEN** the widget type is rejected when the plugin loads and the log says which setting names which missing key

### Requirement: Positions
A list setting MUST declare, for each widget size, how many positions the list has at that size and the role of each
(words chosen by the widget, such as "gauge" and "text"). Each item of the list is an object whose settings are
declared the same way. An item setting MAY be disabled in some roles, with a sentence saying why.

- Items beyond the positions of the current size stay in the file untouched
- The positions declared by a first-party widget MUST match what the widget draws at each of its sizes

#### Scenario: Stats at 4x2
- **WHEN** the schema of the Stats widget is read
- **THEN** its `slots` setting has, at 4x2, five positions with the roles gauge, gauge, text, text, text, and each
  item's `style` setting is disabled in the text role

#### Scenario: Items beyond the positions
- **WHEN** a Stats widget at 2x2 has three slots in the file and the configuration is saved
- **THEN** all three slots are still in the file

### Requirement: Unknown Kind
A setting whose declared kind is not one this version knows MUST still be listed, with its kind as declared, so that the
editor can offer it as text and say that the plugin uses a newer kind of setting.

#### Scenario: A kind from a newer SDK
- **WHEN** a plugin declares a setting with the kind `colourRole`, which this version does not know
- **THEN** the schema lists the setting with kind `colourRole` and the plugin loads

### Requirement: Suggestions From A Provider
A provider MAY answer suggestion queries of named kinds: given the text a user has typed, it returns a short list of
suggestions, each with a label, an optional detail line, and the values to store when it is chosen. A Place setting
names the provider and the kind it asks. The engine MUST route such a query to the named provider as follows:

- A query starts the provider if it is not running and lets it linger afterwards, as listing readings does
- A query that takes more than 10 seconds, or fails, answers with a message and no suggestions; it never stops the deck
- Only the provider talks to its service; UrDeck itself sends the typed text nowhere else

#### Scenario: Finding a place
- **WHEN** the Weather provider is asked for `place` suggestions for "Portl"
- **THEN** it answers with places from its geocoding service, each with the name, the region in the detail line, and
  `location`, `latitude` and `longitude` as the values to store

#### Scenario: Offline
- **WHEN** the geocoding service cannot be reached
- **THEN** the answer is a message saying so and no suggestions, within 10 seconds

### Requirement: Display Unit Conversion
The SDK MUST convert a reading's value between its canonical unit and a display unit (temperature, speed, depth), the
same conversion the formatter uses, so that a form can show and accept a value in the unit the card shows while the
configuration keeps the canonical unit.

#### Scenario: A threshold in Fahrenheit
- **WHEN** a temperature threshold of 75 (canonical, °C) is converted for display in Fahrenheit
- **THEN** the result is 167, and converting 167 back gives 75

#### Scenario: Formatting unchanged
- **WHEN** the formatter's existing tests run after the conversion is moved into the shared methods
- **THEN** they pass unchanged

### Requirement: Every Setting Is Declared
The analyzer MUST warn (URDECK006) about a public settable property of a widget configuration type, a provider
settings type or a list item type that is neither declared as a setting, nor marked as written by another setting's
control, nor excluded from JSON. It MUST also warn about a declared setting whose kind cannot be taken from its type and
is not given. Every first-party widget and provider MUST build without this warning.

#### Scenario: A forgotten setting
- **WHEN** a widget configuration type gains `public int Speed { get; set; }` with no declaration
- **THEN** the build reports URDECK006 naming `Speed`

#### Scenario: A part of another setting
- **WHEN** Weather's `latitude` is marked as written by the `location` setting
- **THEN** no warning is reported for it and the schema does not list it as a setting

### Requirement: Settings In The Catalog
The product's catalog (`urdeck-cli catalog export`) MUST list, for each first-party widget and provider, each declared
setting with its key, kind, label, help, group, sizes, options with their labels, default and, for a list of positions,
the positions per size and the item's settings.

#### Scenario: Help text reaches the site
- **WHEN** the catalog is exported
- **THEN** each Clock setting has a non-empty `help`, and the Stats `slots` setting lists the item settings
