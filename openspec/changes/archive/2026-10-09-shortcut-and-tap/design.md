## Context

See `proposal.md` for the motivation. This document records what the exploration of 2026-10-09 settled, what the code
looks like today, and the choices an implementer would otherwise have to make again.

What exists (verified in the code on 2026-10-09):

- `GestureRecognizer` (engine, no timers, fed with `PointerSample`s by `PagerController`) raises `SwipeStarted`,
  `SwipeMoved`, `SwipeEnded`, `Cancelled` and `Tap`. A tap is a press and release within the slop (4% of a column)
  and 500 ms. Going down raises nothing. A vertical drag ends in the `Ignored` state and a hold longer than 500 ms
  raises nothing on release: these are the free seats for scroll and long press.
- `PagerController.Tap` hit-tests the page indicator and nothing else. `PageHost` holds the `WidgetView`s with their
  positions but offers no lookup by point.
- Each widget is one `SKXamlCanvas` (`WidgetView`), so each widget is one composition visual. The page slide already
  animates visuals on the compositor without repainting.
- The card is drawn by the host (`WidgetPainter`), not by the widget.
- `WidgetRegistry.CreateWidget` attaches one shared `IWidgetHost` object (`WidgetPluginLoader.WidgetHost`) to every
  widget. Nothing ties a host-service call to the widget that made it.
- `IWidget` has grown by default interface members four times (`Attach`, `Subscriptions`, `IsAnimating`,
  `NextAnimationAt`), and `weather` added `IWidgetHost.Log` and a component, all without changing the SDK's
  `AssemblyVersion` (0.3.0.0, pinned by `SdkContractTests`). The package version is 0.6.0.
- The engine targets `$(UrDeckTfm)` (not the Windows TFM) and runs alone for `--snapshot`.
- The window is `WS_EX_NOACTIVATE` and answers `WM_MOUSEACTIVATE` with `MA_NOACTIVATE` (`WindowFocus`).

Two throwaway spikes were run on the owner's machine; their results go into `docs/perf/shortcut-and-tap.md` (task 1):

- **Shell icons.** `IShellItemImageFactory.GetImage` at 256 pixels with `SIIGBF_ICONONLY` returned a 32-bit image with
  alpha for an executable, a Start Menu `.lnk`, a packaged app (`shell:AppsFolder\...`), a folder, an `https` address
  (the default browser's icon, no network) and `ms-settings:`; `steam://` gave a generic page. 10 to 95 ms cold, 1 to
  13 ms warm. The bitmap is bottom-up, and its alpha was premultiplied for most icons but straight for one.
- **Launching without focus.** A topmost test window with the same no-activate recipe launched the owner's four
  targets by touch through `Process.Start` with `UseShellExecute`. Steam (not running, then running), Windows Terminal
  (twice; it opens a new window each time) and a web address came to the front. Seven of eight launches did; the one
  that did not was a malformed address produced by a bug in the spike script, with Settings in front, and it did not
  reproduce. Nothing opened on the panel: windows appeared on the monitor of the application in front or where the
  application was last.
- **Site icons** were probed by hand on two real sites: one offers a manifest with PNGs up to 1024 pixels; the other
  sits behind a sign-in page that answers every path, including `/apple-touch-icon.png`, with status 200 and HTML.

## Goals / Non-Goals

**Goals:**

- A tap path that costs nothing for widgets that do not use it and adds no idle work.
- An input API that scroll, long press and a web view can be added beside without changing it.
- One launch policy and one icon cache for every present and future widget, and for the dock.
- A shortcut configuration that is flat and typed, so a later editor can build a form from it.
- Everything that decides something (gesture, hit test, target kind, icon choice) testable without a window, a shell
  or a network.

**Non-Goals:**

- The dock. It is the next change and is designed as a fixed row of 1x1 widget slots, so it needs none of its own
  drawing, press or launch code. Nothing here may assume that a tappable thing is on a page: keep page-specific code
  in `PagerController`/`PageHost`, and the rest reusable.
- Settings metadata attributes (item 15). The config type is plain properties.
- Any command path to data providers (the Home Assistant button).

## Decisions

### 1. Tap is an optional interface; the interface is the capability declaration

`UrDeck.Sdk.Input.ITapTarget`:

```csharp
public interface ITapTarget
{
    bool CanTap(SKPoint point) => true;
    void OnTap(SKPoint point);
}
```

The host checks `widget as ITapTarget` once when the view is created. Widgets that do not implement it are never
called for input. `CanTap` exists so that a widget with nothing to do at a point (a shortcut with no target, later a
widget with several regions) shows no press feedback. Points are `SKPoint` in card pixels; the SDK already exposes
SkiaSharp types.

Alternatives considered:

- *Default members on `IWidget`* (the pattern of `IsAnimating`): the host could not tell an interested widget from an
  uninterested one without a second member, and every later input kind would widen `IWidget` again.
- *An attribute* (`[Tappable]`): a static fact, cannot express "not at this point" or "not while unconfigured".
- *Regions registered during `Render`* (immediate mode: the widget names tappable rectangles and the host owns hit
  testing and pressed state per region): attractive for a widget with many targets, but it allocates on every paint,
  and with the dock being a row of widgets there is no first-party widget with more than one target. It can be added
  later as a helper on top of `CanTap`/`OnTap`.
- *Press and release delivered to the widget*: not needed once the host owns press feedback (decision 3).

Later input kinds are further interfaces in the same namespace (`IScrollTarget`, ...). For those the host must know
before the gesture is classified whether the widget under the pointer wants the pan; implementing the interface says
so. Tap needs no such negotiation because the recognizer already keeps tap and swipe apart.

### 2. The recognizer learns "pressed" and "press cancelled"; the engine does the hit test

`GestureKind` gains `Pressed` (raised at pointer down, with the position) and `PressCancelled` (raised when `Pending`
ends without a tap: just before `SwipeStarted`, on entering `Ignored`, on a release after `TapMaxMs`, on `Cancel`).
`Tap` ends the press too. `Abort()` stays silent; the controller clears its own pressed view in `Release`. The
recognizer still has no timer: a long hold stays visually pressed until the finger lifts, which is the honest picture
(nothing has been decided yet).

A new pure function in the engine (next to `GridLayoutManager`) maps a point and a page's `WidgetLayoutItem`s to the
index of the widget whose card rectangle (not cell) contains it and the point relative to that card. `PageHost` uses
it to find the `WidgetView`; `PagerController` converts DIPs to card pixels with `PagerSetup.Scale`.

Order in `PagerController` on `Tap`: indicator (existing), then the current page's widget. `Pressed` asks the same
lookup, then `CanTap`, then starts the press on that view. Input is already dropped while a slide animates
(`_animating`), which gives "no tap during a slide" for free.

### 3. Press feedback is a compositor animation on the widget's visual, with theme values

On press the `WidgetView` animates its own visual's `Scale` (around its centre) and `Opacity` to the theme's
`press.scale` and `press.opacity` over about 80 ms; on release or cancel back to 1 over about 160 ms. No Skia paint,
no frame-clock membership. The durations are host constants, not theme values, until a motion level exists.

`ThemeDefinition` gains `Press { Scale, Opacity }` (sanitised to 0.5..1 and 0.1..1, merged over the default like the
indicator block), and the engine resolves it into a small `PressStyle` beside `IndicatorStyle`. The SDK's `Theme` is
not touched: widgets never see these values. Built-in themes start at scale 0.96 and opacity 0.85; the panel check
tunes them.

Alternative considered: a pressed flag in the render context and a pressed state in the image tile (the handoff's
first idea). It repaints on every press and release, needs the frame clock for any easing, and makes every tappable
widget implement the look. The card is the host's, so the pressed card is the host's too.

### 4. Each widget gets its own host-services object

The shared `WidgetHost` becomes a per-widget object created by the registry (working name `WidgetServices`,
implements `IWidgetHost`, engine-internal). It forwards `Readings` and `Log` as today and adds:

- `Launcher` and `Icons` (decisions 5 and 6);
- an engine-side `RepaintRequested` event the `WidgetView` subscribes to and marshals to its dispatcher, invalidating
  directly (no `NeedsRender` round trip: the engine only raises it when something the widget asked for changed);
- `Dispose`, called from `WidgetView.Dispose`, which releases the widget's icon references and detaches the event, so
  a removed view cannot keep a plugin context alive (see the `AssemblyLoadContext` note in `AGENTS.md`).

`CreateWidget` therefore returns the widget together with its services object. Unattached widgets (tests) keep
working through the SDK's null defaults.

### 5. Launch service: a pure target parser in the SDK, the launcher in the engine

- `UrDeck.Sdk.Launch.LaunchTarget.Parse(string?)` (MIT, pure, no I/O) returns the kind (`Invalid`, `File`, `Web`,
  `Shell`, `Uri`), the normalised text (environment variables expanded) and a display name (host without `www.`, file
  name, else the text). The widget uses it for `CanTap` and the placeholder letter; the engine uses the same function,
  so both always agree. A scheme needs two or more characters so that `C:\...` is a path.
- `IWidgetHost.Launcher` (`ILauncher`, default: a null launcher that returns `false`) has
  `bool Launch(string target, string? arguments = null)`.
- The engine's launcher calls `Process.Start` with `UseShellExecute = true`, the default verb, `WorkingDirectory` set
  to the folder of an executable file target, synchronously on the calling (UI) thread. Exceptions become a log line
  and `false`. It remembers the last started target, arguments and time and swallows a repeat within one second.
- The launcher takes an optional "before launch" callback from the host, empty for now. It is the one seam for a
  foreground step if the panel check (task 9) shows a launch staying behind. The order to try then: make the deck
  window the foreground window for the duration of the call, then a synthetic Alt key. `AllowSetForegroundWindow`
  alone only helps a process that already may set the foreground.
- Platform code is marked `[SupportedOSPlatform("windows")]`, as `UrDeck.Providers.System` does for its native calls.

Alternatives considered: launching inside the widget plugin (`Process.Start` in `UrDeck.Widgets.Shortcut`) is less
API, but puts the policy, the double-tap guard and any foreground fix in one plugin, and a later media or dock widget
would copy it. Launching asynchronously keeps the UI thread free but loses the "received the last input" standing
that lets the launched window come forward.

"Focus the running instance" is left out: it means mapping processes to top-level windows, which is unreliable for
packaged and multi-process applications. Desktop-shortcut semantics are what Nexus does today.

### 6. Icon service in the engine, behind `IWidgetHost.Icons`

`IIconSource.GetIcon(string source, int pixelSize)` returns an `IconResult` (`SKImage? Image`, `bool IsLoading`). (It
was drafted as `Get`; the analyzer rejects that name on an interface member, CA1716.) The default on `IWidgetHost` is a
null source. The engine's service:

- **Key and sharing.** Entries are keyed by the normalised source and a size bucket (64, 128, 256, 512: the smallest
  bucket at or above the wanted size). An entry is reference-counted by the `WidgetServices` objects that asked for
  it and its `SKImage` is disposed when the count reaches zero.
- **Loading** happens off the UI thread. Shell icons need COM on an STA thread: one worker thread, created on the
  first request and ended after it has been idle for a while. `Get` records the request and returns loading; when the
  entry settles the service raises `RepaintRequested` on every `WidgetServices` holding it.
- **Shell icons:** `SHCreateItemFromParsingName` and `IShellItemImageFactory.GetImage(256, SIIGBF_ICONONLY)`, copied
  into an `SKBitmap` with the row order taken from the DIB header (the spike saw bottom-up). Alpha: treat the pixels
  as premultiplied unless any pixel has a colour channel above its alpha, then as straight (the spike saw both). An
  image with no alpha at all is opaque.
- **Image files** are decoded with `SKCodec` (ICO included) by extension list, not by sniffing arbitrary files.
- **Site icons:** one lazily created `HttpClient` (`UseCookies = false`, no default credentials, at most five
  redirects, ten second timeout, response size caps, a `User-Agent` naming UrDeck and its version). Fetch the address;
  if the body decodes as an image, done. Otherwise read `<link>` tags with a small tolerant scanner (attribute order
  varies; no HTML parser dependency) and the manifest with `System.Text.Json`, resolve hrefs against the final
  response address, order candidates as the spec says, and download at most four. Acceptance is "decodes with
  `SKCodec` and the shorter side is at least 48 pixels". The discovery logic sits behind a small HTTP interface (as
  `IWeatherHttp` does for weather) so tests run on recorded pages.
- **Disk cache:** `cache/icons/<sha256 of the normalised address>.png` next to the executable, re-encoded as PNG at
  most 512 pixels, age from the file's write time (30 days). If the folder cannot be written the service logs once
  and works from memory. The folder moves with item 15's per-user data folder.
- **Negative results** live in memory for the run.
- **Snapshot:** the service exposes "any pending" like `ReadingHub.AnyPending`, and `SnapshotCommand` waits on both.

Alternatives considered:

- *Icons as readings* (an `icons` provider with image values; the backlog anticipates image values for now-playing
  art). It would reuse demand and repaint, but readings are sampled on an interval and identified by ids whose
  grammar does not fit paths and addresses, and image values are new public API with ownership questions. Revisit
  when now-playing needs album art; the per-widget services object makes either path possible.
- *Favicons from a third-party service* (a favicon API): leaks every address the user adds to a third party. Rejected.
- *No network at all* (browser icon unless the user supplies a file): the owner asked for site icons. The local `icon`
  setting remains the way to avoid any request, and the only way for a site behind a login.
- *SVG icons*: no SVG renderer is in the repo; raster candidates are common enough. Parked.

### 7. Image tile: stateless, no pressed state, capped enlargement

`ImageTile.Draw(canvas, theme, rect, image, label, placeholder)` in `UrDeck.Sdk.Components` (the argument order of
the other components). It follows the gauge's
rules: no state, names no colour or font, the widget passes values. The 2x enlargement cap keeps a 48 pixel favicon
from being smeared across a 250 pixel card while still letting it read as an icon. The placeholder (dimmed-accent
rounded square with one letter in the value role) doubles as the loading state, so a shortcut never shows an empty
card. The handoff's "pressed state" for the tile is dropped (decision 3).

### 8. Shortcut widget

`widgets/UrDeck.Widgets.Shortcut`: `ShortcutConfig : WidgetConfig` with `Target`, `Arguments`, `Icon`, `Label`
(nullable strings, `[JsonPropertyName]`, unknown properties preserved by the base type), `ShortcutWidget :
Widget<ShortcutConfig>, ITapTarget` with `[Widget(Id = "urdeck.widgets.shortcut")]`, `[WidgetSize(1, 1)]`,
`[RefreshOnData]`, `[Category("Launch")]`. `OnConfigured` parses the target once and logs an invalid one once.
`Render` asks `Icons.GetIcon(icon ?? target, shorter side of the content rectangle)` and draws the tile. `NeedsRender`
returns `false`: the first paint is the host's, a new configuration rebuilds the widget, and icon arrival is a
host-driven repaint. `CanTap` is "the target is valid"; `OnTap` calls `Launcher.Launch`.

The label is in the config from the start, hidden unless a text is set, so a wider composition or the dock can use it
later without a format change. No `confirm` setting: a wrong launch is an annoyance, not damage, and the recognizer
already rejects moved and long presses.

### 9. Versioning

Everything in the SDK is additive: `AssemblyVersion` stays 0.3.0.0 and `SdkContractTests` stays as it is; the package
version moves to 0.7.0 with a line in the csproj comment, as `weather` did for 0.6.0.

### 10. Test data carries no real hosts

Fixtures for site icon discovery are written for `example.com` hosts, modelled on the two sites probed in the
exploration (a manifest with sized PNGs; a sign-in page answering every path). The owner's host names do not appear in
the repository, in tests, docs or the perf note.

## Risks / Trade-offs

- [A launch stays behind the application in front on the real host] → The spike passed for every well-formed target,
  but it was a stand-in window. Task 9.2 repeats it with the four real targets, closed and running. The "before
  launch" seam and the order of fallbacks are in decision 5.
- [`Process.Start` blocks the UI thread while the shell resolves a slow target (a sleeping network drive)] → Accepted
  for now: the launch must happen inside the input handling. Measured in task 9; if it is ever seconds, the fix is a
  timeout on a helper thread with the foreground right passed along, not an asynchronous launch.
- [A new application window opens on the panel, under the deck] → Not seen in the spike. Windows and the application
  decide placement; UrDeck does not move windows. Recorded in the perf note if it appears.
- [The site icon picked is the wrong one (a maskable icon with padding, a low-contrast mark)] → Maskable and monochrome
  purposes are skipped; the `icon` setting overrides everything.
- [Site icon requests reach hosts other than the one the user typed (a CDN in an icon link)] → Only over `https`, at
  most four downloads per address, no cookies. Stated in the README's network note.
- [The cache folder next to the executable is not writable once an installer exists] → Falls back to memory with one
  log line; item 15 moves the folder.
- [Shell icon alpha is guessed wrong for some icon] → The heuristic matches both cases seen in the spike; task 5.3
  renders the spike's set to PNGs for a look, and the image tile draws whatever it is given.
- [Compositor scale on an `SKXamlCanvas` looks soft or clips at the card's edge] → It is a few percent for a fraction
  of a second; checked on the panel in 9.1. If it looks wrong, opacity alone is still theme-driven feedback.
- [A press that never ends (pointer capture lost without an event)] → `PointerCaptureLost` and `PointerCanceled`
  already feed `Cancel`; `Release()` and a new press both clear a stuck pressed view.
- [Launch targets in a configuration somebody else wrote] → Same trust as a `.lnk`. No import path exists today; the
  backlog entry for shared pages (item 15) gets a note that imported launch targets need the user's consent.

## Open Questions

- The default press scale and opacity, and whether 48 pixels is the right floor for a site icon, are decided by eye on
  the panel in task 9 and recorded in `docs/perf/shortcut-and-tap.md`. Neither changes the specs' structure.
