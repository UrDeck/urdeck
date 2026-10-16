## ADDED Requirements

### Requirement: Stats Gauge Styles
A slot's `style` MUST also accept `meter`, `tach`, `marker` and `thickbar`, each drawn as that style at the positions
where a gauge is drawn. A slot that asks for the theme's default gauge style (`gauge`, or no `style` at a gauge position)
receives it whichever style it is. The rules of "Stats Presentation" for a reading that has no fraction, for the text
size and for stale and unavailable readings apply to every style.

#### Scenario: Slot names a new style
- **WHEN** a slot at a gauge position sets `"style": "tach"`
- **THEN** it is drawn as a tach whatever the theme's default gauge style is

#### Scenario: Theme default is a new style
- **WHEN** a slot sets `"style": "gauge"` and the theme's default gauge style is `marker`
- **THEN** the slot is drawn as a marker gauge

#### Scenario: A new style on a reading that cannot be scaled
- **WHEN** a slot with `"style": "meter"` points at a text reading
- **THEN** the text is drawn as a plain readout and no meter is drawn
