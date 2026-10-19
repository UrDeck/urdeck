## ADDED Requirements

### Requirement: Theme Carries The Part Values
The `Theme` a widget receives MUST carry, as read-only data resolved for the surface, the theme's chip, divider and well
values and its gauge sparkline and status values, with sizes in pixels. A theme that sets none gives values that change
nothing. The gauge options a widget passes gain an optional history of fractions and an optional status text and
level. Adding these MUST NOT remove or change any member a widget built against the earlier SDK uses.

#### Scenario: A widget built before the parts
- **WHEN** a widget compiled against the earlier SDK is drawn under a theme with a divider, a chip and a well
- **THEN** it draws as before and is not rebuilt

#### Scenario: Sizes are pixels
- **WHEN** a theme sets `well.radius` to 0.09 and the surface's grid cell is 275 pixels
- **THEN** the radius the theme reports is 24.75 pixels
