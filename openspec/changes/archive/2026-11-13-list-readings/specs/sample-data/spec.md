## ADDED Requirements

### Requirement: List Samples
A sample reading MAY be of kind list. It then MUST declare its fields as a list description does (name, kind, label,
role and the rest) and give its value as an array of rows, each an object with `key` and one member per field it has.
Keyframes MAY give a list at each time; a list holds until the next keyframe and never blends two lists.

#### Scenario: A fixed list
- **WHEN** the Steam sample describes `friends/playing` with three rows
- **THEN** a preview of the List widget draws the three rows

#### Scenario: Rows that change
- **WHEN** a list sample's keyframes are two rows at 0 s and three rows at 4 s, repeating every 8 s
- **THEN** a drawing at 5 s shows three rows and one at 9 s shows two

#### Scenario: A row with a missing field
- **WHEN** a sample row has no `game` member
- **THEN** that row's `game` field is absent, as with a provider
