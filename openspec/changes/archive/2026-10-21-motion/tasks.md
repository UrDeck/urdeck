## 1. Groundwork

- [x] 1.1 On `feat/motion`, the build is clean and every golden passes before any change (`dotnet build urdeck.slnx -c Release`, `dotnet test tests/UrDeck.Engine.Tests -c Release --filter Golden`)
- [x] 1.2 Read `BorderSpike.cs` and its `WidgetView`/`MainWindow` hooks on `spike/theme-look-s2` (`git show spike/theme-look-s2:src/UrDeck.Host/BorderSpike.cs`, `git diff spike/theme-look-s1 spike/theme-look-s2 -- src`), `SizedCardChrome` (the border path, the conic and linear border gradients), `ResolvedCardStyle` (how `Compact` is derived and kept), `UnderlayPicture.HostSurface`, `BackgroundLayer` (`SetVisible`, `SetDisplayOn`, `UpdateRunning`), `BackgroundPlan.Resolve`, `DeckBackground.Create`/`RequestKey`, and `DisplayPowerWatcher`

## 2. Engine

- [x] 2.1 `MotionPolicy` (design, decision 1): the level enum, parsing the configuration value (unknown → absent, one warning), `Decide` returning the effective level, the user's level and the source; a truth-table test covering every row of the `motion` spec's scenarios plus each source
- [x] 2.2 The configuration's `motion` value on the root config (absent from a default configuration, preserved across load and save); tests for load, save round trip and an unknown value
- [x] 2.3 Resolve `borderAnimate` on `ResolvedCardStyle` and on its compact style (mode, period); null for `none`, spin without a conic gradient, a dashed border and a border width of 0; tests for each
- [x] 2.4 `ResolvedCardStyle.WithoutBorder()`: a derived style kept on its parent (like `Compact`) whose backs draw everything but the border; a test that its back equals the parent's back with `borderWidth` 0, and that the parent's backs are unchanged (design, decision 3)
- [x] 2.5 The pictures in `src/UrDeck.Engine/Rendering`: the conic source (square, same stops and starting angle as the still border) and the ring mask per shape key (drawn with `SizedCardChrome`'s border path: round, chamfer with `cutCorners`, compact circle), plus the linear-gradient end points for `pulse`, exposed for the host; tests: the ring's pixels match the still border's in each corner region of a card for round, chamfer and circle (within anti-aliasing), the source at angle 0 matches the still conic border's colour at the four edge midpoints, a key per shape
- [x] 2.6 `THEME057` (design, decision 7): the rule beside `THEME051`, the code in `ThemeCodes`, the `THEME090` message changed, fixtures `tests/UrDeck.Engine.Tests/Fixtures/lint/THEME057/pass|fail/theme.json`; the built-in themes still check clean
- [x] 2.7 The background takes the user's level: below `full` a shader candidate is created with speed 0 (design, decision 6); tests: the request key differs between `full` and `off` for a moving shader, is equal for a still background, and the background is not moving at `off`
- [x] 2.8 `--snapshot` decides the level from the configuration only (Windows treated as on); a test that a snapshot with `"motion": "off"` and a moving shader equals the one with `speed` 0, and that a snapshot of the Rigline fixture is unchanged by `borderAnimate`

## 3. Host

- [x] 3.1 **Verify first:** a throwaway check that toggling Settings → Accessibility → Visual effects → Animation effects changes `SPI_GETCLIENTAREAANIMATION` and delivers `WM_SETTINGCHANGE` to the deck window (ask the owner to toggle it if needed). Record the result in a "Findings during apply" section of `design.md`; if it does not arrive, use the fallback in design decision 2
- [x] 3.2 Windows' animation setting: read at startup, re-read on `WM_SETTINGCHANGE` through a window subclass (design, decision 2); a change re-runs the policy on the UI thread
- [x] 3.3 `MainWindow` runs the policy at startup, on a configuration reload, on a theme change and on the Windows change; logs `Motion: <level> (<source>)` only when the effective level changes; rebuilds the page only when it changed and the theme has an animated border; reapplies the background with the user's level
- [x] 3.4 `MotionGate` (design, decision 5) fed by `DisplayPowerWatcher` and the deck's shown/hidden; the background layer and the border sprites follow it, never disagreeing
- [x] 3.5 The border sprite on each `WidgetView` (pages and dock) at the effective level `full`: card back painted with `WithoutBorder()`, the sprite as the view's child visual with the mask brush (the conic picture or, for pulse, a colour or linear gradient brush), the ring as a hollow nine-grid; pictures decoded once and kept on the style; cleared in `WidgetView.Dispose`
- [x] 3.6 The animations (design, decision 4): spin on the source's rotation, pulse on the sprite's opacity, step easing at the constant `BorderStepsPerSecond = 15`, started together per page, paused and resumed with the gate; log lines for start and stop with mode, period, card count and reason

## 4. Verification by test and build

- [x] 4.1 Every golden byte-identical (`git status` shows no PNG changed)
- [x] 4.2 `dotnet build urdeck.slnx -c Release` with 0 warnings; `dotnet format urdeck.slnx --severity warn --verify-no-changes`; `dotnet test urdeck.slnx -c Release`; the engine tests with `-p:CollectCoverage=true` (the coverage floor); `openspec validate --all --strict`; `urdeck-cli theme validate` on the built-in themes clean

## 5. On the panel

- [x] 5.1 Log check with the Rigline fixture copied into `themes/` of a Release build in a scratch folder (as the S2 spike ran; config with `"theme": "rigline"`): one `Motion:` line, the border start line; display off and on (or sleep) and the tray's hide and show each log a stop and a start; an unrelated config save logs nothing about motion or the background
- [x] 5.2 Policy on the panel: `"motion": "reduced"` in the config stops the border and the shader without a restart; removing it brings both back; with the value absent, the Windows Animation effects switch does the same (with the owner)
- [x] 5.3 Cost, the S2 method (`docs/perf/theme-look.md`: 15 s warm-up, the median of one-second samples, two runs each): S2's eight-card page with Rigline against the same theme without `borderAnimate`, with `spin`, then with `pulse`; at `"motion": "off"` against without `borderAnimate` (must be equal within noise). Budget 5 % of a core and 8 MB private (`widget-card`, "Animated Border Cost"); record in `docs/perf/motion.md`
- [x] 5.4 Theme switch: Rigline to `default-dark` and back five times; the border sprites and pictures are released (no growth beyond the host's known per-switch growth, STATUS.md "Separate backlog items")
- [x] 5.5 **Owner's panel look** (stop and ask the owner; do not tick without them): Rigline's turning border at 100 %, 125 % and 150 %, a press and a page swipe, the dock if it has slots; and the border at `"motion": "reduced"` against the turning one, to see that the still and moving rings are the same shape at the corners. Record what was seen in "Findings during apply"

## 6. Documentation

- [x] 6.1 `docs/themes.md` and `site/src/content/docs/themes.md`: `borderAnimate` (spin, pulse, the 15 steps, when it runs, still in a snapshot, not with a dashed border), `motion.level` as a ceiling that does not touch the background, `THEME057` in "Checking a theme"; `cd site; npm run build` passes
- [x] 6.2 The configuration reference (`README.md` and `site/src/content/docs/index.md`, wherever `monitorName`/`pager` are described): `motion`, its values, following Windows when absent, and that `"motion": "full"` overrides Windows
- [x] 6.3 `docs/backgrounds.md` and `site/src/content/docs/backgrounds.md`: the user's reduced motion draws a moving shader still
- [x] 6.4 `docs/perf/theme-look.md`, S2 section: the eye check (2026-10-21, 15 accepted) and a pointer to `docs/perf/motion.md`
- [x] 6.5 `docs/design/look-and-feel/STATUS.md`: the PR 9 row (done, what was built, the numbers, the panel look), "Plan from here" (Rigline unblocked), "Open design points" (motion decided), "Not verified" (the 15-step item settled); `docs/design/theme-format-2-deferred-keys.md` unchanged unless a row names `motion` as pending
