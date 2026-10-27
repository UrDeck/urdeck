## ADDED Requirements

### Requirement: Sample Descriptions Carry Action And Icon
A reading in a sample file MAY have `action` (a path of an action of the same provider) and `icon` (a canonical icon key),
which MUST become the description's natural action and icon (see data-providers, "Natural Action And Icon"). A sample still
performs no actions (see provider-actions, "Demo And Sample Providers"): a widget that uses the natural action of a sample
reading gets a failed result and draws it as it draws any failed action.

#### Scenario: Sample light
- **WHEN** a sample file describes `light.living_room` with `"action": "light.living_room/toggle"` and
  `"icon": "home/light"`
- **THEN** a widget showing it with sample data finds that action and icon in the description
