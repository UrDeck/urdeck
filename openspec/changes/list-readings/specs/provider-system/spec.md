## ADDED Requirements

### Requirement: Top Processes Reading
The provider MUST offer `system:processes/top`, a list reading of the processes using the most processor time since the
previous sample, at most ten, most first. Each row is keyed by the process id and has `name` (the process's executable
name without its extension; the title), `cpu` (percent of the whole processor, one decimal; the status) and `memory`
(working set in megabytes, whole; the detail). The list's unit is "processes" and its empty text "No busy processes".
Processes are enumerated only while the reading is subscribed (see "System Provider Cost"); the first sample after a
subscription publishes nothing, because a share needs two samples. Process names are never written to the log. The
provider's sample file carries a fixed list of fictional processes.

#### Scenario: A busy game
- **WHEN** a game uses 35 % of the processor and nothing else uses more than 2 %
- **THEN** the first row is the game's name with `cpu` 35.0, and the list has at most ten rows

#### Scenario: Not shown
- **WHEN** no widget shows `system:processes/top`
- **THEN** the provider does not enumerate processes

#### Scenario: Process ends between samples
- **WHEN** a process listed in one sample has exited by the next
- **THEN** its row is gone from the next list and nothing is logged
