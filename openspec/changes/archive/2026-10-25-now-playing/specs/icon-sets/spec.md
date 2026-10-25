## ADDED Requirements

### Requirement: Media Keys
The product's canonical keys MUST include `media/play`, `media/pause`, `media/previous` and `media/next`, and the built-in
set MUST map them to icons (the play, pause, skip-back and skip-forward glyphs of the upstream set it already carries,
with their licence unchanged). A set that omits them is answered by the resolution chain like any other omitted key, and
a widget that draws them has a text fallback for the case where no set has them.

#### Scenario: Built-in set
- **WHEN** a theme uses the built-in set
- **THEN** all four keys have an icon

#### Scenario: A user set without media keys
- **WHEN** a theme's own set maps none of the media keys
- **THEN** the resolution chain supplies the built-in icons, as it does for any other omitted key
