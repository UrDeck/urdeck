## Context

The theme format already reads, checks and keeps `icons` (`ThemeFormat2.Icons.cs`: `set`, `stroke`, `tint`, `glow`,
`animated`); nothing draws it, and `ThemeLint.Files.cs` warns on any `icons.set` because no set exists (`THEME044`). The
Weather widget draws Meteocons through `LottieAnimation`: `SelectIcon`, `SyncForecastIcons` and `Poster` load the
animations and cache a poster `SKImage` per key and size, `Advance` times the loop from the card's `motion`,
`periodSeconds` and `animateForecast`, and `IsAnimating` makes the host repaint at 15 frames a second. The Shortcut widget
asks `IIconSource.GetIcon(source, size)` and hands the answer to `ImageTile.Draw`, which draws a letter placeholder when
there is no image.

Constraints that shape the design (see `proposal.md` for the motivation):

- The SDK is MIT and widgets reference only it; the engine is GPL. Parsing and the manifest are engine work; what a widget
  holds and draws with is SDK.
- A `Theme` is built per surface size and disposed on a change, and owns what it holds. Parsed icons do not depend on size.
- The product has not shipped: SDK signatures may change freely. The v1 themes' golden images still guard their pixels.
- Memory has been the scarce resource of the look-and-feel work (`docs/perf/card-chrome.md`), CPU second. Installer size is not.

## Goals / Non-Goals

**Goals:**
- One loader for the built-in set and for a theme folder's set, both through the manifest.
- Icons are vectors restyled at draw time (stroke, tint, filled marks, glow), so a theme's `icons.*` values mean something and
  the set itself costs tens of kilobytes.
- A weather card under a set theme costs less than today's: no Lottie, no poster bitmaps, no animation repaints.

**Non-Goals:**
- Per-theme art (PR 10b), the `ui/` keys, plugins registering sets by themselves, and a card-level switch back to animated
  weather (a card setting that breaks the theme's look was rejected in the explore).
- `Theme.Motion` and any reduced-motion behaviour for icons: set icons do not move.
- A general SVG renderer. Anything outside the whitelist is out.

## Decisions

### 1. The manifest is the only lookup; files keep their upstream names

A set is `manifest.json` (version 1: `name`, `source`, `license`, `upstreamVersion`, `keys`) plus SVGs. The key-to-file
table is what lets `outline` be Lucide's files untouched (`cloud-lightning.svg` answers both thunderstorm keys, `zap.svg`
answers `app/bolt`), records provenance next to the files, and lets a theme author or a future plugin ship any filenames. The
alternative, renaming files to `<group>/<key>.svg`, loses the upstream names that make a version bump a diff, duplicates the
shared thunderstorm file and has nowhere to record the source. A convention with an optional manifest was rejected: two lookup
paths, and a hand-drawn set would escape the licence and provenance fields. A set without a manifest is not loaded; the log
names the file to make, as for a loose `.sksl`.

### 2. `outline` is Lucide, copied once

Lucide (ISC; the Feather-derived icons MIT) at a pinned release, copied into `src/UrDeck.Engine/Icons/Builtin/outline/` as
embedded resources with the licence text; `upstreamVersion` names the release. The mapping (weather and `app` keys) is a table in
the proposal's task list and then in the manifest. Approximate matches are accepted on the board's terms: `sleet` is
`cloud-hail`, both thunderstorm keys are `cloud-lightning`, `not-available` is `cloud-alert`; `home` is `house`, `browser` is
`globe`, `chat` is `message-circle`, `bolt` is `zap`, `chart` is one of the `chart-*` icons. Tabler (MIT) or Phosphor (MIT) can fill a key Lucide draws badly; the manifest's `source` then names every origin as text. Open-Meteo-style terms and CC BY sets are excluded because the product shows no credit for icons. There is no runtime dependency: the files are copied once, and a short `README` in the folder says which release and how to update. The seven seed SVGs
of the design handoff are not used (unknown source).

### 3. A small whitelist parser in the engine, no SVG library

`IconSvgParser` reads the file with `XmlReader` (DTDs prohibited, no external resolver, an element-count and
path-length limit) and turns the allowed elements into `IconShape`s: an `SKPath` per element (`SKPath.ParseSvgPathData` for `path`,
the primitives built directly, `rect` with `rx`/`ry`) and a `filled` flag from `fill="currentColor"`. It ignores presentation
attributes (the theme owns colour and width) and rejects what the file rules forbid. The same parser serves the loader (which
skips a bad file and logs once) and `ThemeLint` (which reports `THEME072` with the reason), so the deck and the tool cannot
disagree. A library (Svg.Skia) would add a dependency, a startup and memory cost, and a renderer that draws colours we then
fight; the whitelist is also what makes a user theme's SVG safe to accept.

### 4. SDK surface

```
IIconSet                 bool TryGet(string key, out IconGlyph glyph)
IconGlyph                immutable list of IconShape (SKPath, bool Filled), in the 24 x 24 space
IconTheme : IDisposable  Set (IIconSet, shared, not owned) | NamesSet | Stroke (fraction) | Tint (+ Color)
                         | Glow? (blur px, colour?, opacity) | the glow cache (owned)
Theme.Icons              IconTheme, default: no set, accent tint, stroke 0.0667, no glow
ThemedIcon.Draw(canvas, theme, rect, key, SKColor? tint = null, float rampPosition = 0.5f) -> bool
ImageTile.Draw(canvas, theme, rect, image, label, placeholder, glyphKey)
```

`Theme.Dispose` disposes `IconTheme`'s cache and nothing else of it. The set is shared because `ThemeResolver` runs per
surface size, and the compact and dock themes use the same set. `Draw` scales the canvas to the centred square of the rectangle,
strokes every shape at `Stroke * 24` in glyph space with the theme's cap (round unless square), fills the marked shapes, and
resolves the colour from the override, else the tint (`ramp` along `ColorMath.Along(theme.Ramp, position)`, `none` is the text
colour because the file rules allow no other colour). The glow reuses the `GlowCache` pattern (a small bounded cache of
blurred `SKImage`s keyed by key, pixel size and colour, living on the theme, not in a static, because widgets load in collectible
contexts). `ImageTile` takes the glyph key as a normal parameter (no compatibility to keep). `NamesSet` is how weather tells
"the theme opted in" from "glyphs are available to everyone".

### 5. Resolution at theme resolve, one composite lookup

`IconSetStore` (engine) loads and parses a set once per origin (built-in resource or theme folder) and holds it for the life of the
resolved theme source; `ThemeResolver` asks it for the chain `[theme's set, outline]` and gives `IconTheme` one composite
`IIconSet` over them. A name that is not found logs one line and leaves `NamesSet` false. Hot reload of a theme folder reparses
its sets with the theme. Reading is file I/O at resolve time only; `TryGet` is a dictionary lookup.

### 6. Weather: two modes in one widget

`Render` checks `theme.Icons.NamesSet`. Set mode skips `SelectIcon`, `SyncForecastIcons` and `Poster`, releases anything a
previous Meteocons-mode paint loaded (the theme can change between paints), makes `HasAnimation` false (so `IsAnimating` and
`NextAnimationAt` are idle) and draws `ThemedIcon.Draw` into the rectangles the animations use. Meteocons mode is the existing
code, untouched, so the three v1 themes and every theme with no set render byte-identically. The icon key is
`"weather/" + WeatherIcons.Key(...)`. For the `ramp` tint the widget needs the temperature as a number: `Shown` and the day
records keep the reading's value in Celsius next to the `ReadingText` (the widget already converts for display, `DisplayUnit`),
and the position is `clamp((c + 10) / 50, 0, 1)`. `icons.animated` is not consulted anywhere; it is deleted (decision 8).

### 7. Shortcut: glyphs by `theme:` and by fallback

The widget parses `icon` for the `theme:` prefix before it calls the icon service (the text would otherwise classify as a URI
scheme); a match passes the key to the tile and requests nothing. Otherwise it asks the service as today, and when the answer
is none and not loading it passes a glyph key chosen from the target's kind, using the same classification the launcher uses
(`LaunchTarget`): web address `app/link`, folder `app/folder`, anything else `app/file`. While loading it passes only the
placeholder text, so the card never shows a glyph and then the real icon. The fallback applies under every theme because
`outline` is always in the chain.

### 8. Removing `icons.animated`

Set icons are pictures, so the key controls nothing. It leaves `ThemeIconsDefinition`, `ThemeDefinition`'s sanitising, the
schema (`docs/schemas/v2/theme.json`), `docs/themes.md`, the fixtures in `tests/.../Fixtures/themes/*/theme.json`, the design
copies under `docs/design/look-and-feel/assets/themes` (left as the handoff snapshot, noted in `STATUS.md`) and `04` is
superseded by `STATUS.md`. A theme that still sets it gets the ordinary unknown-key finding with its suggestion. Prism's draft sets
`animated: true` with `set: "outline"`, a contradiction under the old rule too; its theme change decides.

### 9. Lint

`ThemeLint.Files.cs` replaces the "no icon sets exist" warning with: `THEME044` for a set that is in neither the theme's folder nor
the built-in list, and the new `THEME070` to `THEME076` of the theme-lint delta, run over the sets in the theme's `icons/` folder
and the built-in set (so CI proves `outline` clean through "Built-in Themes Check Clean"). `THEME075` (a file no entry names) is
a directory scan and is the tool's alone; the deck reads the manifest and the files it names only.

## Risks / Trade-offs

- **Lucide glyphs are not the board's glyphs** (sleet, thunderstorms-night, `not-available`) → look at the panel and swap a key
  to Tabler or Phosphor where one is bad; the manifest makes that a one-line change.
- **Optical size.** A 24-grid outline sits in about 20 units and is thin next to a Meteocons image that fills its box → the
  icon rectangles are unchanged first; compare against the board on the panel and, if needed, scale the weather glyph in the
  widget (not in the SDK component).
- **v1 goldens.** The shortcut fallback applies to every theme. If a v1 golden's shortcut card hits it, the golden changes → check
  before anything is redrawn; if one does, stop and decide rather than redraw (the fixtures' targets may resolve icons on Windows only).
- **Untrusted SVG** in a user theme → the whitelist parser, DTDs off, size caps (4 KB a file, 64 KB and 256 keys a manifest,
  element and path-data limits), paths confined to the set's folder.
- **Celsius conversion** for the ramp tint depends on the reading's unit → the widget converts from the unit it already reads for
  display; one test per unit.
- **Glow memory** (Rigline only): at most about six cached images at 4x4, each a glyph size plus blur margin → measured in the perf
  note; the cache is bounded.
- **Losing animated weather under Carbon and Paper** is intended (decided in the explore); a user who wants it picks a theme with
  no set.

## Migration Plan

No data migration. Order: SDK types and `ThemedIcon`; the parser, manifest reader, `IconSetStore` and `outline`; `ThemeResolver`
and lint; weather; shortcut; remove `icons.animated`; Carbon and Paper get their set back and their goldens are redrawn; docs and
the perf note. Rollback is reverting the pull request; no stored state depends on it.

## Open Questions

- Which icon (outline or Meteocons) Prism uses is decided in Prism's theme change.
- Whether the `ui/` keys ship waits for a host-chrome feature that draws them.
