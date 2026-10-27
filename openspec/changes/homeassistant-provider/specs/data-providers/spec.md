## MODIFIED Requirements

### Requirement: Reading States
Every reading a widget asks for MUST be in exactly one of four states:

- **ok**: the value comes from the provider's most recent successful sample or publication
- **pending**: the reading is subscribed to and its provider has not yet reported on it since the subscription began
- **stale**: the provider's most recent sample failed, timed out, or has not reported this reading in its last three
  samples, or the provider reported an interruption and has not published the reading since (see "Interruptions"), and
  an earlier value exists
- **unavailable**: the provider reported the reading as unavailable, the provider or the reading does not exist, or
  the provider failed or was interrupted and no earlier value exists

A reading that is pending or stale MUST still expose its last known value when one exists. A reading that is
unavailable MUST carry a reason. The reason for a reading becoming unavailable or stale MUST be logged once per
change of state, not once per sample.

#### Scenario: Unknown provider
- **WHEN** a widget asks for `nosuch:value` and no provider `nosuch` is registered
- **THEN** the reading is unavailable and the log names the missing provider once

#### Scenario: Unknown reading of a known provider
- **WHEN** a widget asks for a path that is not in the provider's catalog
- **THEN** the reading is unavailable and the log names the reading once

#### Scenario: Provider reports a reading as unavailable
- **WHEN** a provider reports that a reading cannot be supplied, with a reason
- **THEN** the reading is unavailable and carries that reason

#### Scenario: Sample fails after earlier success
- **WHEN** a provider's sample throws after an earlier sample succeeded
- **THEN** its subscribed readings become stale and keep their last value

#### Scenario: Pushed provider interrupted
- **WHEN** a pushed provider reports an interruption while two of its readings are shown, one with a value and one
  never published
- **THEN** the first is stale with its last value, the second is unavailable with the interruption's reason

## ADDED Requirements

### Requirement: Interruptions
A provider MUST be able to report that it is interrupted (its connection to its source is lost), with a reason. The hub
then treats every subscribed reading of that provider as after a failed sample: stale with its last value, or unavailable
with the reason when it has none. A later publication of a reading makes it ok again. Reporting an interruption twice
changes nothing and logs nothing new.

#### Scenario: Reconnected
- **WHEN** a provider reports an interruption and later publishes a reading again
- **THEN** that reading is ok with the new value, and readings not yet published stay stale

### Requirement: Descriptions Refined While Running
A provider MUST be able to describe a concrete reading while it runs, when the reading's path is matched by one of its
catalog's patterns. From then on a widget that asks for that reading's description receives the refined one instead of
the pattern's; describing it again replaces it, and the widgets showing the reading are redrawn. The hub keeps a refined
description only while the reading is subscribed (or in the provider's listing; see "Listing Concrete Readings"), so a
provider cannot grow memory with paths nobody shows. A refined description for a path that matches no pattern is ignored
and logged once.

#### Scenario: Unit learned at runtime
- **WHEN** the catalog has the pattern `{entity}` described as text, a widget shows `sensor.office_temperature`, and the
  provider then describes it as a temperature with the label "Office temperature"
- **THEN** the widget is redrawn and shows a temperature labelled "Office temperature"

#### Scenario: Unit changed
- **WHEN** a refined reading is described again with another kind
- **THEN** the widget receives the new description and is redrawn

### Requirement: Natural Action And Icon
A description MAY name the reading's natural action, a path of an action of the same provider (see provider-actions),
and MAY name a canonical icon key (see icon-sets). A widget that lets the user act on a reading without naming an action
MUST use the natural action; a widget that shows an icon for a reading without naming one MUST use the description's icon. A
description without them means the reading has no natural action and no icon of its own.

#### Scenario: A light's natural action
- **WHEN** a widget reads the description of `homeassistant:light.desk`
- **THEN** the description names the action `light.desk/toggle` and the icon `home/light`

### Requirement: Listing Concrete Readings
A provider whose patterns stand for a finite set it can enumerate MAY offer to list its concrete readings: a description
for each, with no values. Listing starts the provider if it is not running (as an action does) and lingers it afterwards;
a provider that offers no listing, or whose listing fails, answers with a message and no list. The descriptions it lists
MUST be the same as those it would give when the readings are shown.

#### Scenario: A provider without a listing
- **WHEN** the readings of `weather` are listed
- **THEN** the answer says the provider cannot list its readings
