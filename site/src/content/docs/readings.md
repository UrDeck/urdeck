---
title: Readings and lists
description: What a reading is, the five shapes its value can have, and how a list reading holds rows that any widget can draw.
section: Start
order: 6
---

A **reading** is one thing a data provider can tell a widget: the CPU's load, a room's temperature, whether a lamp is on. It
is written `provider:path`, for example `system:cpu/load` or `homeassistant:light.desk`, and every widget that shows
data names its readings that way. In the editor you never type one: the reading picker lists what the providers offer, with
live values.

## The shapes of a value

A reading's value is one of five shapes:

| Shape | Examples |
|---|---|
| A number | CPU load, power in watts, a temperature |
| A text | a track title, a lock's state |
| On or off | a light, a plug |
| A time | a sunrise, shown as the time of day or as a day |
| A list | the busiest processes, the friends who are playing |

## List readings

A **list reading** holds rows, for a glance: a few friends, the ten busiest processes, today's events. Each row has a **key** and
named **fields**.

- The **key** is a text that is unique in the list and stays the same for the same thing: a process id, a Steam id. A widget
  uses it to keep your place in a list that is replaced, and to know which row a tap is on.
- A **field** holds a number, a text, an on/off state or a time, never another list. Its description gives it a name, a label
  and the same unit, decimals and levels any reading can have, so a temperature in a row is formatted as a temperature in a
  readout.
- A row **may lack a field**. A friend who is in no game has no `game`, and the row is drawn without it.
- The list is replaced **as a whole**, so a widget draws the old rows or the new ones, never half of each. A provider that
  publishes the same rows again changes nothing, and nothing is repainted.

### What a field is for

A field may have a **role**, which is how a widget that has never heard of the provider draws the row:

| Role | Drawn as |
|---|---|
| `title` | the row's text. A list with no title uses its first text field. |
| `detail` | a second line under the title |
| `status` | a short mark before the title, coloured by the field's levels |
| `image` | a picture: a file on this PC or an `http` or `https` address, drawn as a square the height of the first line |
| `link` | what a tap on the row opens |

The [List](/docs/widgets/list/) widget draws any list reading by these roles. A role a widget does not know is ignored.

### Bounds

A list has at most **200 rows**, and a text in a row at most **500 characters**. A provider that publishes more is cut to the
bound, and the cut is written once to `urdeck.log` with the provider's name. A list that has two rows with one key, or a
field that holds a list, is refused: the reading keeps its last good list and the log says why.

### A list where a number is expected

Where a widget shows a reading as one value, a list is **its number of rows**, in the list's own unit. A
[Stats](/docs/widgets/stats/) slot showing `system:processes/top` reads `10 processes`, and a list with no rows reads `0`, not a
dash. A provider therefore never publishes a count next to its list.

### Empty, pending, stale and unavailable

A list reading has the states every reading has. An **empty** list is not unavailable: the widget says what the description
says for it (`No busy processes`, `Nobody is playing`). A **stale** list keeps its rows. A reading that **cannot be supplied**
shows its reason.

### The lists UrDeck ships

| Reading | Rows |
|---|---|
| `system:processes/top` | The ten processes using the most processor time, most first: the name (title), the share of the processor in percent (status) and the memory in megabytes (detail). Looked at only while a widget shows it. See [Stats](/docs/widgets/stats/#the-system-readings). |

### In the picker, and from the command line

The reading picker marks a list as a list and shows its number of rows as its live value. A setting that needs rows, such as the
List widget's, offers lists only; a setting that cannot use one does not offer any. Choosing a list shows the fields its rows
hold. From a terminal, `urdeck-cli readings browse <provider>` prints each list's rows with their keys and fields.

Writing a provider that publishes a list is described in [CONTRIBUTING.md](https://github.com/UrDeck/urdeck/blob/main/CONTRIBUTING.md#publishing-a-list).
