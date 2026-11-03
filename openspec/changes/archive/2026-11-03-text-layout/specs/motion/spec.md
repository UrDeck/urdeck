## MODIFIED Requirements

### Requirement: What Each Level Allows
In this version the levels MUST mean:

- `full`: the theme's continuous animations run (the animated border), a moving background moves, a text marquee scrolls
  and a text list scrolls and rotates with motion (see text-layout)
- `reduced` and `off`: no continuous animation runs; each is drawn as it is when still. The two levels behave the same here;
  `reduced` is kept for one-off motions (such as a page slide) that a later change may keep at `reduced` and stop at `off`.
  A marquee line is drawn at rest with an ellipsis, and a text list shows its new content at once, without the scroll or
  the slide; a list still follows its newest item and still rotates on its timer
- The page slide, the press feedback and a widget's own motion settings (the Weather card's `motion`) are not governed by
  the level in this version

#### Scenario: Reduced level
- **WHEN** the effective level is `reduced` and the theme has an animated border
- **THEN** the border is drawn still, and a page can still be swiped with its slide

#### Scenario: Marquee at reduced motion
- **WHEN** the effective level is `reduced` and a marquee line is wider than its rectangle
- **THEN** the line stands still and ends in `…`, and no frames are requested for it

#### Scenario: Chat feed at reduced motion
- **WHEN** the effective level is `off` and a message arrives in a following list
- **THEN** the new message appears at once at the bottom, without a scroll
