# Provider System Specification

## Purpose

Defines the first-party `system` provider: the readings about the local machine that UrDeck offers without
administrator rights, under ids that mean the same on every PC.

## Requirements

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
- GPU readings MUST NOT be collected while no GPU reading is subscribed to, and each kind of GPU reading (load,
  temperature, power and clock) MUST be collected only for adapters on which it is subscribed to
- A vendor library MUST NOT be loaded before a reading that needs it is subscribed to

#### Scenario: Only memory is wanted
- **WHEN** the only subscribed reading is `system:memory/load`
- **THEN** the provider does not query processor times

#### Scenario: No GPU reading is wanted
- **WHEN** the subscribed readings are CPU and memory readings only
- **THEN** the provider makes no GPU query and loads no vendor library

#### Scenario: Only GPU load is wanted
- **WHEN** the only subscribed GPU reading is `system:gpu/load`
- **THEN** the provider does not query the temperature and loads no vendor library

### Requirement: GPU Load And Temperature Readings
The provider MUST offer the load and the temperature of each GPU adapter of the machine, for adapters of any vendor,
without administrator rights.

| Path | Meaning | Kind | Default label | Full name |
|---|---|---|---|---|
| `gpu/load` | busiest engine of the main adapter | percentage, 0 to 100 | `GPU` | `GPU load` |
| `gpu/temperature` | temperature of the main adapter | temperature | `GPU` | `GPU temperature` |
| `gpu/<n>/load` | as `gpu/load`, for adapter `n` | percentage, 0 to 100 | `GPU <n>` | `GPU <n> load` |
| `gpu/<n>/temperature` | as `gpu/temperature`, for adapter `n` | temperature | `GPU <n>` | `GPU <n> temperature` |

- The load is the highest share of time any one of the adapter's engines was busy since the previous sample, which is
  the number Windows Task Manager shows for the adapter
- The temperature's description declares a range of 0 to 100, a warning value of 80, a critical value of 90 and
  Celsius as the default display unit; the load's description declares no warning or critical value
- The device name of a GPU reading is the adapter's name as Windows reports it
- When the adapter's driver reports no temperature, the temperature reading MUST be unavailable with a reason, and
  MUST NOT show zero
- An adapter number that does not exist on the machine MUST be unavailable

#### Scenario: GPU load
- **WHEN** a widget shows `system:gpu/load` while a game is running
- **THEN** the value is a percentage between 0 and 100 that is close to the GPU percentage Task Manager shows

#### Scenario: GPU temperature
- **WHEN** a widget shows `system:gpu/temperature` on a machine whose GPU driver reports a temperature
- **THEN** the value is the adapter's temperature, shown in Celsius unless the widget asks for another unit

#### Scenario: Temperature levels reach a gauge
- **WHEN** a gauge shows `system:gpu/temperature` at 85 degrees with no level set in the widget
- **THEN** the gauge is at the warning level

#### Scenario: Driver reports no temperature
- **WHEN** the adapter's driver does not report a temperature
- **THEN** the reading is unavailable and the widget shows a dash

#### Scenario: No elevation
- **WHEN** a widget shows a GPU reading and the host runs as a standard user
- **THEN** no elevation prompt appears

#### Scenario: Config moved to another PC
- **WHEN** a page that shows `system:gpu/load` is moved from a PC with an NVIDIA GPU to one with an AMD GPU
- **THEN** the reading works without any change to the page

### Requirement: GPU Adapter Selection
The provider MUST list one group of GPU readings per hardware GPU adapter and choose a main adapter for the paths
that name none.

- Software adapters MUST NOT be listed
- Adapters are numbered from 1 in the order of their dedicated video memory, largest first; adapters with equal
  memory keep the order in which Windows lists them
- The main adapter is adapter 1
- A machine without a hardware GPU adapter has no GPU readings in its catalog, and every GPU path is unavailable

#### Scenario: Integrated and discrete GPU
- **WHEN** a machine has an integrated GPU and a discrete GPU with more dedicated video memory
- **THEN** `system:gpu/load` and `system:gpu/1/load` are the discrete GPU's load and `system:gpu/2/load` is the
  integrated GPU's

#### Scenario: Adapter that does not exist
- **WHEN** a widget shows `system:gpu/3/load` on a machine with two adapters
- **THEN** the reading is unavailable

#### Scenario: No GPU
- **WHEN** a machine has only a software adapter
- **THEN** the catalog lists no GPU readings and `system:gpu/load` is unavailable

### Requirement: GPU Power And Clock Readings
The provider MUST offer the power draw and the core clock of each GPU adapter under ids that do not depend on the
vendor, and MUST supply their values where the vendor's own library is supported.

| Path | Meaning | Kind | Default label | Full name |
|---|---|---|---|---|
| `gpu/power` | power the main adapter draws | plain number, unit `W` | `GPU` | `GPU power` |
| `gpu/clock` | current core clock of the main adapter | plain number, unit `MHz` | `GPU` | `GPU clock` |
| `gpu/<n>/power`, `gpu/<n>/clock` | the same for adapter `n` | as above | `GPU <n>` | `GPU <n> power`, `GPU <n> clock` |

- The readings are in the catalog for every hardware adapter, whatever its vendor
- NVIDIA adapters are supported through the library the NVIDIA driver installs; the provider MUST NOT require any
  other software to be installed
- On an adapter whose vendor library is not supported, is not installed or fails to start, both readings MUST be
  unavailable with a reason that says so, and the other GPU readings MUST keep working
- A machine with several adapters of a supported vendor MUST read each adapter's own values
- The readings MUST work without administrator rights

#### Scenario: NVIDIA GPU
- **WHEN** a widget shows `system:gpu/power` and `system:gpu/clock` on a machine with an NVIDIA GPU and its driver
- **THEN** it shows the power in watts and the clock in MHz

#### Scenario: Vendor without a supported library
- **WHEN** a widget shows `system:gpu/power` on a machine whose main adapter is an AMD or Intel GPU
- **THEN** the reading is unavailable, the widget shows a dash, and `system:gpu/load` on the same page has a value

#### Scenario: Library missing
- **WHEN** the main adapter is an NVIDIA GPU and the vendor library cannot be loaded
- **THEN** power and clock are unavailable with a logged reason and the provider keeps supplying its other readings

#### Scenario: Full performance card
- **WHEN** a 4x4 stats widget shows CPU load, GPU temperature and GPU load as gauges and memory load, GPU power and
  GPU clock as text stats on a machine with an NVIDIA GPU
- **THEN** all six have values
