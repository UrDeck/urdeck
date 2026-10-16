## ADDED Requirements

### Requirement: Outside The Dock's Cards
What a theme draws outside its cards (see widget-card, "Drawn Outside The Card") MUST be drawn for the dock's cards too, at
the dock's size, under the dock's cards. It MUST stay inside the dock's band: nothing of it lies over the page's cards or the
page indicator, in the window or in a snapshot.

#### Scenario: Shadows in the dock
- **WHEN** a theme with a hard card shadow is shown with two docked shortcuts
- **THEN** both dock cards have the shadow, at the dock's scale

#### Scenario: A glow that reaches past the band
- **WHEN** a theme's glow reaches further than the space between a dock card and the band's top edge
- **THEN** the glow is cut at the band's edge and the page's bottom row is not covered by it
