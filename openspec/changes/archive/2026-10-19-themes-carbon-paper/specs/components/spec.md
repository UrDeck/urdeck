## ADDED Requirements

### Requirement: Gauge Readout Gap
A theme MAY set `gauge.readoutGap` and the gauge MUST apply it: the space between the track of a bar, meter or thick bar and its readout, in
thicknesses of the track. The default is 0.5; the value is a number from 0 to 4. A theme that does not set it draws
the gap it drew before the key existed.

#### Scenario: A wider gap
- **WHEN** a stacked bar is drawn under a theme whose `gauge.readoutGap` is 1.5, and again with the default
- **THEN** the readout ends one track thickness higher than with the default, and the track is drawn where it was

#### Scenario: A value out of range
- **WHEN** a theme sets `gauge.readoutGap` to 40
- **THEN** the value is ignored with one logged warning and the default gap applies
