## Purpose

Made-up but plausible readings that a data provider carries with it, so a page can be drawn the same way on any machine
without sensors, network or personal data: for previews, pictures, tests and the host's demo mode.

## ADDED Requirements

### Requirement: Provider Sample File
A data provider MAY carry a sample file. The sample file MUST travel inside the provider's assembly (not as a loose
file), so that it is found wherever the provider is. It is a JSON document with:

- `version`: `1`
- `readings`: an object whose keys are reading paths below the provider (`cpu/load`, `{location}/current/temperature`)
  and whose values describe each reading with the fields of a reading's description (kind, label, name, and where they
  apply device, unit, display unit, minimum, maximum, warning and critical levels) and give its value (see "Sample
  Values Over Time")

The file describes its readings itself, rather than borrowing the provider's own catalog, because a provider's catalog
may depend on the machine (the number of processor cores, the graphics adapters) or on the operating system. A sample
file that cannot be read MUST be reported once, with the provider's name and the reason, and the provider then has no
sample; nothing else is affected.

#### Scenario: A provider with a sample
- **WHEN** the system provider's assembly is loaded
- **THEN** its sample file is found inside it, without any file next to the assembly

#### Scenario: A broken sample
- **WHEN** a provider's sample file is not valid JSON
- **THEN** one warning names the provider and the reason, the provider has no sample, and other providers' samples work

### Requirement: Sample Values Over Time
Each sample reading MUST have a value given in one of two ways:

- A fixed value: a number, a text, an on/off value, a time or a date, according to the reading's kind
- Keyframes: a list of times in seconds from the start of a drawing, each with a value, and whether the list repeats.
  Between two keyframes a number changes in a straight line; a text, an on/off value, a time or a date holds the earlier
  keyframe's value until the next one. After the last keyframe the value holds, or the list starts again when it repeats

The value at the start of a drawing (time 0) MUST be the first keyframe's value, so a still picture and the first frame of
a moving one agree. Before time 0 (the history a preview fills before its first frame) a repeating list continues
backwards, and a list that does not repeat holds its first value. A sample reading MAY instead be given as unavailable with a reason, to show how a widget draws a
missing reading.

#### Scenario: A fixed value
- **WHEN** a reading's sample is the number 68
- **THEN** it reads 68 at every moment of a drawing

#### Scenario: A gauge that moves
- **WHEN** a reading's keyframes are 41 at 0 s, 68 at 2 s and 55 at 4 s, not repeating
- **THEN** it reads 41 at 0 s, 54.5 at 1 s, 68 at 2 s and 55 from 4 s on

#### Scenario: A repeating text
- **WHEN** a text reading's keyframes are `partly-cloudy` at 0 s and `rain` at 3 s, repeating with a length of 6 s
- **THEN** it reads `partly-cloudy` at 7 s and `rain` at 10 s

### Requirement: Readings Of A Place
A sample reading's path MAY hold a placeholder in braces (`{location}`) where the provider's own paths hold a value the
user chooses. Such a sample MUST answer for every value: `weather:Zurich/current/temperature` and
`weather:Portland, OR/current/temperature` both read the sample of `{location}/current/temperature`. A text value MAY
contain the same placeholder, which is replaced by the value the path was asked with, so a place's name reads back as
the place asked for.

#### Scenario: Any place
- **WHEN** a page asks for `weather:Zurich/current/temperature` and the weather sample has `{location}/current/temperature`
  at 18
- **THEN** the reading is 18

#### Scenario: The place's name
- **WHEN** the sample of `{location}/place` is the text `{location}` and a page asks for `weather:Zurich/place`
- **THEN** the reading is the text `Zurich`

### Requirement: Times And Dates Relative To The Drawing
A sample time MUST be given as a time of day (`07:14`) and a sample date as a number of days from the day drawn (`0` is
that day, `1` the next). They are resolved against the moment being drawn, so a forecast drawn on any day starts with that
day and a sunrise is that day's sunrise.

#### Scenario: Tomorrow's forecast
- **WHEN** a sample date reading is `1` and the page is drawn on 13 October 2026
- **THEN** the reading is 14 October 2026

#### Scenario: Sunrise
- **WHEN** a sample time reading is `07:14` and the page is drawn on 13 October 2026
- **THEN** the reading is 13 October 2026, 07:14 local time

### Requirement: A Sample Replaces Its Provider
When sample data is asked for (a preview, the tool, a test, or the host's demo mode), every provider that has a sample
MUST be replaced by it, under the provider's own id, and readings MUST reach widgets through the same path real readings
take: the same subscriptions, the same reading states, the same formatting and units. A replaced provider is never
started: it reads no sensor and makes no network request. A provider without a sample stays as it is in the host's demo
mode and is absent from a preview, where its readings are unavailable.

A sample's readings MUST be a pure function of the moment drawn: the same moment gives the same values, whatever came
before and on whatever machine.

#### Scenario: Weather without the network
- **WHEN** a page with a weather card is drawn with sample data
- **THEN** the card shows the sample's conditions and no request is made to the forecast service

#### Scenario: The same moment twice
- **WHEN** a page is drawn twice with sample data at the same moment
- **THEN** every reading has the same value both times

#### Scenario: A provider without a sample in a preview
- **WHEN** a preview draws a widget whose readings come from a provider that has no sample
- **THEN** those readings are unavailable and the widget draws them as it draws any unavailable reading

### Requirement: Samples Cover Their Provider
A first-party provider's sample MUST cover every reading the provider can offer:

- For a provider whose catalog does not depend on the machine, every path of its catalog has a sample with the same kind,
  labels, unit and range
- For a provider whose catalog depends on the machine (numbered cores or adapters), every family of paths has a sample
  with the same description (kind, label, name, unit and range), for a plausible machine (at least eight cores and one graphics adapter
  with every reading an adapter can have)

The repository's tests MUST check this, on the platform where the provider's catalog can be read.

#### Scenario: A new weather reading
- **WHEN** the weather provider starts offering a new reading and its sample is not extended
- **THEN** a test fails, naming the reading

#### Scenario: A label that drifted
- **WHEN** the system provider's label for `cpu/load` changes and the sample still has the old one
- **THEN** a test fails, naming the reading and both labels
