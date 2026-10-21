## MODIFIED Requirements

### Requirement: Findings About Rules Between Values
The rules between values that the deck applies when it loads a theme MUST each be a finding with its own code:

| Code | Severity | Meaning |
|---|---|---|
| `THEME050` | warning | a border style, gradient or animation with a card border width of 0 |
| `THEME051` | warning | `borderAnimate` `spin` without a conic `borderGradient`; the deck switches the animation off |
| `THEME052` | warning | a pattern whose marks are closer than three pixels at the panel width |
| `THEME053` | warning | outside corner marks reaching past the gap, counting the mark's thickness |
| `THEME054` | warning | a shadow or glow whose reach is more than the gap over a translucent card fill; the deck limits it |
| `THEME055` | warning | a line (a border, highlight, accent strip or corner mark) that resolves under one pixel; the deck draws it 1 px wide |
| `THEME056` | info | `card.compact.shape` `circle` on a card that is not square, which is drawn as a capsule |
| `THEME057` | warning | `borderAnimate` (`spin` or `pulse`) on a `dashed` border; the deck draws the border still |
| `THEME090` | info | more than one continuous animation is on |

- `THEME054` MUST use the reach the deck uses when it draws, so the tool and the picture agree
- An `info` finding never changes the exit code

#### Scenario: Style without a border
- **WHEN** `card.borderStyle` is `dashed` and `card.borderWidth` is 0
- **THEN** a `THEME050` warning says the style has no effect

#### Scenario: Reach into the neighbour
- **WHEN** a translucent card fill has a shadow whose reach is larger than `card.gap`
- **THEN** a `THEME054` warning gives the reach, the gap and the reach the deck will use

#### Scenario: Several animations
- **WHEN** a theme turns on two continuous animations
- **THEN** a `THEME090` information finding says each repaints on every frame, and no warning is made

#### Scenario: Animated dashed border
- **WHEN** `card.borderStyle` is `dashed` and `card.borderAnimate.mode` is `pulse`
- **THEN** a `THEME057` warning says the border is drawn still
