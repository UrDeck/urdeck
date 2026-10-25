## 1. Spike: reaching the media session from a plugin

- [ ] 1.1 The documentation part is done (design decision 12). Remaining against a real player and a browser tab: which fields each reports, the playback status values and how they map to `playing`, `paused` and `stopped`, and whether a duration, position and thumbnail are present
- [ ] 1.2 Try decision 11 (a) with a throwaway provider project: a plain `net10.0` target with embedded C#/WinRT (`CsWinRTEmbedded`, `CsWinRTIncludes` for `Windows.Media.Control` and `Windows.Storage.Streams`, `CsWinRTWindowsMetadata`) that reads the current session, loaded from `plugins/` in the collectible load context from its shadow copy; check the C#/WinRT version that matches the .NET 10 SDK; then (b) if (a) does not work. Record what built, what loaded and what did not
- [ ] 1.3 In the same spike: `await` on a projected async operation, reading the thumbnail into bytes without the Windows TFM's stream adapters, subscribing to the session events from a thread-pool thread (as the host's `SystemEvents` subscription needs), and that the project still builds on Linux with the Windows file excluded
- [ ] 1.4 Whether an unpackaged app needs anything for the media session, and what the `globalMediaControl` capability would mean for a packaged Store build; pass the answer to `signing-and-store`
- [ ] 1.5 Create `docs/perf/now-playing.md` with the spike (the chosen project shape, the players checked and what each reported) and update `design.md` decision 11 to the result; if neither candidate works without changing how plugins are built, stop here

## 2. SDK and engine groundwork

- [ ] 2.1 `IIconSource.Release(string source)` as a default member that does nothing; the engine's implementation in `WidgetServices` and `IconService` per the icons spec; tests for the three scenarios plus: a source still loading when released, release then ask again, release at every size bucket
- [ ] 2.2 `ButtonRow` (`Layout`, `HitTest`) with tests for each scenario of the components spec rendered or measured without a canvas, including the minimum touch size, the weights and a rectangle too small
- [ ] 2.3 The four media glyphs in the built-in `outline` set and its manifest keys, the canonical key list and the icon-sets documentation; a test that all four resolve in the built-in set and fall through the chain for a set without them
- [ ] 2.4 Package version moves one minor with a note in the SDK csproj comment; `AssemblyVersion` unchanged; `SdkContractTests` passes unchanged

## 3. The `media` provider

- [ ] 3.1 Create `providers/UrDeck.Providers.Media` in the shape task 1 chose (GPL header, references only the SDK), add it to `urdeck.slnx`, the `UrDeckPlugin` list, `UrDeck.Preview` and the engine test project
- [ ] 3.2 `IMediaSessionPlatform` and its fake; the catalog (readings and actions) with labels and names; `IDataProvider` and `IActionProvider` over the platform
- [ ] 3.3 The session model: read properties, playback info and timeline on the platform's events into one batch per change; `status`, `can/*`, `position` with `position/at`; the "no session" and "session lost" states; reasons for unavailable readings
- [ ] 3.4 Album art: the per-run folder, the content-hash name, keep the current and the previous file, clear leftovers on start and the folder on stop, publish an empty `art` when the track has none, read the thumbnail off the UI thread once per change
- [ ] 3.5 Actions: the five commands, the refusal when the matching `can/*` is off, the failure messages; nothing about titles, artists or art in any log line
- [ ] 3.6 Provider tests with the fake platform for every scenario of the media-provider spec, plus a rapid track change (art arriving after the properties), an action during a track change, and a platform that throws once and recovers
- [ ] 3.7 `sample.json` for a playing track and for nothing playing, and the sample-provider test that the preview and `--demo` read it (no actions, per provider-actions)

## 4. The widget

- [ ] 4.1 Create `widgets/UrDeck.Widgets.NowPlaying` (GPL header, references only the SDK), add it to `urdeck.slnx`, the `UrDeckPlugin` list, the preview and the engine test project; the config (`showAlbum`, `showApp`, `idleLabel`) with unknown properties preserved
- [ ] 4.2 Composition: art with release of the previous source, text lines with shrink-to-fit, the progress bar, the `ButtonRow` with themed glyphs and the text fallback, dimmed disabled buttons, the idle state
- [ ] 4.3 Progress extrapolation from `position` and `position/at`, the one-second repaint only while playing and the bar is shown, `NeedsRender`
- [ ] 4.4 `ITapTarget`: `CanTap` per available button, `OnTap` invokes the action through `Actions`, no optimistic state
- [ ] 4.5 Widget tests with fake services: playing, paused, nothing playing, long title, no duration, no art, a disabled button, a tap per button and in the gap and on the art, the progress after ten seconds, the repaint cadence, art released on a track change, the config round-trip
- [ ] 4.6 Golden images in the built-in themes for playing, paused and idle (Windows), rewritten with `URDECK_UPDATE_GOLDENS` and looked at before they are committed

## 5. Docs and site

- [ ] 5.1 `README.md` (the widget, the provider, the privacy note), a widget page under `site/src/content/docs/widgets/`, the generated reference complete (`npm run build` in `site/`), the site's picture through `npm run media`
- [ ] 5.2 `docs/BACKLOG.md` (item 7: media done, what remains) and `docs/ROADMAP.md`; the statement of what the app reads and sends (item 15) gains the media session

## 6. Verify (automated)

- [ ] 6.1 `dotnet build urdeck.slnx -c Release` with 0 warnings, `dotnet test urdeck.slnx -c Release`, the Linux test projects, the engine coverage floor, `dotnet format urdeck.slnx --severity warn`, and `openspec validate --all --strict`
- [ ] 6.2 `UrDeck.Host.exe --snapshot` of a page with the widget playing (a real player) and with nothing playing

## 7. Verify on the panel (with the owner)

- [ ] 7.1 With a music player, a browser tab and a video app in turn: the card shows the right track, art and buttons; previous, play-or-pause and next each work by touch; the owner approves the look and the touch sizes
- [ ] 7.2 A track change shows the new title and art together; pausing holds the bar; closing the player shows the idle card; no old art appears beside a new title
- [ ] 7.3 Measure the host with the widget playing and idle against the same page without it (private bytes, CPU, GPU), the repaint rate while playing, and that memory returns after many track changes (art released); record it in `docs/perf/now-playing.md`
- [ ] 7.4 The art folder holds at most two files while the deck runs, and is gone after a quit; the log shows no titles, artists or art paths
