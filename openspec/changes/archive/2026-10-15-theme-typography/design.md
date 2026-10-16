## Context

`ThemeResolver.LoadTypefaces` makes one `SKTypeface` per distinct weight from one font (`LoadedTheme.Font`), shared by
the five roles. `ThemeTypographyDefinition` already holds `Fonts` (value, unit, label), `ValueScale`, `Case`,
`Tracking`, `Slant`, `ValueFill` and `ValueGlow`, merged and sanitised by the format 2 change and drawn by nothing. The
SDK `Theme` carries sizes, colours and `Typefaces`. `Readout` and `TextLine` build `SKFont`s from
`Theme.GetTypeface(role)` and draw with `canvas.DrawText`; `Readout` fits at a reference size of 100 and scales
linearly, giving digits equal widths by drawing each digit centred in the widest digit's advance. The clock's flap
display, the gauge's centre text and `ImageTile`'s letter use the typefaces directly.

The font spike's findings (`docs/perf/theme-look.md`, S3) that bear on this: `opsz` is never driven, so Bodoni Moda
sits at its text cut (opsz 11) for large numerals and Bricolage at its display cut (96) for small labels; out-of-range
and static weights are silent; typefaces are cheap per clone (about 1 MB private per extra family, nothing measurable
per weight clone); the host resolves a theme twice (page and dock).

## Goals / Non-Goals

**Goals:**
- Everything the drafts' typography groups say is drawn by the components, in the SDK, so widgets need no code.
- A theme that sets none of it is byte-identical to today (the goldens of `theme-golden-harness` prove it).
- A font that cannot do what the theme asks says so once.

**Non-Goals:**
- The animated fill (`valueFill.animate`), `fonts.body`, `fonts.title`, `case.unit`, `tracking.body`
  (`docs/design/theme-format-2-deferred-keys.md`).
- Bundling fonts. Each theme change adds the families it uses with their licence texts.
- Per-character fallback for missing glyphs (`↑ ↓` in three families): nothing draws arrows today.
- The flap clock's digits, the gauge's own centre text and `ImageTile`'s letter. They use the role typefaces, so a
  per-role font and optical size reach them; case, spacing, slant, fill and glow do not.

## Decisions

### 1. Per-role typefaces in the resolver, keyed by (font, weight, optical size)

`ThemeFont` stays one spec for the theme, and `LoadedTheme` gains the per-role specs from `typography.fonts`
(role, spec, same `Open` function and path-escape rule). `LoadTypefaces` resolves each role's font through the chain role
→ `typography.font` → default theme → system, reads each font file once per resolve (as today, keyed by origin and
name), and clones a typeface per distinct (font, weight, optical size). Roles that agree share one typeface, so a theme
without per-role fonts or `opsz` creates exactly the typefaces it does today.

*Alternative: a typeface per role regardless.* Rejected: it duplicates clones for no gain and changes disposal.

### 2. Optical size is a theme choice, off by default

`typography.opticalSize` (`off` | `auto`) is a new key. Driving `opsz` always would change Inter (which has an `opsz`
axis, 14 to 32) for every existing theme, and the rule of the whole look-and-feel work is that a v1 picture does not
move. So the key is absent-means-off, and the drafts that need it (Obsidian, Prism) set it.

With `auto`: numerals and units take the top of the axis (the value is drawn large; it is fitted at draw time, so the
resolver cannot know the size and the display cut is right for every size a value takes on this panel); label, body
and title take their own pixel size (the resolver knows it), limited to the axis range. Both are applied when the
clone is made, with `wght`, in one `Clone` call.

*Alternative: pick the optical size at draw time from the fitted size.* Rejected: a clone per size per value change,
and the size is stable in a readout by design, so the benefit is a nuance on one size.
*Alternative: a number per role in the theme.* Rejected for now: `auto` covers both draft fonts; a number can come
later without changing `auto`.

### 3. Warnings are per loaded theme, not per resolve

Resolution happens at least twice per theme (page and dock) and again at every display change. The resolver collects
its findings (missing font, weight clamped, weight ignored) and the loaded theme records which were already logged, so
each is written once per theme load. The weight check reads the variable font's `wght` axis range and a static font's
own weight (`SKTypeface.FontStyle.Weight`, rounded to the nearest 100), and compares at the requested weight;
installed fonts (family names) are not checked.

### 4. The text style is plain data on `Theme`

`Theme.Text` is a `TextStyle`: `LabelCase`, `TitleCase` (an enum `None | Upper | Lower`), `ValueTracking`,
`LabelTracking`, `TitleTracking` (em), `ValueSlantDegrees`, `ValueScale`, `ValueFill` (colours and angle, or null),
`ValueGlow` (blur in pixels, optional colour, alpha, or null) and a small glow cache. The default instance is "nothing
set". `ThemeResolver` fills it from the definition, converting the glow blur to pixels with the cell size as it does
for card sizes. The SDK gains only new members, so a widget compiled against the older SDK keeps running; the
`UrDeck.Sdk` package version moves up one minor (the assembly version stays, as its csproj says).

### 5. Components keep their v1 path

`Readout.Run` and `TextLine.Run` take the existing code when the style has nothing for the text they draw, so the
output is the same bytes by construction, and branch to the new drawing otherwise. Case is applied to the string at the
top of `Run`, before any measuring. Spacing: a shaped run is only built when the spacing is not 0; glyphs are placed one
by one (the `Readout` already does this for digits), with the spacing added to each advance but the last, and the
measured width includes it, so `Fit` stays linear in size and the "widest value" rule holds. Slant is
`canvas.Skew` about the baseline (`Translate`, `Skew(-tan a, 0)`, `Translate`), the layout rectangle unchanged.

### 6. valueScale cannot push a value out of its rectangle

A readout is already drawn at the largest size that fits its rectangle, times the widget's own fraction (the clock's
`fontSize`, a stats slot's scale, a gauge's `readoutScale`). If `valueScale` could exceed the fit, "Pop runs big"
would mean digits over the card's padding and into its neighbours. So `valueScale` multiplies the widget's fraction
and the product is limited to the fit: above 1 it enlarges only a readout a widget made smaller than it could be, below 1
it shrinks every readout. The drafts' values (1.05 to 1.35) will therefore mostly show on the cards that draw
below the fit, and not on a single big value. If the mocks need more, the next step is for the theme to change the
card padding, not for a value to leave its rectangle. This is a behaviour that apply must confirm against the mocks
(task 6.3) before the themes depend on it.

*Alternative: let the product exceed the fit.* Rejected for the overflow above.
*Alternative: change the default fit (`valueScale` as the base).* Rejected: it would change every v1 theme.

### 7. Fill and glow live where the text is drawn, and the cache lives on the Theme

`valueFill` is an `SKShader.CreateLinearGradient` across the value's bounding box, rotated by the angle, set on the
value's paint; a stale reading or an explicit `ValueColor` skips it. `valueGlow` draws the value text into a mask with
`SKMaskFilter.CreateBlur(Normal, sigma = blur / 2)` first, then the sharp text. The blurred text is kept as an
`SKImage` per (text, size, colour) in a small bounded cache (16 entries) on the `TextStyle` of the `Theme` and
disposed with it. A static cache in the SDK is avoided on purpose: widgets live in collectible load contexts, and the
SDK note in `AGENTS.md` says to keep plugin types out of long-lived static caches.

## Risks / Trade-offs

- [Tracking and FreeType/DirectWrite differences move glyphs by a fraction of a pixel] → spacing is added to measured
  advances, tested by width rather than by pixel position; Windows-only pixel tests use `[WindowsGlyphFact]`.
- [Glow on a card that repaints often costs a blur each time] → the cache; task 5.4 measures the cost against the
  package's budget (value glow at most 2x the readout draw time) and records it in `docs/perf/theme-look.md`.
- [`valueScale` does less than the drafts imply] → decision 6 and task 6.3; it is a tuning question for the theme
  changes, found early.
- [The extra typefaces cost memory] → about 1 MB private per extra family and almost nothing per clone (S3); a theme
  with two families is 1 MB above one with one. Measure Carbon (two families) on the panel when it lands.
- [Warning once per loaded theme needs state on the loaded theme] → a small set of reported keys; reloading the theme
  (a new `LoadedTheme`) warns again, which is the wanted behaviour after the user edits it.

## Migration Plan

None for users: no key changes meaning, and every existing theme is unchanged (the goldens say so). This change
depends on `theme-golden-harness` being merged first; without the goldens its central promise cannot be checked.

## Findings during apply

- **The member is `Theme.TextStyle`, not `Theme.Text`.** `Theme.Text` is the text colour (an `SKColor`) and cannot change
  type without breaking widgets. The types are `TextStyle`, `TextCase`, `TextFill` and `TextGlow` in `UrDeck.Sdk`; the
  package version is 0.9.0, the assembly version unchanged.
- **valueScale against the mocks (task 6.3).** The design board (`themes-2-ember-blueprint-pop-obsidian.png` and
  `themes-1-...` in the handoff's `assets/design-board`) draws every clock and tile value *smaller* than our fitted readout:
  Pop's `21:47` fills about half its clock card, Obsidian's sits left-aligned under the date, Ember's beside a session
  readout. "Runs big" in the mocks is numerals large against small labels, which the fitted readout already gives. The
  limit to the fit therefore leaves no theme short of its mock; the visible differences are the clock and gauge layouts
  (alignment, the date's position, a gauge's `readoutScale`), which belong to the widget and gauge changes, not to
  `valueScale`. The theme changes need no padding workaround for it.
- **A slant leans out of the readout's rectangle.** With the layout unchanged, the top of a slanted value reaches
  `tan(slant)` times its cap height past the rectangle's right edge (about 50 px for Ember's 10 degrees on a 4 x 2 clock),
  and a raised unit touches the top of the last digit (Ember's `63%` on a gauge). The theme changes for Ember and Rigline
  should check both on their sample page; if it shows, the fix is to count the lean in the unit gap and the fit (a change
  to the Slanted Value requirement), not padding.
- **The glow cost is over the package's 2x budget** (2.8x: 0.29 ms against 0.105 ms for a 400 px value, the copy cached);
  the absolute cost is 0.19 ms per repaint. How it was brought down from 30x is in `docs/perf/theme-look.md`.
- **Memory on the panel (task 6.4).** The host with Obsidian's typography (Jost and Bodoni Moda, per-role, `opticalSize`
  `auto`) holds 134.2 MB private against 131.9 MB with Jost alone and 134.5 MB with `default-dark` (median of three runs
  each): a second family is about 2.3 MB, twice the S3 estimate because the page and the dock each resolve the theme.
  Sharing the base typeface between the two resolves (S3 finding 4) would halve it; still not worth doing.

## Open Questions

- Whether the unit role should follow the value's optical size (decision 2 says yes). It affects only `auto` themes and
  can be changed without touching the specs' structure.
