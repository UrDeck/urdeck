## Purpose

The settings form the editor builds for any widget or provider from its declared settings, so that every setting can be
changed with a fitting control, including the settings of plugins the editor knows nothing about.

## ADDED Requirements

### Requirement: Generated Settings Form
When a widget or a dock slot is selected, the properties pane MUST show a form with one control per declared setting
of the widget's type, in the declared order and groups, each with its label and help text. The control depends only on
the setting's kind and modifiers, never on the widget type. A setting the type does not declare is not shown, and its
value in the file is kept.

- Toggle: a switch; Choice: radio buttons for up to three options, a drop-down list for more; Text: a text box; Number:
  a number box with its unit; Range: a slider with its value; Secret: a password box (providers)
- Groups are shown under small headings; when more than eight settings are visible, each group folds into an
  expandable section, the first one open

#### Scenario: Clock form
- **WHEN** the user selects a Clock widget
- **THEN** the form shows Hours (24-hour, 12-hour) and Style (Simple, Split-flap) as radio buttons, the toggles for the
  date, week, seconds and blinking separator, and the header choice

#### Scenario: A third-party widget
- **WHEN** the user selects a widget from a plugin that declares a toggle `chime` labelled "Chime at the end"
- **THEN** the form shows a switch labelled "Chime at the end" and changing it writes `chime` to the widget object

### Requirement: Values Are Written As They Change
Each change in the form MUST be written to the configuration at once (see config-document): the preview and the deck
show it within half a second. Typing into one text or number field is one undo step. An emptied field removes its key,
so the widget uses its fallback; a value that is not valid for the setting (outside a number's range) is not written
and the field says why.

#### Scenario: Empty removes
- **WHEN** the user clears a Stats slot's Decimals field
- **THEN** the slot object has no `decimals` key and the card uses its automatic decimals

#### Scenario: Out of range
- **WHEN** the user types 9 into Decimals, whose maximum is 3
- **THEN** nothing is written and the field shows the allowed range

### Requirement: Fallbacks Are Shown
An empty field MUST show what will be used instead, as its placeholder: the declared words, or the value the widget will
take from the chosen reading (its label, unit, range or levels) or from the user's region. A text whose empty value
differs from its absent value (allows none) MUST offer a switch to show or hide it: off writes the empty text, on with an
empty box removes the key.

#### Scenario: Label from the reading
- **WHEN** a Stats slot shows `system:gpu/temperature` and has no label
- **THEN** the Label field is empty with the placeholder "GPU temperature"

#### Scenario: Hiding a label
- **WHEN** the user turns off "Show a label" on that slot
- **THEN** the slot has `"label": ""` and the card shows no label

### Requirement: Units Follow The Reading
A number whose unit comes from a reading MUST be shown and typed in the unit the card shows (the slot's chosen unit,
else the user's region), and stored in the reading's canonical unit. A unit setting MUST offer the display units of the
chosen reading's kind and "Automatic", and be hidden for a reading with a single unit.

#### Scenario: Fahrenheit threshold
- **WHEN** a slot shows a temperature in Fahrenheit and the user types 167 into Warning from
- **THEN** the field shows "167 °F" and the file has `"warning": 75`

### Requirement: Settings For Other Sizes And Conditions
A setting declared for some sizes MUST be shown at other sizes, disabled, with "Only at" and the sizes. A setting shown
only when another setting has certain values MUST be hidden until it has one of them.

#### Scenario: Forecast days at 4x2
- **WHEN** a Weather widget at 4x2 is selected
- **THEN** Forecast days is shown disabled with "Only at 4x4."

#### Scenario: Dimmer level
- **WHEN** a Control widget's style is changed from switch to dimmer
- **THEN** the Level reading and its action appear in the form

### Requirement: Readings, Actions And Units In The Form
A reading setting MUST show the chosen reading's label, id and live value (sample value in Sample mode), and open a
reading chooser restricted to what the setting accepts. An action setting MUST offer the actions of the reading chosen
in its owner setting, and be disabled while that reading is not chosen.

#### Scenario: Live value
- **WHEN** a Stats slot shows `system:cpu/load` in Live mode
- **THEN** its Reading button shows "CPU load", the id and the current load, updating as it changes

#### Scenario: Actions of a light
- **WHEN** a Control widget's reading is a Home Assistant light
- **THEN** the Action list offers that light's actions

### Requirement: Place Search
A place setting MUST search as the user types (after two characters), through the provider the setting names, show the
suggestions with a detail line, and store the chosen place's name and coordinates together in one step. A search that
fails says why in the list.

#### Scenario: Choosing a place
- **WHEN** the user types "Portl" in a Weather widget's Place and chooses "Portland, Oregon"
- **THEN** the widget has that location with its latitude and longitude, and the preview shows its weather

### Requirement: Launch Targets And Icons
A launch target setting MUST accept a typed target and offer to browse for an application, a file or a folder (only the
kinds the setting allows), and say what the target is. An icon setting MUST offer Automatic, the glyphs of the current
theme's icon set and, when allowed, a picture file.

#### Scenario: Browse for a folder
- **WHEN** the user chooses Browse, Folder… and picks `D:\Projects` for a Shortcut
- **THEN** the target is `D:\Projects` and the line under it says it is a folder that opens in File Explorer

#### Scenario: Glyph from the theme
- **WHEN** the user opens a Control widget's Icon and picks the light glyph
- **THEN** the widget shows that glyph from the theme's icon set

### Requirement: List Of Positions
A list of positions MUST show one expandable section per position of the widget's current size, each with its number,
its role, what it shows and its live value; opening it shows the item's form. An empty position offers to fill it.
Positions can be reordered with Move up and Move down. When the list has more items than the size has positions, the
form says how many stay in the file.

#### Scenario: Stats at 4x2
- **WHEN** a Stats widget at 4x2 with three slots is selected
- **THEN** the form shows five positions: two gauge positions and one text position filled, two text positions empty

#### Scenario: Shrinking keeps slots
- **WHEN** that widget is changed to 2x2
- **THEN** the form shows one position and says "2 more slots stay in the file and come back at a larger size."

### Requirement: Unknown Kind In The Form
A setting whose kind the editor does not know MUST be edited as text, with a line saying that the plugin uses a newer
kind of setting.

#### Scenario: Newer kind
- **WHEN** a plugin's setting has the kind `colourRole`
- **THEN** the form shows a text box for it and the line "Edited as text: this plugin uses a newer kind of setting."
