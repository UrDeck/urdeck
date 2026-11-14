# text-layout Specification

## Purpose
Defines how widgets show text that does not fit one line: wrapping to a width, ending in an ellipsis, scrolling sideways
(a marquee), and a vertically scrolling list of items, all drawn in the theme's typefaces so that no widget measures or
styles text on its own.
## Requirements
### Requirement: Ellipsis Overflow
When a single line of text is wider than its rectangle and the widget chose the ellipsis overflow, the text MUST be cut at a
character boundary and end in `…` so that the whole line, ellipsis included, fits the rectangle's width.

- The text is cut between user-perceived characters, never inside a surrogate pair, a combining sequence or an emoji sequence
- Trailing spaces before the ellipsis are dropped
- The text size stays the theme's size for the step; only the amount of text shown changes
- Text that fits is drawn whole, with no ellipsis
- When not even the ellipsis fits, nothing is drawn

#### Scenario: Long title in a narrow tile
- **WHEN** a body line of `The Ballad of a Very Long Track Title` is drawn with ellipsis overflow in a rectangle that fits
  twenty characters
- **THEN** it is drawn at the theme's body size, ends in `…` and is no wider than the rectangle

#### Scenario: Text fits
- **WHEN** a line that fits its rectangle is drawn with ellipsis overflow
- **THEN** it is drawn whole, with no ellipsis

#### Scenario: Emoji at the cut
- **WHEN** the cut would fall inside a flag or a family emoji
- **THEN** the whole emoji is dropped, never half of it

### Requirement: Clip Overflow
When a single line is wider than its rectangle and the widget chose the clip overflow, the text MUST be drawn at the theme's
size and cut at the rectangle's edge, with no ellipsis.

#### Scenario: Clipped line
- **WHEN** a line wider than its rectangle is drawn with clip overflow
- **THEN** it is drawn at the theme's size and nothing is drawn outside the rectangle

### Requirement: Text Block
The SDK MUST provide a component that draws text wrapped to the width of a rectangle, at one of the theme's three steps.

- Lines break at spaces; a word wider than a line breaks between characters; an explicit line feed starts a new line
- A line limit MAY be given. When the text needs more lines, the last line shown ends in `…` (as in "Ellipsis Overflow") and the
  rest is not drawn
- Without a line limit the block shows as many lines as the rectangle's height holds, ending the last in `…` when text remains
- Lines are spaced by a fixed ratio of the step size (the same in every theme), and the block is aligned inside the rectangle horizontally
  (left, centre, right) and vertically (top, middle, bottom) as the widget chooses
- The theme's letter case and spacing apply as for a text line, before the text is measured
- The component reports the rectangle it occupies and the number of lines drawn, and whether text was cut, without drawing
  when asked only to measure
- Text size never changes to make text fit; a block that does not fit shows fewer lines

#### Scenario: Wrapped paragraph
- **WHEN** a block draws a sentence that needs three lines in a rectangle tall enough for four
- **THEN** three lines are drawn at the step's size, broken at spaces, and no ellipsis

#### Scenario: Line limit
- **WHEN** the same sentence is drawn with a limit of two lines
- **THEN** two lines are drawn and the second ends in `…`

#### Scenario: Word wider than the line
- **WHEN** a block draws a URL wider than the rectangle
- **THEN** it breaks between characters at the rectangle's width instead of overflowing

#### Scenario: Rectangle too short
- **WHEN** a block with no line limit is given a rectangle tall enough for two lines and text that needs five
- **THEN** two lines are drawn and the second ends in `…`

#### Scenario: Measure only
- **WHEN** a widget measures a block
- **THEN** it gets the occupied rectangle, the line count and whether text was cut, and nothing is drawn

### Requirement: Layout Is Cached
Text layout MUST NOT be recomputed for text, width, font and style it has already laid out. A widget that repaints with the
same text and the same rectangle width measures nothing.

- The cache is keyed by the text, the step, the width and the theme's resolved typeface and size for it, so a theme change
  or a different width lays out again and an unrelated repaint does not
- The cache is bounded; the least recently used layouts leave it first
- The cache holds no reference to a theme or a plugin's types beyond the call, so a plugin can be unloaded

#### Scenario: Repaint with the same text
- **WHEN** a block is drawn twice with the same text, step and width
- **THEN** the second draw performs no text measurement

#### Scenario: Theme changes
- **WHEN** the theme changes to one with another body typeface and the block is drawn again
- **THEN** the text is laid out again with the new typeface

### Requirement: Marquee
A single line wider than its rectangle MAY be drawn as a marquee, and a marquee MUST scroll sideways at the theme's size and loop.

- The line rests at its start for a moment, scrolls left at a constant speed until its end reaches the rectangle's right edge,
  rests there, then returns to the start and repeats. The pause and the speed are fixed by the SDK and not set by a theme in
  this version
- The position is a function of the time of the paint, never of a count of frames, so the motion is the same at any frame rate
- The text is clipped to the rectangle
- A line that fits does not move and does not ask for frames
- With the effective motion level `reduced` or `off` (see motion, "What Each Level Allows") the line is drawn at rest as
  an ellipsis line
- The component tells the widget whether it is moving, so that the widget can return it from `IsAnimating`

#### Scenario: Long line scrolls
- **WHEN** a marquee line is wider than its rectangle and the effective motion level is `full`
- **THEN** it rests at its start, then scrolls left, rests at its end, and repeats; its position depends only on time

#### Scenario: Line fits
- **WHEN** a marquee line fits its rectangle
- **THEN** it is drawn at rest and the component reports that it is not moving

#### Scenario: Reduced motion
- **WHEN** the effective motion level is `reduced` and the line is too wide
- **THEN** it is drawn at rest with an ellipsis and the component reports that it is not moving

#### Scenario: Frame rate
- **WHEN** two paints are made at the same time with different earlier frame counts
- **THEN** the line is in the same position

### Requirement: Text List
The SDK MUST provide a component that draws a list of text items in a rectangle, scrolled vertically and clipped to the
rectangle. The widget owns the component, creates it, feeds it items and releases it; it is used on the UI thread.

- An item has text, an optional muted second line, and an optional leading mark (a short text such as a user name drawn in a
  colour the widget picks); each item wraps to the rectangle's width and takes as many lines as it needs, with an optional
  line limit per item
- Items are separated by a gap that is a fixed ratio of the step size
- The widget can set the scroll offset in pixels, scroll by an amount, scroll to an item, and ask for the total height, the
  maximum offset and the item at a point
- An offset is clamped to the range of the content
- A partly visible item at the top or bottom is clipped, not dropped

#### Scenario: More items than fit
- **WHEN** a list holds twenty items and the rectangle shows five
- **THEN** the items from the offset on are drawn, the first and last visible one clipped at the rectangle's edge if cut

#### Scenario: Offset out of range
- **WHEN** a widget sets an offset beyond the end of the content
- **THEN** the list is drawn at the maximum offset

#### Scenario: Few items
- **WHEN** the items are shorter than the rectangle
- **THEN** they are drawn from the top (or the bottom, in follow mode) and the offset is zero

### Requirement: Follow Mode
A text list MAY be in follow mode, for a feed whose newest item is at the bottom. In follow mode, while the view is at the
bottom, adding items MUST keep the newest in view.

- The content scrolls up by the height of the added items over a short time, a function of the time of the paint; at the
  motion levels `reduced` and `off` it jumps
- Scrolling the list away from the bottom (by the widget) stops following; scrolling back to the bottom resumes it
- Items beyond the list's capacity, set by the widget, are removed from the top; the offset is adjusted so that the view does
  not jump
- Replacing an item's text (an edited message) keeps the view where it is
- The component tells the widget whether it is moving, to return from `IsAnimating`

#### Scenario: New message arrives
- **WHEN** a list at the bottom receives a new item
- **THEN** the content scrolls up so the new item is in view at the bottom

#### Scenario: User has scrolled back
- **WHEN** the list is scrolled away from the bottom and an item arrives
- **THEN** the view does not move

#### Scenario: Capacity reached
- **WHEN** the list holds its capacity and one more item is added
- **THEN** the oldest is removed and the visible items do not shift on screen

### Requirement: Rotating List
A text list MAY be in rotate mode, for a carousel of headlines. A rotating list MUST show one item, or the number of items
the widget asks for, and replace them with the next on a timer.

- The step is a time of the paint, so a widget that is not painted does not advance; the next item slides up over a short time
  (a jump at the motion levels `reduced` and `off`)
- The list reports when it will next change, so that the widget can return it from `NextAnimationAt` and the host paints it
  then without a data refresh
- Rotating stops while the list holds no more items than fit

#### Scenario: Headlines rotate
- **WHEN** a rotating list holds five headlines and shows one
- **THEN** each is shown for its interval and replaced by the next, after the last the first

#### Scenario: Everything fits
- **WHEN** the items fit the rectangle together
- **THEN** all are shown and nothing rotates

### Requirement: Item At A Point
A text list MUST report which item is under a point in the widget's pixels, or none, for the current offset, so that a widget
that accepts taps can act on the item (open its link).

#### Scenario: Tap on a headline
- **WHEN** the widget asks which item is at a point inside the third visible item
- **THEN** the list answers that item's index

#### Scenario: Tap in the gap
- **WHEN** the point is between two items or outside the rectangle
- **THEN** the list answers none

### Requirement: Text Layout Uses Only The Theme
Text layout components MUST draw with the theme's typefaces, sizes and colours and take no font, size in pixels or colour
of their own except the colour of a list item's leading mark. The widget draws no card, as for every component.

#### Scenario: Theme changes
- **WHEN** the active theme changes to one with other typefaces
- **THEN** every layout component draws in the new typefaces without a change to the widget

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

