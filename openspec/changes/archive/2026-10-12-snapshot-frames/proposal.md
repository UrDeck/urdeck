## Why

The website's widget loops (the split-flap clock flipping, the weather icons moving) need pictures of a widget at
consecutive moments. A widget draws from the time it is given, so the host's `--snapshot` can make them if it is asked
for more than one moment. Until now it rendered one picture at the current time.

## What Changes

- `--snapshot <folder> --frames N [--fps F] [--start time]` writes N pictures, `frame-0000.png` onwards, one every 1/F
  second of the time the widgets draw from, starting at `--start` (default now) at 30 frames per second by default.
- Without `--frames` nothing changes.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `host-shell`: a requirement for snapshot frames, added.

## Impact

- `src/UrDeck.Host/SnapshotCommand.cs`; the README's Snapshots section; `site/scripts/render-media.mjs` uses it.
