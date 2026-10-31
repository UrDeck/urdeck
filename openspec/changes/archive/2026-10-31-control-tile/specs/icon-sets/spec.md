## ADDED Requirements

### Requirement: Home Keys
The product's canonical keys MUST include the `home` group, for things in and around a house, independent of where their
readings come from: `home/light`, `home/outlet`, `home/switch`, `home/fan`, `home/lock`, `home/door`, `home/window`,
`home/blinds`, `home/garage`, `home/motion`, `home/leak`, `home/thermometer`, `home/droplet`, `home/bolt`,
`home/battery`, `home/scene`, `home/script`, `home/button`, `home/tv`, `home/speaker` and `home/entity` (a generic thing).
The built-in set MUST map every one of them to an icon of the upstream set it already carries, with its licence unchanged,
and stay free of findings. A set that omits them is answered by the resolution chain like any other omitted key.

#### Scenario: Built-in set
- **WHEN** a theme uses the built-in set
- **THEN** all 21 `home` keys have an icon

#### Scenario: A user set without home keys
- **WHEN** a theme's own set maps none of the `home` keys
- **THEN** the resolution chain supplies the built-in icons
