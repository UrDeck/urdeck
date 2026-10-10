# UrDeck Backlog

The working document behind [ROADMAP.md](ROADMAP.md): the numbered work items, their design notes and status, and how
a work session runs. The roadmap says which outcome each item serves; this file says what the item is. It is the
hand-off point for work sessions: each item below should become (or already is) an OpenSpec change under
`openspec/changes/`. Work one item per session.

Older documents (hand-offs, perf notes, archived OpenSpec changes) say "roadmap item N" and point at
`docs/ROADMAP.md`. They mean the items in this file, which was split out of the roadmap on 2026-10-08; the numbers
did not change.

## How to run a work session

1. Read `README.md`, [ROADMAP.md](ROADMAP.md), this file, and the item's OpenSpec change (create it with the OpenSpec
   propose workflow if it doesn't exist yet; `openspec validate --all --strict` must pass).
2. Implement against `tasks.md`, ticking tasks only when verified.
3. Verify:
   - `dotnet build urdeck.slnx` (0 warnings) and `dotnet test urdeck.slnx`. `AGENTS.md` has the full command list.
   - Rendering without a screen: `UrDeck.Host.exe --snapshot out.png --size 1100x3840` (from the host's bin folder).
   - On the real panel: launch detached (the app otherwise blocks the shell), read `urdeck.log` next to the exe.
     It logs monitors, actual vs. target window bounds, placed widgets, reloads and warnings.
   - Screen capture on Windows: use Windows PowerShell 5.1 (`powershell.exe`, not `pwsh`), call
     `SetProcessDpiAwarenessContext(-4)` first, then `Graphics.CopyFromScreen` of the monitor bounds.
     `PrintWindow` on the window returns blank. Prefer one capture, or ask the user to look.
4. Never commit to `main`. Branch off it (`feat/...`, `fix/...`, `docs/...`, `chore/...`), push, and open a pull
   request; PRs are squash-merged once CI is green. The PR title is a conventional commit
   (`feat(scope): ...`, `fix`, `docs`, `test`, `perf`, `refactor`, `chore`) and the PR description becomes the body of
   the squashed commit, so write both for the changelog reader. See "Git workflow" in item 1.
5. Archive the OpenSpec change when all its tasks are done.

Suggested model per item is noted as **Model**. Items marked Opus involve architecture decisions or hard debugging;
everything else should be fine on Sonnet.

## Current state (2026-10-09)

- `urdeck-framework` (Phase 1 foundation) is complete and archived: SDK, analyzer, plugin loader with hot-reload
  (collectible AssemblyLoadContext), grid layout, WPF host with per-monitor DPI placement, Clock widget, tests, placeholder
  icon and render skipping (`NeedsRender`).
- Repo structure, licensing, the rename and the SDK/Engine split are done (item 1). The repo is `UrDeck/urdeck`.
- `theme-and-card` (item 5) is implemented (themes as data, host-drawn card, gap in the layout, readout and text
  line components, Clock migrated); see item 5.
- Proposed, not started: `display-targeting` (older draft, to be reworked with `/opsx:explore` then `/opsx:propose`).
- `data-providers` (item 3, first of its three changes, `openspec/changes/archive/2026-10-05-data-providers`) is implemented and archived
  (2026-10-05, all tasks and the panel checks done): providers as a plugin kind, the reading hub, the `system` provider
  (CPU load per core and in total, memory load) and the stats widget at 1x1 and 2x2. Measurements are in
  `docs/perf/data-providers.md`.
- Item 14 was explored on 2026-10-03/04. Outcome: composition moves to the GPU and the host is rebuilt on WinUI 3. A
  spike on the panel passed the memory and layering checks (`docs/perf/render-host-spike.md`); a plain Win32 window
  with DirectComposition stays the fallback. See item 14 and `docs/handoff/2026-10-04-render-host-spike.md`.
- `render-path` (the WinUI 3 host) is merged (#11) and so is the glass theme (#12).
- `animation` is merged (#13), archived as `openspec/changes/archive/2026-10-04-animation` (`IWidget.IsAnimating`, one host frame clock at 30 fps that runs only
  while a widget animates, the Clock's `flap` style); all its checks passed on the panel.
- `data-providers` is merged (#14). `stats-gauges` is implemented and archived (2026-10-08,
  `openspec/changes/archive/2026-10-08-stats-gauges`): the gauge component with the styles ring, bar, vertical bar and
  plain, levels with warning and critical colours, the stats widget at 4x2 and 4x4, an eased fill. Panel checks passed;
  measurements are in `docs/perf/stats-gauges.md`.
- `gpu-readings` is implemented and archived (2026-10-08, `openspec/changes/archive/2026-10-08-gpu-readings`): GPU load and temperature for any vendor
  through Windows, power and clock through `nvml.dll` on NVIDIA behind a vendor seam, without elevation. Spike and
  measurements in `docs/perf/gpu-readings.md`. Verified on NVIDIA only. Adds `IReadingSink.Log` to the SDK.
- Proposed, not started: `display-targeting`.
- `weather` is implemented (2026-10-08, `openspec/changes/archive/2026-10-08-weather`): the `weather` provider (Open-Meteo, keyless, any number
  of places typed into the widgets), the 4x2 weather widget with Meteocons animated icons (`SkiaSharp.Skottie`), the
  time reading, patterned catalog entries, attribution carried with the data and drawn by the weather and stats widgets,
  and `IWidgetHost.Log`. Spike and measurements in `docs/perf/weather.md`.
- `pages-and-pager` is implemented (2026-10-09, archived as `openspec/changes/archive/2026-10-09-pages-and-pager`): the window never takes focus
  (`WS_EX_NOACTIVATE`; Escape no longer closes it, `URDECK_ACTIVATABLE=1` restores it), swipe between pages with a
  bounce at the ends, the page indicator (`pager.indicator`: `always`, `fade`, `off`, `auto`) with tap-to-switch,
  `--snapshot --page N`, and the indicator in the theme. Only the current page is alive. Measurements in
  `docs/perf/pages-and-pager.md`. Gaps carried forward: widgets receive no touch yet (no SDK input API), a hosted web view
  would take the swipe from the page, and the window never has keyboard focus, which a web view with text entry needs.
- `shortcut-and-tap` is implemented and archived (2026-10-09, `openspec/changes/archive/2026-10-09-shortcut-and-tap`;
  all tasks and the panel checks done): taps reach widgets through the optional `ITapTarget` interface,
  the host shows the press on the compositor with the theme's `press` values, a launch service and an icon service
  (shell icons, local image files, site icons with a disk cache) sit on the per-widget host services, the SDK has the
  image tile, and the 1x1 shortcut widget (`urdeck.widgets.shortcut`) uses all of it. Spikes and measurements in
  `docs/perf/shortcut-and-tap.md`. The engine now makes network requests of its own (site icons; see the README's
  shortcut section). Gaps carried forward: scroll and long press routing, input for a hosted web view, the dock.
- `dock` is implemented (2026-10-09, `openspec/changes/dock`; the checks on the panel passed): up to four
  widgets in a band at the very bottom of the screen, the same on every page, with the page indicator directly above
  it. A slot is a grid cell at a smaller size (the theme's `dock.height`, 0.71 of a cell), so any widget with a 1x1
  size sits in it as a miniature and the dock has no drawing, press or launch code of its own. The `dock`
  configuration field is now a list of widget objects. On the 1100x3840 panel the dock costs no row. Measurements in
  `docs/perf/dock.md`.
- Next: the 4x4 weather widget (forecast days, humidity and wind), then sensors that need elevation (item 3, fourth
  change). See "Suggested order" below.
- Memory: the WinUI 3 host is ~101 MB private / ~135 MB working set (Release, one Clock), 0% CPU and GPU idle
  (`docs/perf/render-host-baseline.md`). The WPF host it replaced was ~66 MB (`docs/perf/memory-investigation.md`).
  The bar is "no worse than Nexus" (`docs/perf/nexus-baseline.md`), see item 4.

### Suggested order

Item numbers are identifiers, not a sequence. The order below gets the owner's current first page (clock, weather,
performance, shortcuts, dock, page indicator) rebuilt with the fewest blocked steps:

1. **Item 14, rendering path and animation.** First because it replaces the host every widget is drawn in. The render
   host spike is done; next is the `render-path` change (`openspec/changes/archive/2026-10-04-render-path`, the WinUI 3 host), then the
   `animation` change (frame requests and an animation clock, with a split-flap Clock style as first consumer).
2. **Item 3, data providers**, as four changes:
   1. `data-providers` (done): providers as a plugin kind, readings, the `system` provider (CPU and memory load,
      no elevation) and the stats widget at 1x1 and 2x2. Gives the row of per-core cards.
   2. `stats-gauges` (done): the gauge component and the stats widget's 4x2 and 4x4 compositions (the performance
      widget of item 7).
   3. `gpu-readings` (done): GPU load and temperature for any vendor through Windows, GPU power and clock through
      the vendor library (NVIDIA first), all without elevation.
   4. Sensors that need elevation (CPU temperature and what else needs a kernel driver) through an opt-in helper.
3. **Weather** (item 7) as a provider plus a widget, which brings animated colour icons (Lottie), the first network
   provider, per-widget locations and a time reading. Needs item 14 and the first `data-providers` change. The first
   change (the 4x2 widget) is done; the 4x4 widget follows as the next weather change. It goes before the
   elevated-sensors change.
4. **Pages and touch** (item 8; pages done in `pages-and-pager`, taps in `shortcut-and-tap`), then **shortcuts**
   (item 7; done in `shortcut-and-tap`, which brought the image tile) and the **dock** (item 10; done in `dock`).

Display targeting (item 6) is a future change. Its draft is written against the WPF window, so rework it now that the
new host has landed.

---

## 1. Repository structure and standards

**Why:** the repo grew organically; contributors (human and agent) need a predictable layout and enforced style.
**Model:** Sonnet.
**Status:** done except the items marked open below (license, placeholder icon).

Layout (done; monorepo; first-party widgets live here, community widgets in their own repos):

```
sdk/                     # MIT: the plugin contract
  UrDeck.Sdk/            # attributes, Widget<T>, render context (becomes the UrDeck.Sdk NuGet package later)
  UrDeck.Analyzer/
src/                     # GPL
  UrDeck.Engine/         # plugin loader, config store, grid layout, page renderer
  UrDeck.Host/
widgets/                 # first-party widget plugins (UrDeck.Widgets.Clock, ...)
tests/
  UrDeck.Engine.Tests/
  UrDeck.Analyzer.Tests/
docs/                    # ROADMAP.md, BACKLOG.md, perf/, architecture notes
openspec/
```

Tasks:
- [x] Move projects; the host build still copies first-party widgets into `plugins/`. Solution is now `urdeck.slnx`.
- [x] `Directory.Build.props`: shared TFMs, `Nullable`, `ImplicitUsings`, `LangVersion`, `TreatWarningsAsErrors`,
  `EnforceCodeStyleInBuild`. `Directory.Packages.props` for central package versions (SkiaSharp 3.119.4, xunit, Roslyn).
- [x] `.editorconfig`: file-scoped namespaces (convert existing block namespaces), `var` usage, naming (`_camelCase`
  fields), brace/newline rules, CRLF handling consistent with `.gitattributes`. Run `dotnet format` once to apply.
- [x] Test coverage with coverlet (`dotnet test tests/UrDeck.Engine.Tests -p:CollectCoverage=true`); floor for UrDeck.Engine is 65% line (measured 76.7%).
- [x] `AGENTS.md` (build/test/verify commands, conventions, the Windows/WPF gotchas above; `CLAUDE.md` can point to it).
- [x] `CONTRIBUTING.md`: move the widget-authoring section out of README; add performance budgets (item 4) and the
  review checklist.
- [x] GitHub Actions CI on `windows-latest`: build, test, `dotnet format --verify-no-changes`, `openspec validate --all --strict`.
- [x] Product name decided: **UrDeck**. License decided: see "Naming, licensing and hosting" below.
- [x] Placeholder app icon (done when `urdeck-framework` was closed); final logo/branding deferred.

### Git workflow and GitHub maintenance

**Status:** applied on 2026-09-29 (ruleset "main protection", squash-only merge settings, PR template, Dependabot, `pr-title` check). Open: `CODEOWNERS` once there is a second maintainer.

All work happens on a short-lived branch and lands on `main` through a pull request. Nobody (including admins and
agents) pushes to `main` directly. Configure on GitHub (repo settings + a ruleset on `main`), and document in
`CONTRIBUTING.md`:

- Ruleset on `main`: require a pull request (no direct pushes, no force pushes, no deletion), require the CI status
  checks (`build`, `pr-title`) to pass with branches up to date, require linear history, require conversations
  resolved. No bypass for admins. Zero approving reviews are required while there is a single maintainer; raise
  `required_approving_review_count` when that changes.
- Merge methods: squash only (disable merge commits and rebase merges), which also guarantees linear history.
- Squash commit defaults: title = PR title, body = PR description (`squash_merge_commit_title: PR_TITLE`,
  `squash_merge_commit_message: PR_BODY`), so the PR text is the commit text.
- Delete head branches automatically after merge; enable "always suggest updating pull request branches".
- Pull request template (summary, linked OpenSpec change, verification done, perf impact) and a conventional-commit
  PR title check in CI.
- Optional: Dependabot for NuGet and GitHub Actions; `CODEOWNERS` once there is more than one maintainer.

### Naming, licensing and hosting (decided 2026-09-29)

- [x] **Name:** UrDeck (short for "your deck"). Renamed from Widgy: namespaces, assemblies, exe, config (`urdeck-config.json`),
  log (`urdeck.log`), widget type ids (`urdeck.widgets.clock`), analyzer ids (`URDECK001-005`), env vars.
- [x] **GitHub:** the repo lives in the `UrDeck` org as `UrDeck/urdeck` (moved 2026-09-29; the old URL redirects).
- [ ] Reserve the `UrDeck.` NuGet prefix; register `urdeck.app` / `urdeck.dev` (owner task).
- [x] **Licenses** (done: root `LICENSE`, `PLUGIN-EXCEPTION.md`, `LICENSE` for the MIT `sdk/` tree, SPDX headers
  enforced by `.editorconfig`, `PackageLicenseExpression` in the build props, README license map). The plan:
  - Host, engine and official widgets: **GPL-3.0-or-later** with a GPLv3 section 7 **plugin exception**: widgets that use only
    the public SDK API may be under any license.
  - SDK and analyzer (`UrDeck.Sdk`): **MIT**. Templates and example widgets: MIT (so copying them does not make a
    community widget GPL). Fonts, icons, themes and logo: licensed separately.
  - Community widgets: the author chooses. Contributions are inbound=outbound (no DCO/CLA before 1.0 or the first
    outside contribution; revisit if dual licensing is ever wanted).
- [x] **SDK/Engine split** (done): the license boundary is an assembly boundary. `UrDeck.Core` became `sdk/UrDeck.Sdk` (MIT:
  attributes, `Widget<T>`, `WidgetConfig`, render context, `ThemeColors`) and `src/UrDeck.Engine` (GPL: plugin loader, config
  store, grid layout, `PageRenderer`, logging). The analyzer moved to `sdk/`. Widgets reference only the SDK. The SDK is not
  widget-only: data providers, display backends and theme packs will use it too. Done on purpose in the small: the
  `UrDeck.Sdk` `AssemblyVersion` is frozen at 0.1.0.0 (guarded by `SdkContractTests`) and the loader warns when a plugin has
  no widgets or carries a different SDK copy.

#### Parked: do before publishing the SDK, accepting external widgets or opening the marketplace

Deliberately not done yet (one author, one SDK version, no outside widgets). The full analysis by an Opus design review
is in [docs/design/sdk-engine-split-full-design.md](design/sdk-engine-split-full-design.md) with a three-PR migration plan
in [docs/design/sdk-engine-split-full-tasks.md](design/sdk-engine-split-full-tasks.md). Items to pick up:

- [ ] **Loader hardening:** reject (with a clear log line) plugins that reference `UrDeck.Engine`/`UrDeck.Host`, were built
  against a newer SDK, or against an incompatible SkiaSharp major/minor; treat only DLLs that reference the SDK as plugins
  (stray `SkiaSharp.dll` or native libs in `plugins/` currently load as plugins and just log "no widgets").
- [ ] **SDK versioning:** SemVer; a `MinimumCompatibleVersion` the loader enforces; the version a plugin was compiled against is
  the authoritative target (a marketplace manifest only mirrors it). Today only `AssemblyVersion` is frozen.
- [ ] **Public API tracking:** `Microsoft.CodeAnalysis.PublicApiAnalyzers` on the SDK (`PublicAPI.Shipped/Unshipped.txt`) and NuGet
  package validation after the first release.
- [ ] **SkiaSharp policy:** the SDK exposes SkiaSharp types, so declare a version range (the design suggests `[4.153.0, 5.0.0)`)
  and make Dependabot ignore SkiaSharp majors; they are SDK-breaking changes. (Dependabot already moved 3.x to 4.x.)
- [ ] **Packaging:** bundle the analyzer in the `UrDeck.Sdk` NuGet package; a `dotnet new` widget template.
- [ ] **Build guard:** fail the build if anything under `sdk/` references `src/`.
- [ ] **Services and logging for widgets:** `IWidgetHost.Log(string)` and a protected `Log` on `Widget<T>` exist since `weather`
  (one line to `urdeck.log`, enough for a missing resource). The fuller design, for when a widget needs levels and
  categories: use
  `Microsoft.Extensions.Logging.Abstractions`: the SDK exposes `ILogger` (for example a protected `Logger` on `Widget<T>`) and the
  engine supplies an `ILoggerFactory` that writes to `urdeck.log`. No custom logging interface. The host-services hook
  (`IWidget.Attach(IWidgetHost)`) is done in its minimal form with `data-providers` (it carries the readings); the
  logger joins it with the first plugin that needs one.
- [ ] **SDK tests:** the SDK is only covered indirectly through the engine tests; add `UrDeck.Sdk` tests and a floor (the design
  suggests 80%).
- [ ] **Plugin exception wording:** optionally name theme/data packages, non-widget plugin kinds and the SkiaSharp types the SDK
  exposes once those plugin kinds exist.

- **Repo strategy:** monorepo through 1.0 (atomic SDK/engine/host/widget changes while the API churns). Later, split along
  the existing seams: `urdeck/widget-template` (MIT template repo, worth doing soon) and a git-based marketplace
  registry repo (see item 13). Split the SDK out only once it is stable.

## 2. Roadmap and backlog documents

This file and [ROADMAP.md](ROADMAP.md). Keep "Current state" and item status here up to date at the end of each session;
update the milestone status in the roadmap when an item that a milestone lists is finished, and add new ideas to
item 15 with the milestone that needs them.

## 3. Data providers (new `data-providers` change)

**Why:** `[RefreshOnEvent]` and `[RefreshAdaptive]` (the last open parts of the old framework change) only make sense with
shared data: several widgets that all want CPU/GPU/memory (or weather, now-playing) should not each poll.
**Model:** Opus for the design (it fixes public SDK API), Sonnet to implement.

Done in `urdeck-framework`: skipping redundant redraws via `IWidget.NeedsRender(DateTime now)` (the Clock repaints once a
minute) and the placeholder icon.

**Status:** the first change is implemented (2026-10-05) as `openspec/changes/archive/2026-10-05-data-providers`; its `design.md` holds the
decisions and the rejected alternatives. The handoff that started the exploration is
[docs/handoff/2026-10-03-data-providers.md](handoff/2026-10-03-data-providers.md). What it measured on the panel is in
[docs/perf/data-providers.md](perf/data-providers.md). `[RefreshOnEvent]` is gone (`[RefreshOnData]` replaces it) and
`IWidget` gained `Attach` and `Subscriptions`, so the SDK assembly version is 0.3.0.0.
**Next step:** the second change, `stats-gauges` (gauge component and larger compositions), and `gpu-readings` are done; the fourth is the elevated helper, which waits behind the weather widget (item 7).

What the exploration settled (it replaced the earlier "named topics with typed values, one provider per topic"):

- **Providers are a plugin kind** from the first change, loaded like widgets and referencing only the SDK. A provider
  is created on its first subscription and stopped after its last, so 200 installed providers cost nothing if 3 are
  used.
- **Self-describing readings, not typed topics.** A reading has an id (`system:cpu/core/2/load`), a value (number,
  text or on/off; image later) and a catalog entry (kind, label, name, device, optional range and default display
  unit). Any widget can show any reading, and an editor can list them. Rich data such as now-playing is a group of
  readings; typed topics are postponed and may never be needed.
- **A provider owns a namespace and a catalog.** The first-party `system` provider has portable ids
  (`system:cpu/load`) that work on any PC, and will later hide where a value comes from, including the elevated
  helper.
- **The widget declares, the host subscribes.** A widget names its reading ids from its config; the host subscribes
  while the widget is shown. Only providers with a visible consumer run (this is what makes per-page demand work
  once item 8 lands).
- **Engine-owned sampling** off the UI thread, one loop per provider, at a rate the provider declares and the user
  can override under `providers` in the config. Values arrive in batches and are read at paint time without I/O.
- **Uniform states and formatting:** ok, pending, stale (last value dimmed) and unavailable (a dash), with reasons in
  the log. One SDK formatter; the display unit comes from the widget's setting, then the reading's default, then the
  region, because hardware temperatures are read in Celsius even where weather is read in Fahrenheit.
- **Elevation is opt-in:** a one-time deliberate action enables the helper, and demand decides whether it runs.
  Without it those readings are in the catalog and report unavailable.

The three changes are listed under "Suggested order". Left for later, each with the change or consumer that needs it:

- Provider-defined settings and secrets (a Home Assistant URL and token): with the first provider that needs them. The
  weather location no longer needs provider settings: it is per widget and travels in the reading id (item 7), so
  `providers.weather` carries only `intervalMs`. Secrets (Credential Manager or DPAPI) stay parked.
- A logger for plugins: `IReadingSink.Log` already serves providers, which are the only plugins that talk to a network,
  so `ILogger` stays parked until a widget needs to log.
- Backing off when the PC is busy or on battery, which also gives `[RefreshAdaptive]` its meaning: the engine owns
  every provider's timer, so it is one place to change. Shared with the animation frame rate (item 14).
- A per-card sampling rate, history buffers for charts, a catalog that changes while running, image values.
- FPS: it comes from tracing the foreground game, not from hardware, and needs its own provider and rights.
- An editor that lists the catalog with live values (item 11) is a subscriber that is not a shown widget.

## 4. Performance budgets and measurement

**Why:** give users and widget authors clear, comparable costs (the per-component strategy).
**Model:** Opus for the measurement design, Sonnet to implement.

- The bar (restated 2026-10-04): no worse than HYTE Nexus on the same panel, which measures about 627 MB private, 20%
  of one core and 3% GPU (`docs/perf/nexus-baseline.md`). The owner's real goal is an open source, trustable
  alternative, so cost may be spent on looks. The principle that stays: a page with nothing moving costs close to
  nothing, and the user pays only for what they switch on.
- Budgets: host runtime baseline about 100 MB private (WinUI 3, measured in `docs/perf/render-host-baseline.md`); editor is secondary; per-widget budgets with tiers
  (e.g. gold < 5 MB, silver < 10 MB, bronze above), expressed **per grid size** because the render surface
  (width x height x 4 bytes) dominates per-widget memory.
- Measurement: a benchmark mode (e.g. `UrDeck.Host.exe --bench <widgetId> --size 4x2`) that runs the host with only
  that widget and reports the delta from an empty page (private bytes, working set), plus per-widget render time,
  update time and allocations per frame (`GC.GetAllocatedBytesForCurrentThread` around `Render`/`UpdateAsync`).
  Per-widget memory can't be isolated precisely inside a shared process; isolation runs are the fair measure.
- Optional in-app diagnostics overlay showing per-widget render time and CPU.
- Publish tiers in CONTRIBUTING.md; later show them in the widget picker.
- The host decision was item 14: the WPF host was replaced by a WinUI 3 host for GPU composition (`render-path`), with a
  plain Win32 window and DirectComposition as the fallback.

## 5. Theme and card (`openspec/changes/archive/2026-10-03-theme-and-card`)

**Status:** done (SDK 0.2.0.0; paint cost in `docs/perf/theme-card-paint.md`, format in `docs/themes.md`).

Themes as data (colours, card shape, typography, stroke; a folder with optional bundled fonts), a host-drawn card
behind every widget, the gap in the grid layout, and the first shared components (readout, text line), with the Clock
migrated onto them. Do this before building more widgets so they don't each invent styling.
**Model:** Sonnet to implement (`/opsx:apply`), Opus to review.

The design was settled in an exploration on 2026-10-03; decisions and rejected alternatives are in the change's
`design.md`. The earlier `theme-engine` draft is kept for reference as `docs/design/theme-engine-draft-*.md`, and the
handoff that started the exploration is [docs/handoff/2026-10-03-theme-and-components.md](handoff/2026-10-03-theme-and-components.md).

Follow-ups (see `design.md` of the theme-and-card change for the constraints they build on), each its own change, each component landing with its first widget:

- Animation and the rendering path: now item 14.
- Gauge ring (with the performance widget); tinted glyphs and colour or animated icons (with weather); image tile (with
  shortcuts and the dock). Charts stay parked until a widget needs one. See item 7 for which widget brings which.
- Later theme knobs once measured: shadows, gradients, blur, a motion level, backgrounds, icon packs.

## 6. Display targeting (`openspec/changes/display-targeting`)

Pick the target monitor from a UI list with friendly names (EDID), persist a stable monitor identity (not
`DISPLAYn`), never expose resolution/scaling to the user, and re-render at the correct scale after sleep/resume,
hot-plug, mixed-DPI setups and arbitrary monitor wake order. Needs real sleep/wake testing on the Y70.
**Model:** Opus (hard to debug).

The existing draft targets the WPF window. Rework it once the new host from item 14 has landed; placement and recovery
after sleep are also one of the spike's pass/fail checks.

## 7. Default widget set

Clock (done), weather, performance, shortcuts, media/now-playing. Each widget is its own change, must meet its
performance budget (item 4), takes its whole look from the theme (item 5) and brings the shared components it is the
first to need. The goal is the same features as the owner's HYTE Nexus page with a deliberately different look, not a
clone. **Model:** Sonnet to implement; Opus where a widget introduces a new component or config pattern.

| Widget | Sizes | Shows | Needs first | Brings |
|---|---|---|---|---|
| Clock | 4x2, 4x1, 2x1, 1x1 | time, date; `style: "flap"` is a split-flap display (`animation`, item 14) | done | readout, text line (done) |
| Stats (single stat and performance are one widget, `urdeck.widgets.stats`) | 1x1 and 2x2 (one reading; done in `data-providers`), 4x2 (two gauges, three text stats) and 4x4 (four gauges, three text stats) (done in `stats-gauges`) | any reading from any provider, one per slot: CPU and GPU temperature and load, memory, GPU power and clock, one CPU core per card | item 3 | slot configuration (a reading plus a presentation); gauge component (ring, bar, vertical bar) with the larger sizes (done) |
| Weather | 4x2 (done in `weather`) | animated colour icon (day and night variants), temperature, condition, high and low, place, sunrise and sunset | item 14, item 3 (it is a provider plus a widget) | Lottie icons, per-widget locations, the time reading, patterned catalogs; see "Weather widget" below |
| Weather, 4x4 (roadmap) | 4x4 | everything of the 4x2 plus forecast days, humidity and wind (exact content open) | the 4x2 weather change | forecast readings (`daily/N/...`), a date label |
| Shortcut | 1x1 (done in `shortcut-and-tap`) | the icon of an application, a file, a folder or a web address; opens it on tap; settings `target`, `arguments`, `icon`, `label` | item 8 (touch) | tap input (`ITapTarget`), the launch and icon services, the image tile; all shared with the dock (item 10) |
| Media / now playing | open | track, artist, art, controls | items 3 and 8 | image tile reuse |
| Web page | open | any web page, with touch (the owner shows Frigate camera feeds this way in Nexus) | item 14 (new host), item 8 | a hosted widget kind: the host places a web view layer instead of calling `Render` |
| Camera (parked) | open | camera streams without a browser, for example from Frigate's go2rtc | item 9 for real video | reuses the video layer; a first version could draw snapshots on the canvas |

**Stats widget, larger compositions (the second `data-providers` change, now implemented as `stats-gauges`; the notes below are the original exploration).** The owner's reference is
the HYTE Nexus 4x4 performance widget, which is one widget with several configurable parts:

- Row 1: CPU temperature (with gauge) and CPU utilization % (with gauge).
- Row 2: GPU temperature (with gauge) and GPU utilization % (with gauge).
- Row 3: memory % used, GPU wattage and GPU clock speed (MHz or GHz), as text stats.

What is settled and what is open, so the next session starts from context:

- The first change (`data-providers`) deliberately stops at 1x1 and 2x2. It specifies the slot list (`reading`, `label`,
  `unit`, `decimals`, unknown properties preserved) and that only the first slot is drawn; extra slots stay in the file.
  It does not specify gauges, per-slot presentation or layout.
- This is the same widget (`urdeck.widgets.stats`) growing, not a new one: 4x2 is two gauges and three text stats, 4x4 is
  four gauges and three text stats. Configs written for 1x1 must stay valid.
- To be specified in the next change: a per-slot presentation (the roadmap's working names are `gauge` and `text`, with
  `text` as the meaning of a missing property); which slot goes where at each size (probably by slot order, with the
  composition chosen from the widget's grid size, as the conventions below say); what a gauge shows (ring and readout,
  its range from the catalog entry's `Min` and `Max`, colour from the theme, for example `Good`, `Warning` and
  `Critical` thresholds); how a temperature slot picks its unit (the reading's default, then the region, see the
  formatter); and what a slot without a value looks like inside a gauge (the dash and the dimmed colour already exist).
- The gauge ring is a new shared SDK component (a ring around a `Readout`), brought by this change, so it is available to
  any widget and themed like the others (stroke thickness and cap are already theme values).
- Readings the reference needs: CPU temperature (still nothing offers it), and the GPU's temperature, load, power and
  clock, which `gpu-readings` now supplies without elevation (`system:gpu/...`, README). CPU temperature and what else
  needs a kernel driver come with the fourth change (the opt-in helper behind the `system` provider, ids such as
  `system:cpu/temperature`, unavailable until the helper is enabled). A slot whose reading is unavailable shows the dash.
- CPU load is time-based, not Task Manager's number. `system:cpu/load` is the share of time cores are busy, so it reads
  lower than Task Manager (which shows `% Processor Utility`, weighted by frequency) when the CPU boosts: 53% against
  84% with half the cores busy on the owner's machine. Kept on purpose (2026-10-05); how to switch and what to measure
  first are in design decision 10 of `openspec/changes/archive/2026-10-05-data-providers/design.md` and `docs/perf/data-providers.md`.
  Revisit this if gauges make the difference more noticeable.
- Text sizes are theme values; a gauge's inner readout must use them like every other readout, so the theme alone decides
  how large the small text is (it was enlarged twice after the first look at the panel, see `docs/themes.md`).

**Weather widget (explored 2026-10-08; the first change is 4x2 only, 4x4 is its own change).** Settled with the owner,
or checked against the code and the spike (`H:\projects\urdeck-render-spike`, `render-host-spike.md`):

- Scope of the first change: the `weather` provider and the 4x2 widget (icon, temperature, condition, high and low,
  place, sunrise and sunset). 4x4 (forecast days, humidity, wind) is the next weather change.
- **Locations are per widget, several at once.** The widget config holds a free-text `location` (city, zip, "City, ST",
  "City, State", optionally with a country code) and may hold explicit `latitude` and `longitude`. The widget puts the
  location into its reading ids as the first path segment (`weather:Portland, Oregon/current/temperature`); the
  engine's `ReadingId` already allows this. The provider reads the distinct locations from `SetDemand`, geocodes them
  and fetches all of them in one forecast request. It publishes `{location}/place` (the resolved name) so the widget
  can show it by default and the user can see which place was picked. The widget normalises the location (trim, case)
  so two widgets for one place share a fetch.
- **Geocoding: Open-Meteo's search (keyless), probed on 2026-10-08.** Works: `90210`, `Portland, OR`, `Portland, Oregon`,
  `Springfield, IL`, `Paris, TX`, `Portland, Oregon, US`, `London, GB`, `London, England`, a bare `Zurich` (the biggest
  match wins). Does not work: `Portland OR` (no comma), `London, UK` (use `GB`), `Zürich, CH`, `Beverly Hills, CA 90210`,
  or a wrong state. A bare zip can match several countries (`10001`, `97201`), so the first result is shown with its
  resolved name and the other candidates are logged; adding `, US` narrows it. A no-match result is `unavailable`
  with the query in the reason.
- **Catalog (proposed, public API): patterned descriptors.** A descriptor path may contain `{location}`
  (`{location}/current/temperature`); the engine resolves a subscribed path through one lookup that falls back to the
  patterns and caches the concrete descriptor. Today `rt.Catalog` is an exact dictionary read at about six sites in
  `ReadingHub` and `ReadingHub.Sampling`, which is why `CheckPath` would mark every location unavailable. The
  alternative (a catalog that grows while running) stays parked.
- **Provider settings are not needed.** `providers.weather` carries `intervalMs` only (default 15 minutes, minimum
  5; Open-Meteo's current conditions update every 15 minutes, and its free tier allows 600 calls a minute, 5,000 an
  hour and 10,000 a day, which these intervals stay far below; the minimum must stay enforced).
  `IReadingSink.Log` is the provider's logger. Secrets stay parked.
- **Reading vocabulary is provider-neutral**: `current/condition` is text from a small portable set (`clear`,
  `partly-cloudy`, `cloudy`, `fog`, `drizzle`, `rain`, `snow`, `thunder`, ...) mapped from WMO codes in one place;
  also `current/is-day`, `current/temperature`, `current/apparent`, `today/high`, `today/low`, `today/sunrise`,
  `today/sunset`. A later provider fits behind the same ids. Condition labels are English for now.
- **Time as a reading (settled to add; representation proposed).** A new value type holding a `DateTimeOffset`
  (epoch seconds plus the UTC offset) and `ReadingKind.Time`, which `ReadingFormatter` shows as the time of day in the
  value's own offset (a Tokyo sunrise reads in Tokyo time) with the region's 12 or 24 hour setting. An instant, not a
  `TimeSpan`: after sunset the widget needs tomorrow's sunrise, which a time of day cannot say. Dates and date-times
  are further formatting kinds over the same value; durations (uptime, time left) are a number with a unit and wait
  for a use.
- **Icons are Lottie (settled).** Spike result: `SkiaSharp.Skottie` 4.153.1 works headless (`Animation.TryCreate`,
  `SeekFrameTime`, `Render(canvas, rect)`) and measured 3.4% of one core and 3.1% GPU 3D at 30 fps, 7.5% at 60 fps,
  with the whole 1050x520 card repainted each frame; Nexus is about 20% of a core. The 30 fps frame clock from
  `animation` stays. Meteocons fill is MIT and needs an attribution entry.
- **Where Lottie lives (settled 2026-10-08): the SDK, as a component.** A small `IDisposable` wrapper in
  `UrDeck.Sdk.Components` over `SkiaSharp.Skottie` (create from a stream, seek to a time, draw into a rectangle), so any
  author can use it and the SDK's public surface shows no Skottie types. The SDK takes `SkiaSharp.Skottie` (with
  `SceneGraph` and `Resources`, pure managed packages) as a package reference the way it takes `SkiaSharp`, so the
  assemblies resolve from the host's copy; the first task confirms they reach the host output. A plugin-private Skottie
  was rejected because the host build copies only each plugin's own DLL to `plugins/` and the loader would log the
  stray DLLs as "no widgets"; a host service on `IWidgetHost` was rejected because its only gain was a shared parse
  cache. Cost: every SDK consumer gains three dependencies, and Skottie's version moves with SkiaSharp's (already true
  of SkiaSharp itself, see the parked SDK versioning policy in item 1).
- **Assets (settled 2026-10-08): the icons are embedded resources in the weather DLL**, read with
  `GetManifestResourceStream`. The shadow copy carries only the DLL, `.pdb` and `.deps.json`, so loose files next to a
  plugin would not follow it. The one Meteocons file measured is 5 KB, so about thirty icons are small; load them
  lazily, one at a time. The asset policy for third-party widgets that ship large media is a separate, parked question.
- **Motion:** the Lottie loop needs a poster frame for `--snapshot` and for the first paint (`IsAnimating` rules in
  `animation`), and a motion level (at least "off" = freeze on the poster frame) because it is the first animation
  that never stops. Time stays `context.Time`; a pinned animation time is only needed if snapshots must differ from
  the poster frame. Not done by the weather change: wake requests, a transition helper, backing off when busy.
- **Still to check in the proposal or the first tasks:** Skottie parsing a Meteocons file with every layer type we use
  (only `partly-cloudy-day` was tried), the real size of the icon set, and the exact wording of the Open-Meteo and
  Meteocons terms (read on 2026-10-08 through a summarising fetch, so copy the text from the pages themselves).
- **Licences and attribution (read 2026-10-08; compliance is a requirement, not a nicety).** Each item becomes a task
  of the change, and none ships before the code that uses it:
  - **Open-Meteo data is CC BY 4.0.** Credit, a link to the licence, and an indication of changes are required, and
    Open-Meteo asks for a link "next to any location Open-Meteo data are displayed", for example "Weather data by
    Open-Meteo.com" linking to `https://open-meteo.com/`, with no exception for apps. So the 4x2 card always draws that
    credit line, and no widget setting hides it (the "empty hides the label" convention does not apply). Because the
    credit travels with the data, a stats slot pointed at a `weather:` reading needs it too: the proposal decides how
    (a leaning: `ReadingDescriptor` gains an attribution text and link that any widget showing the reading draws, so
    the stats widget can honour it). Our conversions (units, the portable condition classes) go into the notices as
    "data converted".
  - **The free tier is for non-commercial use**: private or non-profit apps with no subscriptions or advertising.
    UrDeck is free and open source, so it qualifies; each user's PC is its own caller. The widget and provider docs say
    that commercial use needs an Open-Meteo API key. Supporting an optional `apiKey` setting would bring provider
    settings and secrets back and is parked.
  - **Geocoding data is GeoNames (CC BY).** Credit it in the notices file; whether the card also needs a GeoNames line
    is a judgment call for the proposal (the conservative reading is yes, in the same credit line).
  - **Meteocons is MIT, Copyright (c) 2020-2024 Bas Milius**, for every style and format including the Lottie files. The
    notice must be included with copies, and the icons are embedded in a DLL we distribute, so the licence text goes in
    the third-party notices (item 12), in the README licence table, and next to the icon sources as
    `Meteocons-LICENSE.txt`, the pattern the Inter font already follows. Keep each file's metadata intact.
  - **The Open-Meteo server code (AGPL v3) does not apply**: we call the API and ship none of its code.
  - SkiaSharp and Skottie are MIT and need the same notice entry (item 12 already plans it for SkiaSharp).
- Parked: named location aliases, a disk cache for geocoding results, localised condition text, the Windows location
  API (IP geolocation is rejected: it leaks the location to a third party).

The web page widget is a requirement, not an extra: it also covers Twitch chat and dashboards with no per-site work.
It costs browser processes per instance, paid only by users who add one.

Conventions settled in the theme exploration (2026-10-03), to follow in every widget:

- A label belongs to the reading, not to the card. There are no host-drawn title bars. A widget that needs a label draws
  it with the readout or text line.
- Label text is a per-widget setting with three states: not set (the widget picks a default from what it points at, for
  example "Core 2"), text (the user's override) and empty (hidden). A widget with several readings has one such setting
  per reading. There is no common "title" field on the base `WidgetConfig`.
- A widget reads its grid size from its config and picks a composition for that size. Components are size-agnostic: they
  draw into whatever rectangle the widget gives them.
- Each widget owns its configuration type. A per-widget editor UI is part of item 11, not of the widget changes.

Notes: sensors that need elevation likely via LibreHardwareMonitor in an opt-in helper behind the `system` provider
(see item 3; its licence, driver and security-tool status are still to be verified); weather
via a keyless API such as Open-Meteo; Meteocons (MIT, full-colour, Lottie) is a candidate for animated weather art.

## 8. Pages and touch

**Status (2026-10-09):** pages, swipe, the indicator and the no-focus window are done (`pages-and-pager`). Taps are
done (`shortcut-and-tap`): a widget opts in with `UrDeck.Sdk.Input.ITapTarget`, the host finds the widget under the
pointer, shows the press from pointer-down with the theme's `press` values and delivers the tap on release; a swipe, a
vertical drag and a long hold never tap. Taps and presses also reach the widgets of the dock (`dock`), which is asked
before the page. Still open: routing scrolls and a long press to widgets (further interfaces
beside `ITapTarget`; the recognizer already ends a vertical drag and a long hold without an event of their own),
swiping over a web view, per-page backgrounds (item 9) and keyboard focus for a web view.

Multiple pages with swipe navigation and a page indicator; tap/touch interaction routed to widgets (through the input
layer of the host chosen in item 14; add an input API to the SDK). **Model:** Sonnet, Opus for the input API design.

Decided 2026-10-04: touch on the panel is for using the deck (swipe pages, tap, scroll inside a widget, interact with
a web widget), never for editing it; see item 11. The host sees every touch first and arbitrates: a horizontal swipe
changes page, a vertical pan scrolls the widget under the finger, and widgets declare what they consume. A widget that
scrolls natively (a chat, a list) needs pan gestures with inertia from the host and a shared scroll component.

## 9. Backgrounds

Static image, video, and generative visualizations per page (the `background` config field exists but is unused).
Video is expensive: gate it behind the performance budget and measure. **Model:** Sonnet.

Confirmed 2026-10-04 as a platform goal: a custom image, a video file, and a painted visualization (OpenGL or
Direct3D), behind translucent cards. This is the main reason composition moves to the GPU (item 14), and the spike
there measures a video background. The decoded-video layer built here is also what a camera widget would use.

## 10. Dock / quick launch

Launcher bar for apps and URLs (the `dock` config field exists but is unused). **Model:** Sonnet.

**Status (2026-10-09):** implemented in `dock` (`openspec/changes/dock`; its `design.md` holds the decisions and the
rejected alternatives). The owner checked it on the panel (`docs/perf/dock.md`). What was settled:

- The dock is a second reserved band at the very bottom of the screen, below the page indicator's. It is reserved
  whenever `dock` has an entry, and the indicator is the element that gives way (`auto` becomes `fade`).
- Four equal, square slots, shown as one centred group. The slot's side is the band's height, the theme's
  `dock.height` (0.71 of a cell: 195 pixels on the panel, which keeps all 13 rows).
- An entry of `dock` is a widget object without a position, normally a shortcut. Any widget type with a 1x1 size can
  sit in a slot: the theme is resolved a second time for the slot's size, so the card is a miniature drawn by the same
  code, and the SDK did not change.
- The dock's views live beside the indicator, outside the pages: they are built once per layout, do not move during a
  swipe and keep their readings and icons across page changes.
- Input: the indicator first, then the dock, then the page. A point in the dock's band that hits no card goes nowhere.

Left out on purpose: more than four slots, a scrolling dock, a backdrop bar behind the slots, per-page docks, drag and
drop (item 11).

## 11. WYSIWYG editor

Drag/drop placement on the grid, resize within supported sizes, property editing from each widget's config type,
widget palette with performance tiers. Likely a separate window on the primary monitor editing the live page.
**Model:** Opus for design, Sonnet for implementation.

Decided 2026-10-04: the standalone editor does all editing (add, configure, move, resize, delete). Nothing is edited on
the panel itself: Nexus splits editing between an app and the touch screen, which is inconsistent, and other screen
types may have no touch. The editor therefore draws its own preview of the page (`PageRenderer` already renders a page
to a bitmap) and can stay loosely coupled to the display by writing the config, which the host already reloads. Its UI
framework follows the host decision in item 14: the same framework if WinUI 3 passes the spike, a separate one if not.

## 12. Packaging and distribution

Installer, start with Windows, auto-update, logo and branding, winget manifest and GitHub release automation, and a third-party notices file
(SkiaSharp is MIT and must be attributed in binary releases). **Model:** Sonnet.

Tray icon with Quit: done (`tray-icon`). The menu is a list of entries, so the editor's "Open editor" entry (and a
single-instance guard, which would also be the place to bring the first copy's editor forward) are added later. Still
open here: using the `icon` config field for the tray icon (moved from `urdeck-framework`).

## 13. SDK distribution and other displays

- Publish `UrDeck.Sdk` as a NuGet package with the analyzer bundled; a `dotnet new` widget template.
- Marketplace: start as a git-based registry repo (winget-pkgs / Scoop bucket model): widgets are added by PR with a
  manifest (id, version, SDK version range, SPDX license, requested capabilities, file hash); CI validates it and the
  host reads a static index. Widgets are unsandboxed .NET DLLs, so plan signing/review and capability declarations early.
- Cross-brand support: some case screens are Windows monitors (like the Y70), others are USB LCDs driven by vendor
  protocols. `PageRenderer` already renders a page to a bitmap, which is the basis for "display backends" that push
  frames to non-monitor devices.

## 14. Animation and the rendering path (changes `render-path` and `animation`, both done)

**Why:** continuous motion is a design target (decided 2026-10-03): animated weather icons that loop like Nexus, music
visualizers, chart and value transitions. Today a widget can only repaint on a fixed timer, and every repaint redraws
its whole card in software and copies the bitmap to WPF. Nobody has measured what a looping animation costs on the
panel, and the answer may change how all widgets are rendered, so this comes before more widgets are built.
**Model:** Opus for the exploration and design (it fixes public SDK API and possibly the host's rendering architecture),
Sonnet to implement.

**Status:** explored on 2026-10-03/04; `render-path` is implemented on `feat/render-path` (`openspec/changes/archive/2026-10-04-render-path`):
the WinUI 3 host, with plugin hot-reload verified and a measured baseline (`docs/perf/render-host-baseline.md`).
**Next step:** `animation` is done and archived (`openspec/changes/archive/2026-10-04-animation`); the weather change (4x2) is done; the 4x4 weather widget is next.
Background: [docs/handoff/2026-10-04-render-host-spike.md](handoff/2026-10-04-render-host-spike.md).

Decided in the exploration:

- **Composition moves to the GPU.** Video and painted backgrounds behind translucent cards (item 9), page swipes
  (item 8) and a web page widget (item 7) all need layers composited on the GPU. Software WPF composition, WPF's
  hardware mode and `SKGLElement` are not candidates. Widgets keep drawing on an `SKCanvas`, so the SDK, engine, widgets
  and `--snapshot` are expected to survive; `src/UrDeck.Host` is rebuilt.
- **The host framework is WinUI 3**, chosen by a spike (`docs/perf/render-host-spike.md`). It ships a layered web view,
  a media element and gestures, and serves the editor too. On the panel it passed the memory check (a full page with
  video, an animated icon and a web view: 419 MB private and 10.5% of one core, against 627 MB and 20% for Nexus) and
  the layering check (translucent Skia cards and a web view over video). Sleep and resume are tested by the owner
  on the panel as the last gate of `render-path`. The fallback is a plain Win32 window with DirectComposition. All framework
  code stays inside the host.
- **The bar is Nexus** (`docs/perf/nexus-baseline.md`), with "a page with nothing moving costs close to nothing" kept
  as the principle.
- **Two changes, in this order:** `render-path` (the new host), then `animation`.
- **The first animation consumer is the Clock** with a split-flap style. The weather widget is its own change after
  `animation`; Lottie through `SkiaSharp.Skottie` (a build matching the repo's 4.153 exists) arrives with it.

Built in `animation`: a widget reports `IsAnimating`; one host frame clock at 30 fps runs only while some widget does;
frames never refresh data. Deliberately left out and revisited by the weather change, the first widget that loops: wake
requests (a repaint at a wall-clock time), a monotonic or pinnable animation time, a transition helper, a user motion
level, backing off when the PC is busy (needs `system.cpu` from item 3), smaller or GPU-backed surfaces. Details in the handoff.
The 2026-10-08 weather exploration took the poster frame and the motion level; see "Weather widget" under item 7.

Out of scope here: the weather widget itself, the background feature (item 9; the spike only measures one), page
navigation (item 8; the spike only tries a swipe), display targeting (item 6).

## 15. Ideas not yet scheduled (added 2026-10-08)

Collected while the milestones were drawn up. Each names the milestone that needs it; none has an OpenSpec change yet.

- **Per-user data folder** (milestone 2). Config, themes, plugins and `urdeck.log` live next to the executable today,
  which an installer into a protected folder and an updater that replaces the folder both break. Move them to a
  per-user folder and keep "next to the executable" as a portable mode.
- **Config format version and migrations** (milestone 2, proven by 1.0). The file has no version. Add one before the
  first public release, so that every later change to the format can carry a migration.
- **First run** (milestone 2). With no config the app should place a default page that suits the monitor it finds, and
  say in the log and the tray where the config is.
- **Landscape and other shapes** (milestone 2). The grid is four square columns from the monitor width, which gives a
  1920x1080 monitor two rows of very large cells. Decide whether the column count follows the monitor's shape or is a
  page setting. This touches the grid spec and every widget's composition, so decide before the editor is built.
- **Code signing** (milestone 2). An unsigned download is blocked by SmartScreen, which is a poor start for a project
  whose point is trust. Look at the free signing programmes for open source (SignPath Foundation is one to verify).
- **Hardware reports** (milestone 2). An issue template that asks for the lines `urdeck.log` already writes (monitors,
  GPUs and the source of each reading), and a compatibility table in the docs fed from them.
- **Settings metadata in the SDK** (milestone 3). Labels, ranges, choices and "this is a reading id" or "this is a
  colour" on a widget's config type, so the editor can build its form. Decide between attributes on the config type
  and a description the widget returns; either is public API.
- **Pause when nobody can see the page** (milestone 4). Stop the frame clock and the providers while the session is
  locked or the display is off, following "the user pays only for what they switch on". Shares the busy and battery
  back-off parked in items 3 and 14.
- **Network statement and declared capabilities** (milestone 6). A short document listing every request the app and
  the first-party providers make. For plugins, a declared capability (network, elevation) that the editor shows before
  a widget is added; the marketplace manifest in item 13 already plans the same field. To list, as of
  `shortcut-and-tap`: the engine itself fetches site icons (only for a widget whose icon source is an `http(s)`
  address; the address, its web manifest and at most four icon candidates, which may be on another host over `https`;
  at most once per address in 30 days; no cookies or credentials), beside the weather provider's requests.
- **More `system` readings.** Network throughput, disk use and activity, fan speeds, battery. Each is a few catalog
  entries once its source is known; do them when a user asks.
- **More providers and widgets.** Home Assistant (also the first user of provider settings and secrets), calendar or
  agenda, an audio visualizer (named as a design target in item 14), a timer.
- **Shared pages.** A page and its theme exported as one file that somebody else can import, as the step before a
  marketplace. An imported page must not bring launch targets in silently: a shortcut's `target` and `arguments` are
  trusted like a `.lnk` on the desktop because the user wrote them, so an import has to show them and ask for consent
  (or strip them) before they can be tapped. The same goes for an `icon` or `target` that is a web address, which
  makes the app send a request.
