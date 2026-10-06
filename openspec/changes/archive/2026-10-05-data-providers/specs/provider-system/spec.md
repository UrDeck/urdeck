## Purpose

Defines the first-party `system` provider: the readings about the local machine that UrDeck offers without
administrator rights, under ids that mean the same on every PC.

## ADDED Requirements

### Requirement: System Provider Identity
UrDeck MUST ship a provider with the id `system` as a plugin in `plugins/`. It MUST reference only the widget SDK, and
MUST work without administrator rights, a kernel driver or a helper process. Its default sampling interval is 2000
milliseconds and its minimum is 250 milliseconds.

#### Scenario: Present in a default install
- **WHEN** the host starts with its shipped plugins
- **THEN** a provider with the id `system` is registered

#### Scenario: No elevation
- **WHEN** a widget uses a `system` reading and the host runs as a standard user
- **THEN** the reading has a value and no elevation prompt appears

### Requirement: CPU Load Readings
The provider MUST offer the load of the whole CPU and of each logical processor as percentages from 0 to 100,
measured over the time since the previous sample.

| Path | Meaning | Default label | Full name |
|---|---|---|---|
| `cpu/load` | all logical processors together | `CPU` | `CPU load` |
| `cpu/core/<n>/load` | logical processor `n`, counted from 1 | `Core <n>` | `CPU core <n> load` |

- The catalog MUST list one `cpu/core/<n>/load` entry per logical processor of the machine
- The device name of the CPU readings is the processor's name as Windows reports it
- A core number that does not exist on the machine MUST be unavailable

#### Scenario: Total load
- **WHEN** a widget shows `system:cpu/load` on a busy machine
- **THEN** the value is a percentage between 0 and 100 that rises and falls with the machine's CPU use

#### Scenario: Per-core cards
- **WHEN** four widgets show `system:cpu/core/1/load` to `system:cpu/core/4/load`
- **THEN** each shows the load of its own logical processor and their default labels are `Core 1` to `Core 4`

#### Scenario: Core that does not exist
- **WHEN** a widget shows `system:cpu/core/99/load` on a machine with 16 logical processors
- **THEN** the reading is unavailable

#### Scenario: Config moved to another PC
- **WHEN** a page that shows `system:cpu/load` is used on a different machine
- **THEN** the reading works without any change to the page

### Requirement: Memory Load Reading
The provider MUST offer `memory/load`: the share of physical memory in use, as a percentage from 0 to 100, with the
default label `Memory` and the full name `Memory load`.

#### Scenario: Memory load
- **WHEN** a widget shows `system:memory/load`
- **THEN** the value is the percentage of physical memory in use

### Requirement: System Provider Cost
The provider MUST do only the work its subscribed readings need, and MUST do nothing while stopped.

- A sample MUST NOT allocate per reading on a steady state beyond what publishing the values needs
- Memory readings MUST NOT be collected while only CPU readings are subscribed to, and the reverse

#### Scenario: Only memory is wanted
- **WHEN** the only subscribed reading is `system:memory/load`
- **THEN** the provider does not query processor times
