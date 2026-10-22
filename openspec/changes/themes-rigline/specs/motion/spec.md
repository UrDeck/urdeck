## ADDED Requirements

### Requirement: A Built-in Theme May Animate
`rigline` MUST set a conic card border with a `spin` animation and a `motion.level` of `full`, and the deck MUST govern it
with the effective motion level like any animated border.

- At `full` the border spectrum turns round every card, page cards and compact cards alike
- At `reduced` and `off` the border is drawn still, and the theme then costs what its still form costs
- With the display off or the deck hidden, the border is paused

#### Scenario: Rigline at full
- **WHEN** `rigline` is selected and the effective motion level is `full`
- **THEN** the spectrum travels round the edge of every card, including 1x1 tiles and dock slots

#### Scenario: Rigline at reduced or off
- **WHEN** the effective level is `reduced` or `off`
- **THEN** the border stands still at its start angle and no animation runs

#### Scenario: Paused while unseen
- **WHEN** the display is off or the deck is hidden under `rigline` at `full`
- **THEN** the border is paused and resumes when the deck is shown again
