## ADDED Requirements

### Requirement: Reading Area And Category
A reading's description MAY name the area (room) the reading belongs to and a category (plain words such as "Lights",
"Sensors", "CPU"). Both are for finding readings in the editor; they MUST NOT change how a reading is shown on the deck.
A description without them is grouped by its device, or under "Other".

#### Scenario: A light in the office
- **WHEN** Home Assistant lists `light.desk_strip`, which is assigned to the area Office
- **THEN** its description has the area "Office" and the category "Lights"
