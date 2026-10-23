## ADDED Requirements

### Requirement: First-Party Providers Carry Samples
Every first-party data provider MUST carry a sample file covering every reading it offers (see `sample-data`). A
third-party provider MAY carry one; without it, its readings are unavailable in previews and it runs normally in the
host's demo mode. The documentation for provider authors MUST describe the sample file and how to check it.

#### Scenario: The first-party providers
- **WHEN** the system and weather providers are built
- **THEN** each assembly carries a sample file that the sample checks accept

#### Scenario: A third-party provider without a sample
- **WHEN** the host runs in demo mode with a third-party provider that has no sample
- **THEN** that provider starts and samples as usual, and the first-party providers are replaced by their samples
