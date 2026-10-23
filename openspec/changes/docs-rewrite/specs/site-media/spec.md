## ADDED Requirements

### Requirement: Cookbook Pictures
The media matrix MUST include one picture per cookbook recipe (`site/cookbook/<recipe>/`), rendered from the recipe's
theme folder with what its `recipe.json` asks to show (the sample page, the dock, or one widget at a size), over the
background it names or none. A recipe added or removed adds or removes its picture, and a change to a recipe's folder
makes only its picture stale.

#### Scenario: A new recipe
- **WHEN** a folder `site/cookbook/hard-shadow/` with a theme and a `recipe.json` is added and the media are generated
- **THEN** its picture exists and the media manifest names it as the recipe's picture

#### Scenario: One recipe changed
- **WHEN** only the circular dock recipe's theme changes
- **THEN** only its picture is stale
