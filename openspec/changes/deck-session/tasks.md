## 1. Seams

- [ ] 1.1 `IDeckScheduler` (post, one-shot timer, now) and a frame-source interface in the engine; host implementations over `DispatcherQueue` and the existing frame clock; a fake with a manual clock in the tests
- [ ] 1.2 Move `MotionGate` to the engine unchanged; its existing behaviour covered by a test

## 2. Widget and page state

- [ ] 2.1 `WidgetSlot`: widget instance, services, subscriptions (while in use and shown), refresh and wake scheduling, `IsAnimating`, tap and pressed state, `Paint(SKCanvas, size)`; drops the widget on dispose
- [ ] 2.2 `WidgetView` keeps the canvas and delegates to its slot; no behaviour change
- [ ] 2.3 `PageSlots` (page or dock) with the reading-to-slot map, underlay and border-motion decisions; `PageHost` delegates
- [ ] 2.4 Tests: subscribe on show, unsubscribe on hide and dispose, refresh and wake times, animating registration, tap reaches the slot, disposed slot holds no widget

## 3. Pager and session

- [ ] 3.1 `PagerState`: recognizer, live pages, offset, settle as a function of time; `PagerController` applies it to composition visuals
- [ ] 3.2 `DeckSession`: reload reactions, theme and background plan, motion decision, layout key and rebuild for a surface, readings to dirty slots, plugin reload releases slots first
- [ ] 3.3 `IDeckPresenter`; `MainWindow` becomes the window presenter with placement, delegating everything else to the session
- [ ] 3.4 Tests with a fake presenter: config reload rebuilds once, unrelated reload keeps the background key, hidden deck holds no slots, page change keeps one page alive at rest, swipe builds the neighbour, reading change dirties only its slots

## 4. Second presenter (amendment of 2026-11-06, design D4)

- [ ] 4.1 No static state holding sessions, slots or widgets; configuration, readings, theme/background inputs, scheduler and frame source injected
- [ ] 4.2 `ShowPage` and `PageChanged`; input optional; layout rectangles readable; a helper painting all visible slots of a page and the dock into one canvas
- [ ] 4.3 Tests: two sessions on one fake scheduler (independent pages, a reading change dirties both, plugin reload releases both), a session without input, `ShowPage`

## 5. Verify

- [ ] 5.1 `dotnet build urdeck.slnx -c Release` with 0 warnings, `dotnet test urdeck.slnx -c Release`, the engine coverage floor, `dotnet format urdeck.slnx --severity warn`, the Linux test projects; goldens unchanged
- [ ] 5.2 Owner, on the panel: a full page for ten minutes (values update, clock flips, weather moves), swipe and dot tap, a shortcut tap, edit the config and a theme while running, hot-reload a plugin, sleep and resume, unplug and replug the panel, the settings window's picker; the log matches the previous build's lines
- [ ] 5.3 Idle and moving CPU and GPU on the panel within the numbers in `docs/perf/` (record them in the pull request)
