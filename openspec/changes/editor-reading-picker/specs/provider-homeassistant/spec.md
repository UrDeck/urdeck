## MODIFIED Requirements

### Requirement: Listing Entities
The provider MUST be able to list its concrete readings (see data-providers, "Listing Concrete Readings"): one
description per entity in Home Assistant, built by the same rules as a shown entity's, from the states and Home
Assistant's area, device and entity registries (four requests). Each description has the entity's area (its own, else
its device's) and a category from its domain (data-providers, "Reading Area And Category"). When the registries cannot
be read, the entities are listed without areas and the log says so once.

#### Scenario: Browse
- **WHEN** the readings of `homeassistant` are listed for an installation with 1,234 entities
- **THEN** 1,234 descriptions are returned, each with its entity id as path, its friendly name, kind, unit, action, icon,
  category and, where Home Assistant assigns one, area

#### Scenario: Area from the device
- **WHEN** an entity has no area of its own and belongs to a device in the area Kitchen
- **THEN** its description's area is "Kitchen"

#### Scenario: Registries unavailable
- **WHEN** the registry requests fail
- **THEN** the entities are listed without areas and one log line says so
