## 0. Before starting

- [ ] 0.1 `docs/perf/specialized-display.md` says "Decision: go", and this change's `design.md` reflects S1 to S7 (spike task 5.2)

## 1. Display model and matching (engine)

- [ ] 1.1 Display kind (desktop, specialized) in the display model; matching over both kinds by device path, then model code (design D3)
- [ ] 1.2 `display.kind`, `display.rotation`, `display.touch` in the configuration, kept and written as `display` is; the schema in `docs/schemas/`
- [ ] 1.3 Selectors choose among specialized displays when any are connected and nothing is remembered
- [ ] 1.4 Tests: toggle keeps the remembered display, first run prefers the specialized panel, in-use counts as absent, kind written on change

## 2. Controller (engine)

- [ ] 2.1 `DirectOutputController` with environment and owner seams, signals, states and the retry schedule (D4)
- [ ] 2.2 Tests with scripted target sequences: every scenario listed in D4

## 3. Touch mapping (engine)

- [ ] 3.1 `TouchMapper`: logical ranges and rotation to deck pixels, contacts to the pager's samples (first contact)
- [ ] 3.2 Tests: each rotation, range edges, tap, press, swipe, a second contact ignored, contact lost without an up

## 4. Rendering factoring (engine)

- [ ] 4.1 Split `PageRenderer` into steps the direct presenter can call with the session's slots and pager offset; goldens unchanged
- [ ] 4.2 Placeholder card for a widget that needs a hosted view; test with a fake descriptor

## 5. Direct presenter (host)

- [ ] 5.1 `DeckHost`: choose the presenter from the deck display's kind; swap on a kind change keeping the session's page (D1)
- [ ] 5.2 `DisplayCoreOutput`: manager with `Start()` and events, acquire, mode, rotation (D5), device, source, two primaries, fence, release on dispose and on process exit
- [ ] 5.3 GPU renderer per S3 (D3D12 + Skia), and the raster renderer with moving backgrounds still; the choice logged
- [ ] 5.4 Frame loop on the UI thread with the pacer thread (D2): one frame per change at rest, 30 per second while moving, skip rather than queue; frames started and stopped lines
- [ ] 5.5 `TouchReader` (D7): Raw Input while shown, device selection, unregister while dark or hidden
- [ ] 5.6 `SessionWatcher` (D8): display state, lock and unlock, resume; dark by disconnecting the path or a black frame per S7
- [ ] 5.7 Device loss and the watchdog (Risks)
- [ ] 5.8 `URDECK_DIRECT_DUMP` and the `Direct output:` log lines (D9)

## 6. Settings window and tray

- [ ] 6.1 Specialized rows in the picker (badge, number, no position, in-use state), live updates on a toggle
- [ ] 6.2 Rotate on the deck's specialized row
- [ ] 6.3 Identify through the direct presenter; other specialized rows say they get no marker
- [ ] 6.4 Tray waiting texts for the new reasons (in use, remote session, device lost)

## 7. Site and docs

- [ ] 7.1 Site page "Specialized displays": what it is, editions, turning it on and off (and undoing it on a single-display PC), any touch setting from S1, rotation, what is not available, tested hardware
- [ ] 7.2 The configuration reference (`display.kind`, `rotation`, `touch`), the troubleshooting page's `Direct output:` lines
- [ ] 7.3 `README.md`, `docs/BACKLOG.md`, `docs/ROADMAP.md`, `AGENTS.md` (a gotcha paragraph: how to run and diagnose direct output)

## 8. Verify

- [ ] 8.1 `dotnet build urdeck.slnx -c Release` with 0 warnings, `dotnet test urdeck.slnx -c Release`, the engine coverage floor, `dotnet format urdeck.slnx --severity warn`, the Linux test projects, the site build, `openspec validate --all --strict`
- [ ] 8.2 Implementer's PC, panel specialized: start, toggle off and on while running, every scenario in `specs/direct-output` by hand (taps, swipe, dot tap, press feedback, rotate, lock, display off, sleep and resume, unplug, another app holding it, Remote Desktop, quit), results and photos in `docs/perf/specialized-display.md`
- [ ] 8.3 Implementer's PC: a frame dump compared with `--snapshot` at the same size and moment (motion off)
- [ ] 8.4 Implementer's PC: idle and moving CPU, GPU and memory, direct against the window on the same panel, in `docs/perf/specialized-display.md`
- [ ] 8.5 Implementer's PC: touches on the panel never act on the desktop over a ten-minute session of normal use
- [ ] 8.6 Owner, on Windows Pro with the panel as a desktop monitor: nothing changed (a full page, swipe, taps, sleep, the picker), no `Direct output:` errors in the log
