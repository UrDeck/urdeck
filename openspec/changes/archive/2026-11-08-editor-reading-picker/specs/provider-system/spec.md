## ADDED Requirements

### Requirement: Listing System Readings
The provider MUST be able to list its concrete readings on this machine (see data-providers, "Listing Concrete
Readings"): the whole CPU's load and each logical processor's, memory, and each hardware GPU adapter's readings, with
the same descriptions they have when shown and the categories "CPU", "Memory" and "GPU". Listing MUST NOT start
sampling and MUST answer within a second.

#### Scenario: Browse system readings
- **WHEN** the readings of `system` are listed on a PC with 16 logical processors and one GPU
- **THEN** the list has the CPU load, 16 core loads, the memory load and that GPU's readings, each with its category
