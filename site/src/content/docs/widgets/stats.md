---
title: Stats
description: Show readings from the system or the weather as numbers and gauges.
section: Widgets
order: 11
---

`urdeck.widgets.stats` shows readings from any data provider: a large value with its unit and a label, optionally inside
a gauge. Its settings are a list of `slots` that fill the positions of the card's size in order.

| Size | Positions |
|---|---|
| 1x1, 2x2 | one reading (slot 1) |
| 4x2 | two gauges side by side (slots 1 and 2) above three text stats (slots 3 to 5) |
| 4x4 | four gauges in two rows (slots 1 to 4) above three text stats (slots 5 to 7) |

Slots beyond the positions of the size stay in the file; a position without a slot stays empty.

```json
{ "typeId": "urdeck.widgets.stats", "col": 0, "row": 2, "slots": [ { "reading": "system:cpu/core/2/load" } ] }
```

A 4x4 performance card: four gauges (CPU load, GPU temperature and load, memory load) above three text stats (the GPU's
power and clock, and the CPU's first core):

```json
{ "typeId": "urdeck.widgets.stats", "col": 0, "row": 0, "width": 4, "height": 4, "slots": [
  { "reading": "system:cpu/load" }, { "reading": "system:gpu/temperature" },
  { "reading": "system:gpu/load" }, { "reading": "system:memory/load", "label": "RAM" },
  { "reading": "system:gpu/power" }, { "reading": "system:gpu/clock" }, { "reading": "system:cpu/core/1/load" } ] }
```

## A slot

| Field | Meaning |
|---|---|
| `reading` | The reading's id, `<provider>:<path>`. With no slot the widget shows `system:cpu/load`. |
| `label` | Absent: the label the provider declares (for example `Core 2`). A text: shown instead. `""`: no label. |
| `unit` | `celsius` or `fahrenheit` for readings that have several units (otherwise the reading's default, then the region's). |
| `decimals` | Number of decimals to show (default 0). |
| `style` | `plain`, `gauge` (the theme's default gauge), `ring`, `bar`, `verticalBar`, `meter`, `tach`, `marker` or `thickbar`. Absent: plain at 1x1 and 2x2, the theme's gauge style in a gauge position. Text positions are always plain, and so is a reading with no range to measure against. |
| `min`, `max` | The gauge's range, replacing the one the provider declares (a percentage is 0 to 100). |
| `warning`, `critical` | From these values on the gauge is drawn in the theme's warning or critical colour. In the reading's canonical unit (degrees Celsius for a temperature). |

The fill eases to a new value over about 300 ms; the number and the colour change at once.

## The `system` readings

The `system` provider needs no administrator rights.

| Reading | Meaning |
|---|---|
| `system:cpu/load` | The share of time all logical processors are busy, in percent. |
| `system:cpu/core/<n>/load` | The same for logical processor `n`, counted from 1. |
| `system:memory/load` | Memory in use, in percent. |
| `system:gpu/load`, `system:gpu/temperature`, `system:gpu/power`, `system:gpu/clock` | The main GPU (the one with the most video memory): load in percent, temperature, power in watts and clock in MHz. `system:gpu/<n>/...` is each adapter, counted from 1 in that order. |

CPU load is the share of time the processors are busy, so with a CPU that boosts it reads lower under load than Task
Manager, which weighs by clock speed. GPU load is the busiest engine of the adapter, as in Task Manager.

GPU load and temperature come from Windows itself and should work with any vendor, but they have been verified on NVIDIA
only. Power and clock come from the vendor's library, which today means NVIDIA's `nvml.dll` (installed with the
driver); on AMD and Intel they show a dash. `urdeck.log` names each GPU and the source of each of its readings when it is
first sampled, which is what a [hardware report](/tested-on/) should include.

A reading that does not exist shows a dash and its id. A reading whose provider stopped answering keeps its last value in
the dimmed text colour. A provider runs only while a shown widget uses it, and stops five seconds after the last one goes.

The [weather](/docs/widgets/weather/) provider's readings can be shown here too, for example
`weather:Tokyo/current/temperature`.
