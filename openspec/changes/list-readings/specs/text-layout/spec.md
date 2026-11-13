## ADDED Requirements

### Requirement: Text List Item Image
A text list item MAY have a leading image. The image MUST be drawn as a square the height of the item's first line,
with the theme's image corner, before the mark and the text; the text of every line of the item starts after it. An item
whose image is not yet available keeps the image's place empty, so the text does not move when the image arrives. An
image that cannot be decoded leaves the place empty and is reported once by the icon service.

#### Scenario: Avatar beside a name
- **WHEN** an item has the text "Alex", the second line "Deep Rock Galactic" and an avatar image
- **THEN** the avatar is drawn left of both lines, the height of the first line

#### Scenario: Image arrives late
- **WHEN** an item's image is decoded after the list was first drawn
- **THEN** the image appears in its place and no text moves

### Requirement: Text List From A List Reading
The SDK MUST let a widget fill a text list from a list reading and its description in one call. Each row becomes an item
keyed by the row's key: the title field is the item's text, the detail field its second line, the status field its mark
(formatted text, coloured from the theme's status colours by the field's level), and the image field its image, asked
from the icon service. Values are formatted with the reading formatter. A list replaced by one with some of the same keys
keeps the scroll position on the first visible row that is still present.

#### Scenario: Friends list
- **WHEN** a widget fills a text list from `steam:friends/playing` with five rows
- **THEN** the list holds five items with names, games and avatars, in the provider's order

#### Scenario: A friend stops playing
- **WHEN** the list is scrolled to the third row and the first row disappears from the next list
- **THEN** the row that was third (now second) stays at the top of the view
