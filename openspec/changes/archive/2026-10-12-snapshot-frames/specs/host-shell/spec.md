## ADDED Requirements

### Requirement: Snapshot Frames
`--snapshot` with `--frames N` MUST treat its output as a folder and write N pictures of the page named `frame-0000.png`
onwards, the picture `i` drawn at the time `--start` plus `i` divided by `--fps`. `--start` is a local date and time and
defaults to now; `--fps` defaults to 30 and MUST be above zero; N MUST NOT be negative. The widgets MUST be the same
instances for every frame, so that an animation that depends on the time moves from one picture to the next. The
background stays at its snapshot time. Without `--frames` a snapshot is one picture, as before.

#### Scenario: A split-flap clock across a minute
- **WHEN** the host is run with `--snapshot out --frames 90 --start 2026-10-12T20:13:58.5` and the page has a clock in
  its `flap` style
- **THEN** `out` holds 90 pictures, and the digit of the minute is flipping in the pictures around the 1.5 second mark
  and at rest in the first and the last

#### Scenario: Invalid frame options
- **WHEN** the host is run with `--snapshot out --frames 10 --fps 0`
- **THEN** nothing is written, the log says why and the exit code is 1
