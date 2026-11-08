## Purpose

Finding and choosing a reading in the editor by what the user calls it, with its live value, across every provider,
including a Home Assistant installation with thousands of entities.

## Requirements

### Requirement: Choosing A Reading
A reading setting MUST open a picker that lists the readings of every provider, restricted to what the setting accepts
(readings with a range, readings with an action, or one provider's readings). Choosing a reading sets it and closes the
picker; the current reading is selected when the picker opens.

#### Scenario: Only controllable readings
- **WHEN** the user opens the picker from a Control widget's reading
- **THEN** only readings that can be switched or set are listed, and the picker says so

### Requirement: Search And Filters
The picker MUST search as the user types, without waiting for the network: every word typed must match the reading's
label, name, id, area or category, ignoring case and accents. Matches are shown first by label, then by id. When a
provider supplies areas and categories, the picker MUST offer filters for them. The picker shows how many readings
match out of how many.

#### Scenario: A thousand entities
- **WHEN** Home Assistant has 1,192 entities and the user types "desk"
- **THEN** the matching entities are listed within a tenth of a second, grouped by category and area, the matched text
  in bold, with "9 of 1,192 entities match"

#### Scenario: Area filter
- **WHEN** the user chooses the area Office
- **THEN** only entities in the Office are listed

### Requirement: Live Values In The Picker
Each listed reading MUST show its current value (its sample value in Sample mode). Only readings on screen are
subscribed, and they are released when they scroll away or the picker closes.

#### Scenario: Scrolling
- **WHEN** the user scrolls through the list
- **THEN** values appear on the rows that come into view, and the host subscribes to no more readings than are visible
  plus a few

### Requirement: Readings Not In A List
The picker MUST let the user type a reading id by hand and say whether a provider has it. A reading that a provider
describes with a pattern (a `{name}` segment) MUST be offered as a template that opens the typed id with the pattern
filled in.

#### Scenario: A core's load
- **WHEN** the user chooses the template `system:cpu/core/{n}/load`
- **THEN** a box shows the id with `{n}` selected, and typing 3 shows the label and value of core 3

### Requirement: Provider Not Connected
When a provider cannot list its readings because it is not set up or not reachable, the picker MUST say so with a way to
set it up, and still list the readings of that provider that the pages already use, without values. A reading setting in
the form whose provider is missing required settings MUST say so too.

#### Scenario: Home Assistant without a token
- **WHEN** Home Assistant has an address but no token and the user opens the picker's Home Assistant tab
- **THEN** the picker says Home Assistant is not connected, offers Set up, and lists the entities the pages use

### Requirement: Readings Section
The editor MUST have a Readings section that lists every provider's readings with their live values, with the same
search and filters as the picker, and lets the user copy a reading's id.

#### Scenario: Browsing
- **WHEN** the user opens Readings and types "gpu"
- **THEN** the GPU readings of this PC are listed with their current values
