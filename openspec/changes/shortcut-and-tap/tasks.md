## 1. Record the spikes

- [x] 1.1 Create `docs/perf/shortcut-and-tap.md` with the three spikes of 2026-10-09 as `design.md` describes them (shell icon extraction: targets, sizes, timings, row order and alpha; launching from a no-activate window: the eight launches, which monitor, the one failure and why it is discounted; site icon probing: the two site shapes), with no real host names

## 2. SDK

- [x] 2.1 Add `UrDeck.Sdk.Input.ITapTarget` (`CanTap` with a default of `true`, `OnTap`) with doc comments that state the rules of the widget-sdk spec
- [x] 2.2 Add `UrDeck.Sdk.Launch.LaunchTarget.Parse` (kind, normalised text, display name; pure, no I/O) and tests for every scenario of the launcher spec's "Launch Targets" plus: an empty and a whitespace target, `www.` stripped from the display name, a one-letter scheme is a path, a bare name, a malformed web address
- [x] 2.3 Add `ILauncher` and `IIconSource` with `IconResult`, their null implementations, and `Launcher` and `Icons` as default members of `IWidgetHost`; offer both as protected members on `Widget<TConfig>` that work before `Attach`
- [x] 2.4 Add the `ImageTile` component (fit and centre, smooth sampling, the 2x enlargement cap, optional label below, placeholder square with one letter, empty placeholder) and tests for each scenario of the components spec, rendered to a bitmap
- [x] 2.5 Move the package version to 0.7.0 with a note in the csproj comment, keep `AssemblyVersion`, and confirm `SdkContractTests` passes unchanged

## 3. Engine: gestures and hit testing

- [x] 3.1 Add `GestureKind.Pressed` and `GestureKind.PressCancelled` to `GestureRecognizer` (raised as design decision 2 lists) and extend `PagerTests` (or a new test class): down then up is pressed then tap; a horizontal drag is pressed, press cancelled, swipe started; a vertical drag is pressed, press cancelled; a long hold is pressed, then press cancelled on release; a cancel while pending; a second pointer changes nothing; `Abort` raises nothing
- [x] 3.2 Add the point-to-widget lookup over `WidgetLayoutItem`s (card rectangle, not cell; unplaced and invisible items skipped; returns the index and the point relative to the card) with tests for a card, a gap, an empty cell, the card's edge pixels and an unplaced widget

## 4. Engine: theme

- [x] 4.1 Add `Press { Scale, Opacity }` to `ThemeDefinition` (merge, sanitise to 0.5..1 and 0.1..1 with the usual warning) and a resolved `PressStyle`; add the values to `default-dark`, `default-light` and `glass`
- [x] 4.2 Theme tests: an older theme without press values takes the default's, an out-of-range value warns and falls back, a user theme overrides one of the two

## 5. Engine: services

- [x] 5.1 Replace the shared `WidgetHost` with a per-widget services object (readings and log forwarded as before, `RepaintRequested`, `Dispose` releasing what the widget holds); `CreateWidget` hands it out with the widget; existing tests still pass
- [x] 5.2 The launcher: shell execute with the default verb, working directory for executables, failures as a log line and `false`, the one-second repeat guard (on an injected time source), the log line without arguments, the empty "before launch" callback; tests through a fake process starter for valid, invalid, refused and repeated launches and for arguments given with an address
- [x] 5.3 Shell icons: the STA worker thread (lazy, ends when idle), `IShellItemImageFactory` at 256 pixels, row order from the DIB header, the alpha rule of design decision 6; a Windows-only test that an executable in `%WINDIR%` yields an image with transparent pixels; render the spike's target set to PNGs once, look at them (upright, clean edges) and note the result in the perf note
- [x] 5.4 Image file sources: decode by extension list through `SKCodec`, a missing or undecodable file is "none" with one log line; tests with a PNG and an ICO fixture and a text file renamed to `.png`
- [x] 5.5 Site icon discovery behind a small HTTP interface: the tolerant `<link>` scanner, the manifest reader, candidate ordering, the checks (decodes, shorter side at least 48, no SVG, cross-host only over https, at most four downloads, five redirects, size and time limits); tests on recorded `example.com` fixtures for: a manifest with sized PNGs and a wanted size of 256, a page with only `apple-touch-icon`, only `/favicon.ico`, a 16 pixel favicon, a direct image address, a site that answers everything with a sign-in page, a maskable-only manifest, an `http` cross-host link, a truncated body
- [x] 5.6 The real HTTP client (no cookies, no credentials, redirect cap, timeout, `User-Agent`, certificate errors are failures) created on first use
- [x] 5.7 The disk cache (`cache/icons`, SHA-256 file names, PNG at most 512 pixels, 30 day age from the write time, background refresh that keeps the old icon on failure, unreadable file replaced, unwritable folder falls back to memory with one log line) and the per-run negative list; tests on a temporary folder with an injected time source
- [x] 5.8 The icon service itself: classification into image file, shell item, web address (site icon, then shell fallback) and other address; entries keyed by source and size bucket; reference counts per services object and disposal at zero; `RepaintRequested` when an entry settles; "any pending"; tests with fake loaders for sharing, release on dispose, one repaint per settle, loading versus none, and that nothing is requested without a web source
- [x] 5.9 A plugin-reload test that a widget which asked for an icon and was disposed leaves its load context collectible

## 6. Host

- [x] 6.1 `PageHost`: find the view under a point with the engine lookup; `WidgetView`: keep the widget's `ITapTarget` (cleared on dispose), safe `CanTap` and `OnTap` wrappers that log a throwing widget, a repaint check after `OnTap`, subscribe to `RepaintRequested` and invalidate on the dispatcher, dispose the services object
- [x] 6.2 `WidgetView` press and release animations on its composition visual (scale around the centre and opacity from `PressStyle`, about 80 ms in and 160 ms out; nothing started when both values are 1)
- [x] 6.3 `PagerController`: on `Pressed` find the view, ask `CanTap`, press it; on `PressCancelled` and `Tap` release it; on `Tap` the indicator first, then deliver the tap in card pixels; clear a pressed view in `Release` and before a new press
- [x] 6.4 `SnapshotCommand` waits for pending icons as it waits for pending readings (a few seconds at most)

## 7. Shortcut widget

- [x] 7.1 Create `widgets/UrDeck.Widgets.Shortcut` (GPL header, references only the SDK), add it to `urdeck.slnx`, the `UrDeckPlugin` list in `UrDeck.Host.csproj` and the engine test project; `ShortcutConfig` (`target`, `arguments`, `icon`, `label`) with unknown properties preserved
- [x] 7.2 `ShortcutWidget`: parse the target in `OnConfigured` and log an invalid one once; `Render` with the icon source rule, the wanted size from the content rectangle and the image tile; the placeholder text rule; `CanTap`, `OnTap`, `NeedsRender`
- [x] 7.3 Tests with fake services: the minimal shortcut, the icon override asks only for the override, loading shows the placeholder letter and the ready icon replaces it, a label text is drawn and an empty or unset one is not, no target declines taps and launches nothing, a tap launches once with the arguments, a failed launch changes nothing, a tap needs no repaint, the config round-trips an unknown property
- [x] 7.4 Add a shortcut to the sample `urdeck-config.json` only if a target exists on every Windows machine (for example the documents folder); otherwise leave the sample alone and document the widget in the README

## 8. Verify (automated)

- [x] 8.1 `dotnet build urdeck.slnx -c Release` with 0 warnings, `dotnet test urdeck.slnx -c Release`, the engine coverage floor, `dotnet format urdeck.slnx --severity warn`, and `openspec validate --all --strict`
- [x] 8.2 `UrDeck.Host.exe --snapshot` of a page with shortcuts to an executable, a folder, a packaged app and a web address: icons, not placeholders (the web one needs the network); and of a shortcut with no target: the empty placeholder

## 9. Verify on the panel (with the owner)

- [x] 9.1 Press feedback: a finger down on a shortcut shrinks and dims the card, a sideways drag from a shortcut releases it and changes the page, a long hold releases without launching, a clock does not react; the owner approves the look and the press values (record the final values in the perf note and `docs/themes.md`)
- [x] 9.2 Launching, by touch, with another application in front on the main monitor: the owner's two web pages, Windows Terminal and Steam, each once closed and once already running; each comes to the front, and the monitor it opened on is noted. If any stays behind, use the "before launch" seam in the order of design decision 5, repeat, and record what was needed
- [x] 9.3 Icons: the four shortcuts show the expected icons (a site icon for the site that offers one, the browser icon for the one behind a sign-in page until `icon` is set to a file, then that file); restarting shows them without a request (the log says so); a double tap opens one window
- [x] 9.4 Measure the host with a page of four shortcuts against the same page without them (private bytes, CPU and GPU at rest and during repeated presses), the time `Launch` holds the UI thread for each target, and record it in `docs/perf/shortcut-and-tap.md`; the idle cost must not rise
- [x] 9.5 A page without shortcuts starts no icon worker and makes no request; leaving the page with the shortcuts releases their icons (private bytes return after the reclaim)

## 10. Documentation

- [x] 10.1 `README.md`: the shortcut widget and its settings with examples for an application, a packaged app (`shell:AppsFolder\...` and how to find the id with `Get-StartApps`), a folder and a web address; where icons come from; the network note (which requests, when, how to avoid them with a local `icon`); the cache folder
- [x] 10.2 `docs/themes.md`: the `press` values; `CONTRIBUTING.md`: tap input for widget authors (`ITapTarget`, no pressed state in the widget, launch and icon services, the image tile)
- [x] 10.3 `docs/BACKLOG.md`: "Current state", items 7, 8 and 10 (tap input and the shortcut done; the dock next, as a fixed row of 1x1 widget slots in the bottom band whose slot size is still to be decided), a note on item 15's "shared pages" that imported launch targets need consent, and the engine's icon requests under the network statement; `docs/ROADMAP.md` milestone 1
- [x] 10.4 Update `docs/handoff/2026-10-09-shortcut-and-dock.md` so it hands off the dock change only (what this change settled, what is left), instead of deleting it
