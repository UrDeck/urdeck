## ADDED Requirements

### Requirement: Text Case
Readout labels and text lines MUST be drawn in the letter case the theme asks for. `typography.case.label` applies to
a readout's label and to a text line at the label step; `typography.case.title` applies to a text line at the title
step. A text line at the body step and a readout's value and unit are never recased. The change is made with invariant
casing, so it does not depend on the machine's language, and it is made before the text is measured, so shrinking to fit
and alignment act on the text as drawn. With `none`, or no value, the text is drawn as given.

#### Scenario: Upper-case labels
- **WHEN** the theme sets `typography.case.label` to `upper` and a widget draws a readout with the label `Cpu`
- **THEN** the label is drawn as `CPU`

#### Scenario: Body is never recased
- **WHEN** the theme sets `typography.case.label` and `typography.case.title` to `upper` and a widget draws a body text line `Sunny`
- **THEN** the line is drawn as `Sunny`

#### Scenario: Recased text still fits
- **WHEN** a title line is wider than its rectangle once it is upper-cased
- **THEN** it shrinks to fit the rectangle's width

### Requirement: Letter Spacing
Readout values and labels and text lines at the label and title steps MUST be drawn with the letter spacing the theme
asks for in `typography.tracking` (`value`, `label`, `title`), in em of the text's size, added after each character
except the last. A body line and a readout's unit are not spaced. A readout's digits keep equal widths with spacing
added; the readout's size is chosen with the spacing in the measured width, so the widest value still fits and the size
does not change as the value changes. With no spacing, or 0, the text is drawn and measured exactly as without this
requirement.

#### Scenario: Spaced label
- **WHEN** the theme sets `typography.tracking.label` to 0.2 and a widget draws the label `CPU` at 20 pixels
- **THEN** each pair of neighbouring letters is 4 pixels further apart than without the key, and the text is 8 pixels wider

#### Scenario: Digits stay equal
- **WHEN** the theme sets `typography.tracking.value` to 0.1 and a readout changes from `11:11` to `10:00`
- **THEN** the text size is unchanged and the colon stays in the same position

#### Scenario: Spacing is part of the fit
- **WHEN** the theme adds spacing to the value and the widest value no longer fits the rectangle at the old size
- **THEN** the value is drawn smaller so the widest value fits

#### Scenario: No spacing
- **WHEN** the theme sets no spacing
- **THEN** the drawn pixels are identical to those drawn before this requirement existed

### Requirement: Slanted Value
A readout's value MUST be drawn sheared by the angle in `typography.slant` (0 to 20 degrees), leaning to the right,
about the value's baseline. The unit and the label are not slanted. The readout's rectangle, alignment and size are
those of the unslanted value. With no slant, or 0, the value is drawn as without this requirement.

#### Scenario: Slanted digits
- **WHEN** the theme sets `typography.slant` to 10 and a readout draws `41`
- **THEN** the top of each digit is further right than its foot, the label below is upright, and the readout occupies the same rectangle as without the slant

### Requirement: Value Scale
A readout MUST multiply its size, as the widget asks for it, by `typography.valueScale` (0.5 to 2). The size is never
larger than the size at which the value, its unit and its label fill the rectangle, so a value above 1 enlarges only a
readout that a widget draws smaller than the rectangle allows, and a value below 1 shrinks every readout.

#### Scenario: Smaller than the rectangle
- **WHEN** the theme sets `typography.valueScale` to 0.8 and a widget draws a readout at full size
- **THEN** the value is drawn at 80 % of the size at which it fills the rectangle

#### Scenario: Restoring a reduced readout
- **WHEN** a widget draws a readout at 0.6 of the fitted size and the theme sets `typography.valueScale` to 1.25
- **THEN** the value is drawn at 0.75 of the fitted size

#### Scenario: Never beyond the rectangle
- **WHEN** a widget draws a readout at full size and the theme sets `typography.valueScale` to 1.35
- **THEN** the value is drawn at the size at which it fills the rectangle

### Requirement: Value Fill
A readout's value MUST be filled with the gradient in `typography.valueFill` (two to six colours, evenly spaced, at the
given angle in degrees across the value's bounding box) instead of the text colour. A value that is not current is
drawn in the muted colour without the gradient, and a colour the widget gives the value explicitly replaces the
gradient. The unit and the label are not filled. The fill does not move.

#### Scenario: Gradient digits
- **WHEN** the theme sets `typography.valueFill` to a gradient from magenta to cyan at 0 degrees and a readout draws `41`
- **THEN** the left of the digits is magenta, the right is cyan, and the unit and the label use their usual colours

#### Scenario: Stale reading
- **WHEN** a readout draws a formatted reading that is not current while a value fill is set
- **THEN** the value is drawn in the muted text colour with no gradient

#### Scenario: Widget colour wins
- **WHEN** a widget gives its readout an explicit value colour while a value fill is set
- **THEN** the value is drawn in that colour

### Requirement: Value Glow
A readout's value MUST be drawn over a blurred copy of itself when the theme sets `typography.valueGlow`: the copy is
blurred by `blur` (a fraction of a grid cell, resolved to pixels for the surface), in `color` (absent: the value's own
colour, or the gradient when there is one), at `alpha` strength. The glow is drawn behind the value and does not change
the readout's rectangle. A blurred copy is kept for as long as the text, size and colour do not change, so a card that
repaints without a new value does not blur again.

#### Scenario: Glowing digits
- **WHEN** the theme sets `typography.valueGlow` with a blur of 0.1 and an alpha of 0.4 and a readout draws `41`
- **THEN** a soft halo of the value's colour surrounds the digits, and the digits are drawn sharp over it

#### Scenario: Same value, no new blur
- **WHEN** a readout is drawn twice with the same value, size and colour
- **THEN** the glow is blurred once

#### Scenario: Glow does not move the layout
- **WHEN** a glow is set
- **THEN** the readout's rectangle is the same as without it
