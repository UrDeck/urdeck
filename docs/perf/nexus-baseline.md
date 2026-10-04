# HYTE Nexus baseline (the bar for UrDeck)

The owner's bar for UrDeck's cost is "no worse than Nexus" on the same panel. This records what Nexus costs, measured
on 2026-10-03 on the owner's machine (Windows 11 Pro 10.0.26200, 32 logical processors, HYTE Y70 Touch panel).

## What Nexus is

HYTE Nexus 2.16.5 is an Electron 31.7.7 application (the `version` file in
`%LOCALAPPDATA%\Programs\HYTE Nexus`; the folder has the Chromium layout: `app.asar`, `resources.pak`, `libGLESv2.dll`,
`libEGL.dll`, `ffmpeg.dll`). It is composited on the GPU: its GPU process uses both the 3D engine and the video decode
engine, so the animated background is a video decoded on the GPU. Chromium draws with Skia through Direct3D, so Nexus
is the same rendering architecture UrDeck is moving to, wrapped in a full browser.

## Numbers

Nexus was showing the page it starts with (assumed to be the owner's usual page, which has an animated background and
an animated weather icon; not confirmed).

| Measure | Value |
|---|---|
| Processes | 7 (6 Electron processes and `HYTE.Nexus.Service`) |
| Private bytes, all processes | about 627 MB |
| CPU | about 20% of one core (under 1% of the machine) |
| GPU | about 3% 3D, about 3% video decode |
| Dedicated video memory | 238 MB |

Per process (10 s sample):

| Process | CPU, % of one core | Private MB | Working set MB |
|---|---|---|---|
| Electron GPU process | 10.3 | 284 | 181 |
| `HYTE.Nexus.Service` | 3.6 | 112 | 235 |
| Electron (path not readable, probably the main process) | 1.1 | 102 | 139 |
| Electron renderer | 4.1 | 91 | 135 |
| Electron (path not readable) | 0.0 | 15 | 56 |
| Electron audio service | 0.0 | 12 | 89 |
| Electron (path not readable) | 0.0 | 11 | 37 |

A separate 27 s sample of the three readable processes gave 21.0% of one core (GPU process 15.6%, renderer 5.4%).

## Method

- Processes: `Get-Process` filtered on the name (`HYTE Nexus`, `HYTE.Nexus.Service`). Three of the processes do not
  expose their path or command line to a non-elevated shell, so filter on the name, not the path.
- CPU: the change in `TotalProcessorTime` over the sample window, as a share of one core.
- GPU: the `\GPU Engine(pid_<id>_*)\Utilization Percentage` counters, five samples four seconds apart, averaged per
  engine type. Video memory: `\GPU Process Memory(pid_<id>_*)\Dedicated Usage`.
- These are two short samples, not a benchmark. Repeat them with the page confirmed before quoting them as a result.
