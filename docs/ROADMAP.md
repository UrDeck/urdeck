# UrDeck Roadmap

UrDeck is a lightweight, extensible widget dashboard for secondary/case displays (first target: the HYTE Y70 Touch
1100x3840 portrait panel), replacing HYTE Nexus. Priorities: low resource use, great visuals, a modular widget SDK,
a WYSIWYG editor, and touch.

This document is the hand-off point for work sessions. Each item below should become (or already is) an OpenSpec
change under `openspec/changes/`. Work one item per session.

## How to run a work session

1. Read `README.md`, this file, and the item's OpenSpec change (create it with the OpenSpec propose workflow if
   it doesn't exist yet; `openspec validate --all --strict` must pass).
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

## Current state (2026-10-03)

- `urdeck-framework` (Phase 1 foundation) is complete and archived: SDK, analyzer, plugin loader with hot-reload
  (collectible AssemblyLoadContext), grid layout, WPF host with per-monitor DPI placement, Clock widget, tests, placeholder
  icon and render skipping (`NeedsRender`).
- Repo structure, licensing, the rename and the SDK/Engine split are done (item 1). The repo is `UrDeck/urdeck`.
- `theme-and-card` (item 5) is implemented (themes as data, host-drawn card, gap in the layout, readout and text
  line components, Clock migrated); see item 5.
- Proposed, not started: `display-targeting` (older draft, to be reworked with `/opsx:explore` then `/opsx:propose`), and `data-providers`
  (item 3, not written yet).
- Item 14 was explored on 2026-10-03/04. Outcome: composition moves to the GPU and the host is rebuilt on WinUI 3. A
  spike on the panel passed the memory and layering checks (`docs/perf/render-host-spike.md`); a plain Win32 window
  with DirectComposition stays the fallback. See item 14 and `docs/handoff/2026-10-04-render-host-spike.md`.
- `render-path` (the WinUI 3 host) is implemented on the branch `feat/render-path` (PR open); all its tasks are verified, and it is waiting to be archived and merged.
- Next: archive and merge `render-path`, then explore and propose `animation`; then item 3 (data providers). Each in a fresh
  session starting from its change or handoff. See "Suggested order" below.
- Memory: the WinUI 3 host is ~101 MB private / ~135 MB working set (Release, one Clock), 0% CPU and GPU idle
  (`docs/perf/render-host-baseline.md`). The WPF host it replaced was ~66 MB (`docs/perf/memory-investigation.md`).
  The bar is "no worse than Nexus" (`docs/perf/nexus-baseline.md`), see item 4.

### Suggested order

Item numbers are identifiers, not a sequence. The order below gets the owner's current first page (clock, weather,
performance, shortcuts, dock, page indicator) rebuilt with the fewest blocked steps:

1. **Item 14, rendering path and animation.** First because it replaces the host every widget is drawn in. The render
   host spike is done; next is the `render-path` change (`openspec/changes/render-path`, the WinUI 3 host), then the
   `animation` change (frame requests and an animation clock, with a split-flap Clock style as first consumer).
2. **Item 3, data providers.** Handoff: `docs/handoff/2026-10-03-data-providers.md`.
3. **Weather widget** (item 7), which brings glyphs and animated colour icons. Needs item 14. It fetches its own data,
   so it does not need item 3.
4. **Performance widget** (item 7), which brings the gauge component. Needs item 3.
5. **Pages and touch** (item 8), then **shortcuts and dock** (items 7 and 10), which bring the image tile.

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
docs/                    # ROADMAP.md, perf/, architecture notes
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
- [ ] **Services and logging for widgets:** widgets log nothing today. When the first one needs to (weather), use
  `Microsoft.Extensions.Logging.Abstractions`: the SDK exposes `ILogger` (for example a protected `Logger` on `Widget<T>`) and the
  engine supplies an `ILoggerFactory` that writes to `urdeck.log`. No custom logging interface. A general host-services hook
  (the design suggests `IWidget.Attach(IWidgetHost)`) can come with it.
- [ ] **SDK tests:** the SDK is only covered indirectly through the engine tests; add `UrDeck.Sdk` tests and a floor (the design
  suggests 80%).
- [ ] **Plugin exception wording:** optionally name theme/data packages, non-widget plugin kinds and the SkiaSharp types the SDK
  exposes once those plugin kinds exist.

- **Repo strategy:** monorepo through 1.0 (atomic SDK/engine/host/widget changes while the API churns). Later, split along
  the existing seams: `urdeck/widget-template` (MIT template repo, worth doing soon) and a git-based marketplace
  registry repo (see item 13). Split the SDK out only once it is stable.

## 2. Roadmap document

This file. Keep "Current state" and item status up to date at the end of each session.

## 3. Data providers (new `data-providers` change)

**Why:** `[RefreshOnEvent]` and `[RefreshAdaptive]` (the last open parts of the old framework change) only make sense with
shared data: several widgets that all want CPU/GPU/memory (or weather, now-playing) should not each poll.
**Model:** Opus for the design (it fixes public SDK API), Sonnet to implement.

Done in `urdeck-framework`: skipping redundant redraws via `IWidget.NeedsRender(DateTime now)` (the Clock repaints once a
minute) and the placeholder icon.

**Next step:** a fresh Opus session running `/opsx:explore` from
[docs/handoff/2026-10-03-data-providers.md](handoff/2026-10-03-data-providers.md), which adds the first consumer (the
performance widget) and the open questions to the direction below.

Design direction, to be validated with `/opsx:explore` then `/opsx:propose` and an Opus review:

- A latest-value store plus a change signal, not a pipeline framework: named **topics** (`system.cpu`,
  `media.nowplaying`), each with a typed latest value.
- **Providers** are a plugin kind in the SDK, so community authors are not locked into official data; official providers use
  the same public API. One provider per topic, owned by the engine. Polled providers implement `Sample()` and the engine
  schedules them at the fastest rate any subscriber needs; pushed providers publish when something happens (media, power).
- **Demand-driven:** a provider starts when its first widget subscribes and stops with the last. Subscriptions are dropped on
  plugin reload.
- **Consuming:** a widget declares the topics it uses (replacing the bare `[RefreshOnEvent("name")]`) and reads the latest
  value at draw time (a cached read, no I/O); the engine redraws it when a topic changes.
- **Adaptive refresh** (`[RefreshAdaptive]`, today it runs at `minMs`) becomes a consumer of the `system.cpu` topic: back off
  when the machine is busy (a case display often runs next to a game) and on battery.
- Hard problems to settle in the design: sharing payload types across plugin load contexts (standard payload types in the SDK
  plus a generic schema'd value for community topics; shared contract assemblies later), engine-owned scheduling and fault
  containment so a bad provider cannot spin or hang, topic naming and collisions, naming providers in
  `PLUGIN-EXCEPTION.md`, and which "Parked" SDK items (loader hardening, versioning, API tracking) must land first.
  Optional later: providers that subscribe to other topics (derived data).
- Keep the first implementation minimal. First-party providers (system CPU/memory) serve the default widget set (item 7).

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
| Clock | 4x2, 4x1, 2x1, 1x1 | time, date; a split-flap style arrives with `animation` (item 14) | done | readout, text line (done) |
| Performance | 2x2 (one stat), 4x2 (two gauges, three text stats), 4x4 (four gauges, three text stats) | CPU and GPU temperature and load, memory, GPU power and clock | item 3 | gauge ring (a ring around a readout) |
| Single stat | 1x1 | one reading, for example one CPU core per card | item 3 | nothing new; may be the performance widget at its smallest size |
| Weather | 4x2 | animated colour icon, temperature, condition, high and low, place, sunrise and sunset | item 14 | tinted glyph, colour or animated icon |
| Shortcut | 1x1 | an app or URL icon that launches on tap | item 8 (touch) | image tile, shared with the dock (item 10) |
| Media / now playing | open | track, artist, art, controls | items 3 and 8 | image tile reuse |
| Web page | open | any web page, with touch (the owner shows Frigate camera feeds this way in Nexus) | item 14 (new host), item 8 | a hosted widget kind: the host places a web view layer instead of calling `Render` |
| Camera (parked) | open | camera streams without a browser, for example from Frigate's go2rtc | item 9 for real video | reuses the video layer; a first version could draw snapshots on the canvas |

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

Notes: sensor data likely via LibreHardwareMonitor (some sensors need admin; run it as a provider, see item 3); weather
via a keyless API such as Open-Meteo; Meteocons (MIT, full-colour, Lottie) is a candidate for animated weather art.

## 8. Pages and touch

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

Installer, start with Windows, tray icon (and using the `icon` config field for it, moved from `urdeck-framework`), auto-update, logo and branding, winget manifest and GitHub release automation, and a third-party notices file
(SkiaSharp is MIT and must be attributed in binary releases). **Model:** Sonnet.

## 13. SDK distribution and other displays

- Publish `UrDeck.Sdk` as a NuGet package with the analyzer bundled; a `dotnet new` widget template.
- Marketplace: start as a git-based registry repo (winget-pkgs / Scoop bucket model): widgets are added by PR with a
  manifest (id, version, SDK version range, SPDX license, requested capabilities, file hash); CI validates it and the
  host reads a static index. Widgets are unsandboxed .NET DLLs, so plan signing/review and capability declarations early.
- Cross-brand support: some case screens are Windows monitors (like the Y70), others are USB LCDs driven by vendor
  protocols. `PageRenderer` already renders a page to a bitmap, which is the basis for "display backends" that push
  frames to non-monitor devices.

## 14. Animation and the rendering path (changes `render-path` and `animation`, not written yet)

**Why:** continuous motion is a design target (decided 2026-10-03): animated weather icons that loop like Nexus, music
visualizers, chart and value transitions. Today a widget can only repaint on a fixed timer, and every repaint redraws
its whole card in software and copies the bitmap to WPF. Nobody has measured what a looping animation costs on the
panel, and the answer may change how all widgets are rendered, so this comes before more widgets are built.
**Model:** Opus for the exploration and design (it fixes public SDK API and possibly the host's rendering architecture),
Sonnet to implement.

**Status:** explored on 2026-10-03/04; `render-path` is implemented on `feat/render-path` (`openspec/changes/render-path`):
the WinUI 3 host, with plugin hot-reload verified and a measured baseline (`docs/perf/render-host-baseline.md`).
**Next step:** archive and merge `render-path` (all tasks verified on the panel), then explore
`animation`.
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

Proposed for `animation`, not yet agreed: a widget asks for frames while it renders (and stops by not asking), one
frame clock in the host that owns the rate and the limits (cap, motion level, display asleep, PC busy), a monotonic
animation time that `--snapshot` pins, and data refresh on its own slow cadence. Details in the handoff.

Out of scope here: the weather widget itself, the background feature (item 9; the spike only measures one), page
navigation (item 8; the spike only tries a swipe), display targeting (item 6).
