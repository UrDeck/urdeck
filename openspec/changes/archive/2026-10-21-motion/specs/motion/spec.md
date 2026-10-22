## Purpose

Decides how much the deck may move: one effective motion level made from the user's `motion` setting, Windows' animation
setting and the theme's ceiling, so that every moving part of the deck honours a wish for less motion in the same way.

## ADDED Requirements

### Requirement: Effective Motion Level
The deck MUST decide one effective motion level, `full`, `reduced` or `off` (in that order, from most to least motion),
from three inputs:

- The user's choice: the configuration's `motion` value when it is set to `full`, `reduced` or `off`. When it is absent
  (or not one of those values) the choice is Windows': `off` when Windows' "Animation effects" setting is off, `full` when
  it is on
- The theme's ceiling: its `motion.level`, `full` when the theme sets none
- The effective level is the lower of the user's choice and the theme's ceiling

A `motion` value that the user set always wins over Windows' setting, in both directions: `"motion": "full"` lets a deck
move on a machine with Windows' animations off.

#### Scenario: Nothing set
- **WHEN** the configuration has no `motion`, Windows' animation effects are on and the theme sets no `motion.level`
- **THEN** the effective level is `full`

#### Scenario: Windows animations off
- **WHEN** the configuration has no `motion` and Windows' animation effects are off
- **THEN** the effective level is `off`, whatever the theme sets

#### Scenario: User overrides Windows
- **WHEN** the configuration has `"motion": "full"`, Windows' animation effects are off and the theme's ceiling is `full`
- **THEN** the effective level is `full`

#### Scenario: Theme ceiling
- **WHEN** the configuration has `"motion": "full"` and the theme sets `motion.level` to `reduced`
- **THEN** the effective level is `reduced`

#### Scenario: User reduces
- **WHEN** the configuration has `"motion": "reduced"` and the theme's ceiling is `full`
- **THEN** the effective level is `reduced`

### Requirement: Motion Level Follows Its Inputs
The deck MUST re-evaluate the effective level whenever one of its inputs changes, without a restart:

- An edit of `motion` in the configuration (with the configuration's hot reload)
- A theme change
- A change of Windows' "Animation effects" setting while the deck runs

When the effective level changes, everything the level governs follows it (see `widget-card`, "Animated Border", and
`backgrounds`, "Reduced Motion Stills The Background"). When it does not change, nothing is rebuilt or restarted.

#### Scenario: Windows setting switched while running
- **WHEN** the deck runs with no `motion` in its configuration and an animated border turning, and the user turns
  Windows' animation effects off
- **THEN** within a second the border stands still, without a restart

#### Scenario: Unrelated save
- **WHEN** the configuration is saved with only a widget's setting changed
- **THEN** the effective level is not logged again and no animation restarts

### Requirement: What Each Level Allows
In this version the levels MUST mean:

- `full`: the theme's continuous animations run (the animated border) and a moving background moves
- `reduced` and `off`: no continuous animation runs; each is drawn as it is when still. The two levels behave the same here;
  `reduced` is kept for one-off motions (such as a page slide) that a later change may keep at `reduced` and stop at `off`
- The page slide, the press feedback and a widget's own motion settings (the Weather card's `motion`) are not governed by
  the level in this version

#### Scenario: Reduced level
- **WHEN** the effective level is `reduced` and the theme has an animated border
- **THEN** the border is drawn still, and a page can still be swiped with its slide

### Requirement: Motion Level Is Logged
The deck MUST log one information line whenever the effective level is first decided and whenever it changes, giving the
level and what decided it: the configuration, Windows' animation setting, or the theme's ceiling.

#### Scenario: Decided by Windows
- **WHEN** the deck starts with no `motion` in its configuration and Windows' animation effects off
- **THEN** the log has one line saying the motion level is `off`, from Windows' animation setting

#### Scenario: Decided by the theme
- **WHEN** the configuration has `"motion": "full"` and the theme's `motion.level` is `reduced`
- **THEN** the log says the motion level is `reduced`, from the theme
