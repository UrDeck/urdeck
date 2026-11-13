## ADDED Requirements

### Requirement: Reading Settings And Lists
A reading setting MAY declare that it accepts list readings only, or that it does not accept them; by default it accepts
every reading, a list then being shown as its count (see data-providers, "A List Read As One Value"). The declaration MUST
be part of the setting's schema, so the editor and the catalog export can filter by it.

#### Scenario: The List widget's reading
- **WHEN** the List widget's configuration declares its reading setting as lists only
- **THEN** its schema says so, and the editor offers only list readings for it

#### Scenario: A stats slot
- **WHEN** a stats slot's reading setting declares nothing about lists
- **THEN** a list reading can be chosen for it, and the slot shows the list's count
