## Context

`provider-actions` gives a widget a way to ask a provider to act. This change is its first consumer and the backlog's
"media / now playing" widget (item 7). The relevant existing pieces:

- Providers are plugins that reference only the SDK, are created on first demand and stopped after a linger. They may
  push (`SampleAsync` empty, publish from callbacks).
- Rich data is "a group of readings" (data-providers); value types are number, text, on/off and time. Image values are
  parked.
- `IIconSource.GetIcon(source, size)` accepts a local image file and caches a decoded image per source for as long as the
  asking widget lives.
- Taps arrive as a point in card pixels on the UI thread (`ITapTarget`); the host shows press feedback for the whole card.
- First-party providers and widgets are built by the host and copied to `plugins/`; the preview tool and the goldens
  reference the same projects for their embedded sample files and must build on Linux.

Windows exposes the current media session through the System Media Transport Controls
(`Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager`): the current session, its media properties
(title, artist, album, thumbnail), playback info (status, which commands are enabled), timeline (position, end time, when
it was last updated) and the commands try-play, try-pause, try-toggle, try-skip-next and try-skip-previous. It is
event-driven. The description here is from knowledge of that API; task 1 confirms it against the documentation and a real
player before anything else is built.

## Goals / Non-Goals

**Goals:** a media widget that follows whatever plays and controls it by touch; no polling; no network; no new value type;
the provider and widget testable on Linux; a theme styles everything.

**Non-Goals:** choosing among players, seek, volume, shuffle or repeat, lyrics, other sizes, anything that persists art.

## Decisions

1. **Readings, not a typed topic.** The data-providers design says rich data is a group of readings and that typed topics
   may never be needed. Title, artist, album, status, app, position, `position/at`, duration, art and three `can/*` flags
   are ordinary readings, so the editor and any other widget (a tiny text-only "now playing" line, a theme preview) can use
   them without new SDK types.

2. **Position as a number plus a time, not a ticking reading.** The player reports its position now and then (on seek, on
   play, on pause), with the time it was read. The widget extrapolates while playing. A provider that published the
   position every second would force a one-second sampling loop for a progress bar and break "a page with nothing moving
   costs close to nothing". The widget's own one-second repaint runs only while playing and only when the bar is shown.
   Both values are published in one batch, so a widget never combines a new position with an old time.

3. **Art as a content-addressed file, read through the icon service (path B).** The provider writes the thumbnail to a
   file named by its hash and publishes the path as text. `GetIcon` already loads a local image file, caches it, decodes it
   off the UI thread and repaints the widget when ready. A new value type (`image` reading) would touch `ReadingValue`,
   the hub, sample files, the formatter and the editor for one consumer; it stays parked. Because the path is derived from
   content, a path never changes meaning, so the icon service's cache is never stale.

4. **`IIconSource.Release(source)`.** The icon service keeps an entry until every widget that asked for it is disposed. For
   a fixed shortcut that is right; for art that changes every few minutes it keeps one decoded image per track (up to a
   512-pixel bitmap each) for the whole run of the deck. A default interface member that does nothing, implemented by the
   engine, fixes it without touching hosts written before it. Rejected alternatives: a size-bounded least-recently-used
   eviction in the icon service (surprising for a shortcut that is merely off-screen for a while, and it hides the cause),
   and a single fixed art path rewritten in place (the cache would serve the old picture, and a change in place cannot be
   told apart from an unchanged file without polling its timestamp).

5. **Art folder in the temporary folder, per run.** A provider has no data-folder service and should not gain one for
   this. The user's temporary folder is the conventional place for a rebuildable cache, is private to the user, and
   needs no configuration. The provider clears leftovers when it starts (a crash cannot leave art behind for long) and
   removes the folder when it stops. Rejected: the app's `cache/` folder (the provider would need the host's path through a
   new service for the sake of a few kilobytes), and bytes in a reading (decision 3).

6. **One play-or-pause button, five actions.** The user wants previous, play or pause, and next zones. SMTC has try-play,
   try-pause and try-toggle. The widget uses `control/playpause` for its single middle button (the glyph follows `status`),
   so a stale `status` cannot send the wrong command; `control/play` and `control/pause` exist for other widgets and for
   automation.

7. **`ButtonRow` as a layout component, not a widget-local calculation.** Drawing a glyph and deciding what a tap hit are
   two uses of the same rectangles; if each widget computed them separately they would drift (a tap on a visibly drawn
   button missing it). The component returns rectangles and a `HitTest`, draws nothing (the look is the theme's glyphs and
   text styles), and owns the touch-size and gap rules so no widget assumes a resolution. Gaps between buttons hit the
   nearer button, so a finger that lands a little off is not ignored.

8. **Whole-card press feedback stays.** The host scales and dims the whole card for a press (widget-input). With three
   buttons that is coarser than per-button feedback, but it needs no new host surface. Per-button feedback is a later change
   if the panel shows it matters; the widget draws no pressed state.

9. **A button that the player disables is dimmed and inert.** `can/*` flags come from the session's playback info. A tap on
   a disabled button would only produce a failed action; dimming says why in advance, and `CanTap` returning false for it
   also means no press feedback for a control that will do nothing.

10. **No optimistic state.** Media events arrive within tens of milliseconds, so waiting for the reading is invisible to
    the user and cannot show a wrong state (see `provider-actions` design, decision 8).

11. **Project structure for the Windows part: embedded C#/WinRT in a plain `net10.0` project (to be proven by task 1).**
    The preview tool and the goldens reference provider projects as `net10.0` and run on Linux, and the host copies each
    plugin's `GetTargetPath` built with the default framework. The Windows Runtime APIs are available through a Windows
    target framework (`net10.0-windows10.0.19041.0`; Microsoft's documentation for desktop apps describes only that route
    for .NET 6 and later), and a `net10.0` project cannot reference a project with a Windows TFM. The C#/WinRT
    documentation describes an **embedded** mode for exactly this case: `CsWinRTEmbedded=true`, `CsWinRTIncludes` naming
    the namespaces to project (here `Windows.Media.Control` and `Windows.Storage.Streams`), `CsWinRTWindowsMetadata` naming
    the Windows SDK release, and `LangVersion` 9 or later, on a TFM "not tied to a specific Windows SDK version". The
    projection and its support code are compiled into the plugin's own assembly, so the shadow copy (DLL, symbols,
    dependency file) is self-contained and the plugin needs no runtime library from the host.
    The plan, in the order task 1 tries it:
    a. **Embedded projection, Windows-only compile.** One `net10.0` project holds the logic, the sample and
       `IMediaSessionPlatform`; the Windows implementation file and the C#/WinRT properties apply only when the build runs
       on Windows (the projection tool is a Windows program), so on Linux the project builds without it and the preview
       tool and the Linux tests use the fake and the sample. `[SupportedOSPlatform("windows")]` marks the Windows class;
       CA1416 stays on.
    b. **Two projects**, if (a) does not load from the collectible context: the neutral provider (`net10.0`) and a
       Windows platform assembly (`net10.0-windows10.0.19041.0`) that the provider loads by name from the same folder when it
       is present. Needs the plugin loader to resolve a sibling assembly from the shadow copy, which it does not do today.
    c. **Multi-targeting** is the least attractive: MSBuild's `GetTargetPath` call in the host's plugin copy target would need
       a framework passed to it and every consumer would need to pick the right one.
    What the spike must also answer, because embedded mode removes the helpers a Windows TFM provides: whether
    `await` works on the projected async operations, and how to read the thumbnail stream into bytes (the stream-to-`Stream`
    adapters belong to the Windows TFM's support code; `DataReader` from `Windows.Storage.Streams` is the fallback). The rest
    of the change depends only on the seam, not on which candidate wins.

12. **What the session API reports (confirmed against the documentation, 2026-10-25).** The media session service
    (`Windows.Media.Control`) is available from Windows 10 version 1809, below the host's own 19041 floor. The manager gives
    the current session (the one "the system believes the user would most likely want to control") and raises
    `CurrentSessionChanged` and `SessionsChanged`. A session has media properties (title, artist, album title, album
    artist, track number, a thumbnail as a reference to a random-access stream) and raises `MediaPropertiesChanged`,
    `PlaybackInfoChanged` and `TimelinePropertiesChanged`. Playback info carries the status and per-command enabled flags
    (`IsPlayEnabled`, `IsPauseEnabled`, `IsPlayPauseToggleEnabled`, `IsNextEnabled`, `IsPreviousEnabled` and others), which
    are the source of the `can/*` readings. The timeline's `Position` is "current as of `LastUpdatedTime`", a UTC time,
    which is exactly the position-plus-instant pair of decision 2. The commands are the `Try...Async` methods (play, pause,
    toggle, skip next, skip previous). The session exposes the source application only as an application user model id, not
    a display name, so the `app` reading is that id tidied for display. The documentation lists the capability
    `globalMediaControl` for these classes; see Risks for what that means for a packaged build.

## Risks / Trade-offs

- The Windows project shape (decision 11) is the one real unknown. Embedded C#/WinRT is documented for this case but I have
  not built it with .NET 10 and this repository, and the C#/WinRT version matching the .NET 10 SDK needs checking. It is a
  spike with a fallback, not a guess. If neither (a) nor (b) works without changing how plugins are built, the change stops
  after task 1 and goes back to the owner.
- **Packaged build and the `globalMediaControl` capability.** The documentation lists that app capability for the media
  session classes. UrDeck is unpackaged today (Velopack), where capabilities do not apply. The `signing-and-store` change
  plans a Microsoft Store listing of the same installer; if that becomes a packaged (MSIX) build, the capability must be
  declared and Store certification may ask about it. Task 1 records whether an unpackaged app needs anything and flags the
  question to `signing-and-store`; it is not decided here.
- **Some players report little.** The timeline's position is only as fresh as the player's last update; a browser tab may
  report no duration or a stale position, and a paused session's position is the last one given. The widget draws what is
  reported and shows no bar when there is no duration. The playback rate is not applied (a player at 2x drifts until its
  next update).
- Players differ in what they report: some never send a duration, a position, a thumbnail or the `can/*` flags. Every
  field is optional in the readings and the widget degrades per field (no bar, placeholder art, buttons enabled when a flag
  is unknown). The list of players the owner checks on the panel is a task.
- "Current session" is Windows' choice, so a browser tab can be current while a music player plays. This matches the media
  keys and is accepted; per-widget player choice is a later change.
- The thumbnail can be several hundred kilobytes; it is written once per distinct image and at most two files exist.
- Whole-card press feedback over three buttons (decision 8).

## Open Questions

- Do we want a 2x1 compact layout in this change? Left out: one size proves the pattern; sizes follow the stats widget's
  example.
- Should `sample.json` carry a real art file so the site's picture and the goldens show a cover? A themed placeholder is
  the default; a small generated image embedded as a resource is the alternative. Decide with the golden images.
