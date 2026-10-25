## Why

The owner's Nexus page has a media widget and the default widget set (backlog item 7) lists "media / now playing" as the
open entry. It is also the right first consumer of `provider-actions`: Windows already publishes what is playing and
accepts transport commands (System Media Transport Controls), with no account, no network and no settings, so it proves
the action path before Home Assistant adds provider settings and secrets on top.

This change is applied after `provider-actions` (it uses `IActionProvider` and `IWidgetHost.Actions`).

## What Changes

- **A `media` data provider** (first-party, `providers/UrDeck.Providers.Media`) over the current Windows media session:
  readings for the title, artist, album, playback status, position and duration, the source application, which
  transport commands are available, and the album art; and the actions `control/previous`, `control/next`,
  `control/play`, `control/pause` and `control/playpause`. Pushed from the session's events; it does not poll. The Windows
  specific part sits behind an `IMediaSessionPlatform` seam, as the System provider's GPU code does behind `IGpuPlatform`,
  so the logic is tested with a fake and builds on Linux.
- **Album art travels as a file, through the existing icon service.** The provider writes the current art to a small
  per-run cache folder under a name derived from its content and publishes that path as a text reading; the widget asks
  `Icons` for it. No new value type. The icon service learns to let a widget **let go of a source** it no longer shows,
  because it keeps every source a widget asked for until the widget is disposed, and a track change would otherwise keep
  one decoded image per track for as long as the deck runs.
- **A 4x2 now-playing widget** (`urdeck.widgets.nowplaying`): art on the left, title, artist and album (when it fits)
  beside it, a progress bar, and a row of previous, play-or-pause and next. The progress is extrapolated from the
  position and the time it was read, so nothing polls once a second; the widget repaints once a second only while
  something plays. Nothing playing is a calm "nothing playing" state, not an error.
- **Separate tap zones.** `ITapTarget.CanTap` and `OnTap` receive the point in card pixels; the widget answers per
  button. A small SDK component, `ButtonRow`, lays out the rectangles and draws nothing of its own look, so what is drawn
  and what is tapped come from one calculation and cannot drift apart.
- **Transport glyphs** in the built-in icon set (`media/play`, `media/pause`, `media/previous`, `media/next`, from the
  Lucide set already shipped), drawn with `ThemedIcon`, so a theme styles them like every other glyph.
- **Sample data** for the provider (a sample track with art-less state and a "nothing playing" variant), so the site, the
  goldens and `--demo` render the widget without a player.
- **A spike first** (task 1) for the one thing not yet proven: whether embedded C#/WinRT in a plain `net10.0` project, the
  documented way to use Windows Runtime types without a Windows target framework, loads from a plugin's shadow copy in a
  collectible load context while the project still builds on Linux for the preview tool (see `design.md`, decisions 11 and
  12). The Windows media session API itself is confirmed against its documentation (Windows 10 1809 and later).

Deliberately not in this change: choosing between several players (the widget follows the session Windows calls current);
volume, seek and shuffle or repeat controls; lyrics; a 2x1 or 4x4 size; start-the-player-on-tap; per-widget player
choice; any use of the art beyond the widget (no persistent cache, no thumbnails sent anywhere).

## Capabilities

### New Capabilities

- `media-provider`: the `media` provider: its readings and actions, when it runs, how it reads a session, the art file
  rule, the platform seam and what is logged.
- `widget-now-playing`: the widget: identity, configuration, composition, states, progress, taps and repaint rules.

### Modified Capabilities

- `icons`: a widget may release a source it no longer shows.
- `icon-sets`: the four media keys.
- `components`: `ButtonRow`.

## Impact

- `sdk/UrDeck.Sdk`: `ButtonRow` in `Components`; `IIconSource.Release(string source)` as a default member (no-op), so a host
  written before it compiles. Additive; `AssemblyVersion` stays, the package version moves one minor.
- `src/UrDeck.Engine`: `IconService`/`WidgetServices` implement `Release`; the four glyphs and their manifest keys in the
  built-in `outline` set; nothing else.
- `providers/UrDeck.Providers.Media` (new, GPL, references only the SDK), added to `urdeck.slnx`, the `UrDeckPlugin` list
  in `UrDeck.Host.csproj`, `UrDeck.Preview` (for the sample) and the engine test project. The shape of its project file
  (target frameworks) is decided by task 1.
- `widgets/UrDeck.Widgets.NowPlaying` (new, GPL, references only the SDK), added the same way.
- `src/UrDeck.Preview`, `site/`: the sample provider and the widget appear in the catalog export and the generated
  reference; the site's widget page and a picture come from `npm run media`.
- Privacy and disk: the provider reads what other applications publish to Windows about their playback. It sends nothing
  anywhere. Album art is written to a per-run folder under the user's temporary folder, cleared when the provider stops and
  of leftovers when it starts; no title, artist or art is logged. A transport command is sent to whichever application
  Windows calls current, which is also what the keyboard's media keys do.
- Config: a widget object with `typeId` `urdeck.widgets.nowplaying`. No `providers.media` settings beyond the existing
  `intervalMs`, which the pushed provider ignores.
- Tests: the provider with a fake platform (no session, a session, every field, a track change, art arriving late or not
  at all, session lost, commands and their failures), the art file rule, `ButtonRow` geometry, the widget with fake
  services (states, progress extrapolation, taps per button, a disabled command), `Release`, golden images of the widget
  in the built-in themes.
- Docs: `README.md`, the site's widget page (a new page under `site/src/content/docs/widgets/`; the reference tables are
  generated), `docs/BACKLOG.md` (item 7), `docs/ROADMAP.md`, `docs/perf/now-playing.md`.
