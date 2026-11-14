## MODIFIED Requirements

### Requirement: Reading Values
A reading's value MUST be one of: a number, a text, an on/off state, a time, or a list. A time is an instant together with
the UTC offset it is to be read in, so that a sunrise at another place can be shown in that place's local time; a date is
the same value, read as the day that instant falls on in that offset. A list is an ordered set of rows (see "List
Readings"). Values MUST be of types defined by the SDK, so that a widget can read a value from any provider without
referencing that provider's assembly. Two time values are equal only when both the instant and the offset are equal. Two
lists are equal only when they have the same rows in the same order, each with the same key and the same fields with
equal values.

#### Scenario: Widget reads a value from an unrelated provider
- **WHEN** a widget built without any reference to a provider's assembly reads one of that provider's readings
- **THEN** it receives the value as a number, a text, an on/off state, a time or a list

#### Scenario: Time keeps its offset
- **WHEN** a provider publishes the instant 2026-10-08 06:42 at UTC offset +09:00
- **THEN** a widget that reads it gets that instant and the offset +09:00

#### Scenario: Same instant, other offset
- **WHEN** a provider publishes the same instant first with offset +09:00 and then with offset +02:00
- **THEN** the second publication counts as a change and widgets that show the reading are repainted

#### Scenario: Same list again
- **WHEN** a provider publishes a list of three friends and a minute later publishes a new list with the same three rows
- **THEN** the second publication is not a change and no widget is repainted

## ADDED Requirements

### Requirement: List Readings
A provider MAY describe a reading of kind list. Its value MUST be a list of rows, and the list is published and replaced
as a whole, so a widget never sees part of an old list and part of a new one.

- A row has a key, a text that is unique within the list and stays the same for the same thing across publications
  (a SteamID, an app id), and named fields; each field holds a number, a text, an on/off state or a time, never a list
- A row MAY lack a field; the field is then absent for that row, which is not an error
- The description of a list reading MUST declare its fields in order, each with a name, a kind (any kind but list), a
  label and, where they apply, the unit, display unit, decimals, range and levels a reading of that kind may declare
- A field MAY carry one role: title, detail, status, image (a local file path or an `http` or `https` address) or link
  (a launch target). At most one field is the title; a list with none uses its first text field as the title. A role a
  widget does not know is ignored
- The description MAY declare an empty text, shown by a widget when the list has no rows
- Every rule for readings (identity, patterns, states, interruptions, demand, samples) applies to a list reading as to any
  other

#### Scenario: A widget that does not know the provider
- **WHEN** the List widget shows `steam:friends/playing`, whose description gives `name` the title role, `game` the
  detail role and `avatar` the image role
- **THEN** each row is drawn with the friend's name, the game below it and the avatar beside it

#### Scenario: Whole-list replacement
- **WHEN** a provider replaces a list of five rows with a list of four in one publication while a widget paints
- **THEN** the widget paints either the five rows or the four, never a mix

#### Scenario: A row without a field
- **WHEN** a friend's row has no `game` field
- **THEN** a widget reading `game` on that row finds it absent and draws the row without a detail line

#### Scenario: Stale list
- **WHEN** a provider that published a list fails its next samples
- **THEN** the list reading is stale and still exposes the last list

### Requirement: List Bounds
The system MUST keep a list to at most 200 rows and a text field to at most 500 characters. A publication beyond either
bound is cut (the first 200 rows are kept; a longer text is cut and ends with an ellipsis), and the cut is logged once per
reading, naming the provider, until the reading is published within the bounds again. A field holding a list, or two rows
with the same key, make the publication invalid: the reading keeps its previous value and the reason is logged once.

#### Scenario: Too many rows
- **WHEN** a provider publishes a list of 350 rows
- **THEN** widgets receive the first 200, and one log line names the provider and the reading

#### Scenario: Duplicate keys
- **WHEN** a provider publishes a list in which two rows have the key `42`
- **THEN** the reading keeps its previous value and the log says why, once

### Requirement: A List Read As One Value
Where a widget formats a list reading as one value (a stats slot, a readout), the formatter MUST give the list's number of
rows as a whole number, with the list description's unit when it declares one. A widget written before lists existed
that reads a list reading therefore shows its count.

#### Scenario: Friends in a game in the stats widget
- **WHEN** a stats slot shows `steam:friends/playing` and the list has 3 rows
- **THEN** the slot shows 3 with the list's unit

#### Scenario: Empty list as a value
- **WHEN** the list has no rows
- **THEN** the slot shows 0, not "unavailable"

### Requirement: Formatting A Field
The formatter MUST format a field of a row with the field's description by the same rules as a reading of that kind
(units, the region's units and clock, decimals, levels), so that a temperature in a list row and a temperature in a
readout look alike.

#### Scenario: Hours played
- **WHEN** a list field `hours` is a number with one decimal and the unit `h`, and a row holds 12.25
- **THEN** the field is formatted as `12.3` with the unit `h`

#### Scenario: Last played in the region's style
- **WHEN** a list field `lastPlayed` is of kind date and the region writes dates day first
- **THEN** the field is formatted as a day in that style
