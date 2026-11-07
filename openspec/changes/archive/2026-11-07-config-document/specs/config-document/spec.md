## Purpose

The configuration as one document in the running host: every change to it, from the editor, the host or the file,
goes through it, everything in the process sees the change at once, and the file is written behind it without losing
anything the host does not understand.

## ADDED Requirements

### Requirement: Edits By Path
The host MUST change the configuration only through the configuration document, as transactions of edits that address
values by path (setting, removing, inserting into a list, moving within a list). Each transaction has a label and an
origin: the user (through the editor), the host itself, or the file changing outside.

- Setting a value to nothing removes the key; the document never writes a JSON `null`
- A transaction is applied whole or not at all
- Keys the host does not understand (settings of a widget whose plugin is not installed, keys of a newer version, keys
  a plugin reads) are kept exactly as they are unless an edit addresses them

#### Scenario: Unknown keys survive
- **WHEN** the configuration has a widget of type `acme.widgets.ticker`, whose plugin is not installed, with a key
  `symbols`, and the user changes another widget's label
- **THEN** the written file still has the ticker widget with `symbols` unchanged

#### Scenario: A failed transaction changes nothing
- **WHEN** a transaction sets two values and fails on the second
- **THEN** the configuration has neither change and no change is announced

### Requirement: Changes Apply In The Process First
Every applied transaction MUST be announced in the process, with its origin and the paths it touched, before and
independently of writing the file. The deck MUST show a change made through the document within half a second, also
when the change came from the same process.

#### Scenario: Editor change reaches the deck
- **WHEN** the editor sets a Clock widget's `format` to `12h`
- **THEN** the deck shows the 12-hour time within half a second, whether or not the file has been written yet

#### Scenario: A burst of changes
- **WHEN** the editor applies twenty changes within one second (a slider being dragged)
- **THEN** the deck rebuilds its page at most about seven times in that second and ends showing the last value

### Requirement: Writing The File
The document MUST write `urdeck-config.json` half a second after the last change, and immediately when the host exits
or the editor closes with a change not yet written.

- The file is written whole, indented, with the keys in the document's order; comments and the formatting of a
  hand-written file are not kept
- The write is atomic: a reader never sees a partly written file, and no temporary file is left behind
- A write whose content equals the file's current content is skipped
- The host never writes the file only because it is exiting
- A write that fails is retried with the next change and reported once to the editor; the deck keeps running

#### Scenario: Three quick edits
- **WHEN** three edits are made 100 ms apart
- **THEN** the file is written once, half a second after the third

#### Scenario: Exit without changes
- **WHEN** the host runs and quits without any change to the configuration
- **THEN** the file is byte for byte unchanged

#### Scenario: Exit with a pending write
- **WHEN** the user changes a setting and quits from the tray within half a second
- **THEN** the file has the change

### Requirement: Undo And Redo
The document MUST keep an undo history of the user's transactions and of changes from outside, at least 100 steps,
and undo and redo them in order. Consecutive changes to the same text or number field within one second are one step.
The host's own transactions (remembering the display, moving a secret out of the file) are not steps, and undoing or
redoing never reverts them.

#### Scenario: Undo everything
- **WHEN** the user makes five changes and undoes five times
- **THEN** the configuration equals what it was before the first change, and the file is written with that content

#### Scenario: Typing is one step
- **WHEN** the user types "GPU temp" into a label field and then undoes once
- **THEN** the label is what it was before the typing began

#### Scenario: Host change kept across undo
- **WHEN** the user changes a widget, the host then remembers a newly chosen display, and the user undoes
- **THEN** the widget change is undone and the remembered display is still in the configuration

### Requirement: Changes From Outside
When `urdeck-config.json` changes on disk and the content differs from what the document last wrote or read, the
document MUST read it and:

- if it is valid, take it as an undoable step labelled as a change from outside, and announce it
- if it is not valid JSON, keep the current configuration, log the warning, and report the problem with its line and
  column to the editor
- if its version is newer than the host's, take what the host can read and become read-only (see "Read-Only Document")

The document recognises its own writes by their content, not by time.

#### Scenario: Own write
- **WHEN** the document writes the file
- **THEN** no change from outside is reported and the deck does not reload because of it

#### Scenario: Edited in a text editor
- **WHEN** the file is changed in a text editor while the host runs
- **THEN** the deck shows the new configuration, and one undo restores the configuration from before the outside change

#### Scenario: Half-typed edit
- **WHEN** the file is saved with a missing closing brace
- **THEN** the deck keeps the previous configuration and the problem names the line and column

### Requirement: Read-Only Document
A configuration whose `version` is newer than the host's MUST be read as well as possible and never written while the
host runs: the document refuses edits from the user and the host, and the editor says why.

#### Scenario: Newer file
- **WHEN** the file has `"version": 2` and the host is version 1
- **THEN** the deck shows what it can read, no edit is applied, and the file is byte for byte unchanged after the host
  ends
