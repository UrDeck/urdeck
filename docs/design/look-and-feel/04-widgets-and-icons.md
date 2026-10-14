# 04 Widgets, components and icons

All SDK additions are **MIT, additive and source-compatible** (new optional members, new classes). Widgets outside this
repo compile against `UrDeck.Sdk`; do not change an existing signature. Bump the SDK minor version.

## 1. SDK surface changes

### 1.1 `Theme` (what a widget sees)
New read-only members (all have safe defaults so an old host/new widget and new host/old widget both work):

```csharp
public GaugeTheme Gauge { get; init; }       // resolved gauge modifiers (sweep, segments, ticks, redline, marker, ...)
public TypeTheme Type { get; init; }         // case, tracking, slant, valueFill, valueGlow, valueScale per role
public SKColor Accent2 { get; init; }        // = Accent when the theme has none
public SKColor Ink { get; init; }
public IReadOnlyList<SKColor> Ramp { get; init; }
public ChipTheme Chip { get; init; }
public DividerTheme Divider { get; init; }
public IconTheme Icons { get; init; }        // set handle + tint + stroke + Animated flag
public MotionLevel Motion { get; init; }     // Full | Reduced | Off  (effective level, after user/OS)
```
**Compact:** the host builds a second resolved `Theme` for compact cards (applying `card.compact`), so widgets are unaware.
Gauge/typography overrides in `card.compact` are **not** supported in this iteration (only chrome keys).

`TextRole` already exists (Value, Unit, Label, Body, Title). `TextStep` unchanged.

### 1.2 `GaugeStyle` enum
Append `Meter, Tach, Marker, ThickBar` (do not renumber; `Plain` keeps its value). `ThemeGaugeDefinition.Parse` accepts
`meter`, `tach`, `marker`, `thickbar`/`thickBar`.

### 1.3 `GaugeOptions` additions
`Sparkline` (`float[]? History`, drawn when `theme.Gauge.Sparkline.Enabled`), `StatusText` (`string?` override), `Thresholds`
(warning/critical as **fractions**, so the component can place the redline and status chip). All optional.

### 1.4 New components
| Component | Purpose | Notes |
|---|---|---|
| `Chip.Draw(canvas, theme, rect, text, GaugeLevel level)` | status label | honours `theme.Chip` and `typography.case` |
| `Divider.Draw(canvas, theme, rect)` | the rule between sections; `dimension` style takes an optional label | |
| `ThemedIcon.Draw(canvas, theme, rect, key, tintOverride?)` | draws a theme-set icon; returns `bool` drawn so the caller can fall back (Lottie, app icon) | **no I/O on paint**: the host resolves the `IconSet` at theme load |
| `Sparkline.Draw(canvas, theme, rect, float[] values, SKColor color)` | polyline + optional fill | values normalised by the caller's Min/Max |
| `TextLine`/`Readout` | honour `case`, `tracking`, `slant`, `valueFill`, `valueGlow`, `valueScale` | **default-off paths must be byte-identical** to v1 |

### 1.5 `Gauge.Draw` internals
Split `Gauge.cs` into one file per style (`RingGauge`, `BarGauge`, `TachGauge`, ...) behind the existing static entry, because it will
otherwise grow past reason. Keep `Gauge.Draw`'s signature and return value (the readout rect). Constants `RingStart/RingSweep`
become the defaults of `theme.Gauge.Sweep`.

**Caching.** Anything that builds a `SKPath`, `SKShader` or blurred `SKMaskFilter` must cache per `(rect size, theme id)` in a small
LRU inside the component (the component "keeps no state" today as a design rule: loosen it to "keeps only caches that are
keyed on inputs and invalidated with the theme"). Dispose with the theme's lifetime via a registration on `Theme`.

## 2. Widget changes

### 2.1 Clock (`widgets/UrDeck.Widgets.Clock`)
Today: `Format, ShowDate, TextColor, FontSize, Style (simple|flap)`.
New optional config (all default to today's behaviour):

| Key | Type | Meaning | Used by (mocks) |
|---|---|---|---|
| `ShowSeconds` | bool | small seconds next to the digits (":32") | Carbon |
| `ShowWeek` | bool | ISO week label ("Wk 42") in the header | Paper |
| `Subtitle` | string? | free text in the header, right side ("Rig online") | Rigline, Ember ("Session 04:12" is a *static string* for now; a real session timer is out of scope) |
| `BlinkSeparator` | bool | the colon blinks at 1 Hz; obeys `theme.Motion` (steady at Reduced/Off) | Carbon |
| `Header` | `"date"\|"none"\|"label"` | what the header row shows; `label` uses `Subtitle` on the left | Carbon ("Local time") |

Layout contract: a **header row** (label left, date/secondary right, `label` role, `case` applied) and the **digits** (value role).
`valueFill` and `valueGlow` apply to the digits. In `flap` style the flap cards use the theme card colours; `valueFill` is not applied to flaps (document).
`IsAnimating` is true when the separator blinks, a `valueFill.animate` is active, or the flap animates; and false at `Motion != Full`.

### 2.2 Stats (`widgets/UrDeck.Widgets.Stats`)
- **History buffer:** per slot a ring buffer of the last `historySeconds` (default 60) samples at the reading's rate; append on every
  value update (the widget already receives updates through the reading hub); memory ≈ 4 bytes × seconds × slots. Only
  allocated when `theme.Gauge.Sparkline.Enabled` and the card is large enough (≥ 2x2 or slot rect height ≥ 0.9 cell).
- **Status chip:** when `theme.Gauge.Status.Enabled`, pass `StatusText`/level; `Chip.Draw` beside the label.
- **Gauge style:** unchanged resolution order: slot `Style` > theme `gauge.style`. New names accepted in slot `Style`.
- **Colour by ramp / valueTint:** via the component; the widget only passes `Level` and `Fraction` as today.
- **Eased fill** stays in the widget.
- Slot `Min/Max/Warning/Critical` are converted to fractions for the redline/status (new).

### 2.3 Weather (`widgets/UrDeck.Widgets.Weather`)
- Icon resolution (current condition and each forecast day): **(1)** `theme.Icons.Set` has `weather/<key>.svg` → draw `ThemedIcon`
  (static, tinted, `theme.Icons.Stroke`); **(2)** else Meteocons Lottie (today). `icons.animated: false` only forces the static
  Meteocons poster frame (`PosterFraction`) instead of playing it; it never hides an icon. A set that lacks a key therefore degrades to Meteocons rather than to nothing.
- Keys are the existing `WeatherIcons.Key` outputs: `clear-day, clear-night, partly-cloudy-day, partly-cloudy-night, overcast, fog,
  drizzle, rain, sleet, snow, thunderstorms-day, thunderstorms-night, not-available` (13). **Do not invent new weather keys**:
  the mapping from provider codes is already done.
- `icons.glow` draws a blurred copy of the icon behind it (Rigline); its colour is the tint unless the glow sets one.
- Tint (`icons.tint`): `accent` | `accent2` | `text` | `ramp` (for weather: cool→warm along `colors.ramp` by the day's temperature, mapped over -10..40 °C and clamped) | `none` | colour. Carbon/Rigline/Paper/Pop use accent/ramp/accent/text.
- The 5-day strip draws **in a `divider`-separated section**; Blueprint's `dimension` divider draws a label (`"5-DAY"`; plain text from the widget `Label`).
- Forecast days sit on the theme's **`well`** when it has one (Rigline: 4 % white rounded rectangles behind each day); no well, no background.

### 2.4 Shortcut (`widgets/UrDeck.Widgets.Shortcut`)
- Real app icons stay (they are the product). **New:** `Icon: "theme:<key>"` draws a theme glyph (`app/terminal`, `app/folder`, `app/music`,
  `app/gamepad`, …) instead of a file icon; used by the *site decks and examples* and by users who want glyph tiles.
- Real app icons are **never tinted**. Glyphs obey `icons.tint`.
- `ShowLabel` (bool, default: `true` if `Label` set): Carbon/Paper show the label; Rigline/Prism/Pop do not. The theme does not
  decide this; each deck's config does. Provide the key; the drafts' site decks set it.
- The **accent strip** (Rigline) and **circle** shape (Obsidian) are card chrome (`card.compact`), drawn by the host.
- Press feedback unchanged (compositor).

## 3. Icon system

### 3.1 Resolution order for any themed icon request `(group, key)`
1. `<theme folder>/icons/<set>/<group>/<key>.svg` (user or built-in theme with embedded icons)
2. built-in set `outline` (ships with the engine; the seeds in `assets/icons/outline/`)
3. the widget's own fallback (Lottie for weather; the app's real icon for shortcuts; nothing otherwise)

A theme with `icons.set` absent uses (2) only for `theme:` glyphs, and (3) for weather: i.e. **v1 behaviour exactly**.

### 3.2 File format rules (validator-enforced, see `08`)
- ≤ 4 KB, UTF-8, `viewBox="0 0 24 24"` required, `fill="none"` assumed, strokes only (the theme stroke applies), no `<style>`, `<script>`,
  `<image>`, `<use>`, `<text>`, no `transform`, no external references. Elements allowed: `path, circle, ellipse, line, polyline, polygon, rect`.
- A **filled** variant: `fill="currentColor"` on an element marks it as filled (Paper accent dot, etc.). Loader honours `fill="currentColor"` per element.
- Stroke width = `icons.stroke` (fraction of icon size; default 0.0667 = 1.6/24, as the mocks) × icon size. Round caps/joins unless
  `stroke.cap = square` (then butt/miter).
- Licence: set authors state the licence in `icons/<set>/LICENSE.txt`; built-ins must be CC0/MIT/OFL-compatible or original.

### 3.3 Canonical keys (the contract between widgets and sets)

`weather/` (13, fixed): `clear-day clear-night partly-cloudy-day partly-cloudy-night overcast fog drizzle rain sleet snow thunderstorms-day thunderstorms-night not-available`

`app/` (24, for `theme:` glyphs; extendable but **append-only**): `terminal folder file music gamepad browser mail chat settings code camera image video
calendar clock home power lock link cloud download star heart bolt chart`

`ui/` (6, used by host chrome later; reserved): `chevron-left chevron-right close menu check warning`

Seed SVGs in `assets/icons/outline/` cover: `weather/clear-day`, `weather/overcast`, `weather/partly-cloudy-day`, `app/terminal`, `app/gamepad`,
`app/music`, `app/folder` (the exact glyphs from the mocks). **Everything else is art to be drawn (task in PR 10).** Until a theme set
or the `outline` set has a key, the fallback chain gives the widget's own look, so nothing breaks.

### 3.4 Per-theme sets (draft)
| Set | Look | Stroke | Tint |
|---|---|---|---|
| carbon | outline, square caps, 24 grid, slightly condensed | .066 | accent |
| rigline | outline, round, gradient tint along `ramp` per icon index, glow | .066 | ramp |
| prism | thin outline (1.2/24), round | .05 | text |
| paper | outline, vermilion tint | .066 | accent |
| ember | heavier outline, square caps | .075 | accent |
| blueprint | thin technical line, with a small dimension tick | .058 | text |
| pop | heavy outline (2.2/24) | .085 | text (black) |
| obsidian | hairline, rounded | .05 | accent |

**First release:** every draft theme sets `"icons.set": "outline"` (the built-in set) and varies only `stroke` and `tint`; that already
gives about 70 % of the mock's icon character. Per-theme art (a folder per set in the theme) is optional polish, PR 10b. A set
name the loader cannot find is treated as absent with one info log, so a theme can name its future set early without harm.
