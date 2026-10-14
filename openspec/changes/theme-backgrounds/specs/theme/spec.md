## ADDED Requirements

### Requirement: Built-in Theme Metadata
Every built-in theme MUST carry a `meta` group with at least a display name, a description and a mode, so that a
catalog can list it without drawing it.

- `default-dark` is named "Default Dark" with mode `dark`, `default-light` is "Default Light" with mode `light`, and
  `glass` is "Glass" with mode `dark`
- A built-in theme may carry pairings and tags; the three that exist before the format 2 themes define no background and
  no default, so adding `meta` changes nothing that is drawn

#### Scenario: Built-ins have metadata
- **WHEN** the three built-in themes are loaded
- **THEN** each has a display name, a description and a mode

#### Scenario: Nothing drawn changes
- **WHEN** a snapshot of each of the three built-in themes is taken before and after `meta` is added
- **THEN** the pictures are identical
