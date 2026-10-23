## ADDED Requirements

### Requirement: Demo Mode
The host MUST accept `--demo`, alone or with `--snapshot`. In demo mode every data provider that carries a sample is
replaced by its sample (see `sample-data`, "A Sample Replaces Its Provider"), and the host makes no network request of
its own: a shortcut that points to a site draws its fallback instead of fetching the site's icon. Everything else is as
without `--demo`: the configuration, the theme, the background, the motion level, the window and its placement. Demo mode
never writes the configuration. The log MUST say once at startup that the deck runs on sample data and which providers
were replaced.

The sample readings are taken at the moment drawn, so in a live deck they change with time as their keyframes say, and
a snapshot with `--start` shows the readings of that moment.

#### Scenario: A deck on sample data
- **WHEN** the host is started with `--demo` and a configuration with a stats card and a weather card
- **THEN** the cards show the samples' readings, the log names the replaced providers, and no request is made to the
  forecast service

#### Scenario: A deterministic snapshot
- **WHEN** `--demo --snapshot out.png --size 1100x3840 --start 2026-10-13T21:47:32` is run twice, on two Windows machines
- **THEN** both pictures show the same readings, the same time and the same weather

#### Scenario: Without demo mode
- **WHEN** the host is started without `--demo`
- **THEN** every provider runs as before
