# UrDeck Roadmap

UrDeck is a lightweight, extensible widget dashboard for secondary and case displays (first target: the HYTE Y70 Touch
1100x3840 portrait panel), built as an open source alternative to HYTE Nexus. Priorities: a page you can trust and
read the source of, low resource use, great visuals, a modular widget SDK, a WYSIWYG editor, and touch.

This page is the plan in outcomes. The work items behind it, with their design notes and status, are in
[BACKLOG.md](BACKLOG.md); "item N" below is a section there. Last updated 2026-10-09.

## Where it is today

The foundation is in place and runs on the real panel: a WinUI 3 host that composites each widget on the GPU, a plugin
SDK with hot reload, themes, animation, data providers, pages you swipe between, and the Clock, Stats, Weather and
Shortcut widgets. There is no
installer and no editor yet: you build from source and edit a JSON file. See the [README](../README.md).

## Milestones

A milestone is an outcome somebody can check, not a list of features, and none has a date: this is a spare-time
project. They are listed in the planned order. Milestones 3 and 4 can swap. The one fixed rule is that the SDK is
frozen last (milestone 5), because the editor and the web page widget both still change its public API.

| # | Milestone | Done when | Status |
|---|---|---|---|
| 0 | Foundation | Plugins, themes, GPU host, animation and data providers work on the panel | done |
| 1 | Daily driver | The owner's PC runs UrDeck in place of HYTE Nexus for two weeks, sleep and resume included | in progress |
| 2 | Public preview (0.1) | Somebody else with a case display downloads a release and has a working page in five minutes, without the .NET SDK | not started |
| 3 | Editor (0.5, beta) | A new user builds and changes a page without opening a file | not started |
| 4 | Living page | A page with a video background, a web view and an animated icon runs within the Nexus bar (no more memory, CPU or GPU than HYTE Nexus) | not started |
| 5 | SDK 1.0 for authors | A widget and a provider built outside this repo, from the template and the NuGet package alone, load in a release build | not started |
| 6 | UrDeck 1.0 | Installs, updates and migrates its config by itself; the SDK and the config format are covered by a compatibility promise | not started |

**0. Foundation (done).** SDK and analyzer, the SDK/Engine licence split, hot-reloading plugin loader, themes and the
host-drawn card (item 5), the WinUI 3 host and the frame clock (item 14), data providers with the `system` and
`weather` providers (item 3), Clock, Stats and Weather 4x2 (item 7).

**1. Daily driver.** Everything on the owner's current Nexus page, and nothing that forces a restart of the app.
- The 4x4 weather widget (item 7).
- Pages, swipe and the page indicator, and taps on widgets (item 8; done).
- The shortcut widget, which brought tap input, launching, icons and the image tile (item 7; implemented in
  `shortcut-and-tap`, its checks on the panel are still open).
- The dock, a row of shortcut slots at the bottom of every page (item 10; next).
- Recovery after sleep, hot-plug and monitor wake order (the robustness half of item 6; the picker UI waits for
  milestone 3).
- CPU temperature through the opt-in elevated helper (item 3, fourth change). May slip to milestone 2 if the driver
  question takes long; the card shows a dash until then.
- Start with Windows, in its simplest form (item 12).

**2. Public preview (0.1).** For people who are willing to edit a JSON file. Its purpose is hardware variety: every
measurement so far is from one panel and one NVIDIA GPU, and that is the largest unknown in the project.
- A portable zip built and published by a release workflow, with the third-party notices (item 12).
- Config, themes, plugins and the log in a per-user folder instead of next to the executable (item 15).
- A config format version and a first migration (item 15).
- A first run that produces a sensible page on any monitor, including a landscape one (item 15).
- A tray icon with Quit (item 12; shipped in `tray-icon`), later the entry that opens the config editor.
- Code signing, so the download is not blocked by SmartScreen (item 15).
- A hardware report template and a compatibility table: AMD and Intel GPUs, other panels (item 15).
- Screenshots and a short clip in the README, issue templates, Discussions.

**3. Editor (0.5, beta).** Item 11, plus what it needs from the rest:
- Settings metadata in the SDK, so the editor can build a form from a widget's config type (item 15). This is public
  SDK API and the main reason milestone 5 comes after this one.
- The reading picker with live values (item 3) and the theme picker.
- The monitor picker with friendly names (the UI half of item 6).

**4. Living page.** What makes the page worth looking at, and the reason composition moved to the GPU.
- Backgrounds: image, video, painted visualization (item 9).
- The web page widget, a hosted widget kind (item 7). Also public SDK API.
- Media / now playing (item 7).
- The motion level as a theme or page setting, and the later theme knobs (items 5 and 14).
- Pausing while the session is locked or the display is off (item 15).

**5. SDK 1.0 for authors.**
- The parked list under item 1: loader hardening, SemVer and the minimum compatible version, public API tracking, the
  SkiaSharp range, the build guard, SDK tests.
- `UrDeck.Sdk` on NuGet with the analyzer bundled, and the `dotnet new` template (item 13).
- Benchmark mode and published tiers (item 4), so an author can state what a widget costs.
- Provider settings and secrets (item 3), proven by one provider that needs them (Home Assistant is the candidate).
- Author documentation: a walkthrough from the template to a loaded widget.

**6. UrDeck 1.0.** Installer, auto-update, winget manifest, final logo and branding (item 12); every config written
since 0.1 still loads; a written statement of what the app sends over the network and to whom (item 15).

**After 1.0.** The marketplace registry, display backends for USB LCDs, splitting the template and the SDK into their
own repos (item 13), the camera widget (item 7).

## Following along and helping

- Progress per item, and what is being worked on now: "Current state" in [BACKLOG.md](BACKLOG.md).
- Want something that is not here, or want it sooner? Open an issue. Reports from hardware other than the Y70 and an
  NVIDIA GPU are especially useful, even before milestone 2.
- Want to build it? Start with [CONTRIBUTING.md](../CONTRIBUTING.md).
