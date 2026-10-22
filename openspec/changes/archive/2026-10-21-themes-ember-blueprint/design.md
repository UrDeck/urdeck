## Context

The drafts are `tests/UrDeck.Engine.Tests/Fixtures/themes/{ember,blueprint}/theme.json`. They hold no font files, so their
goldens (`ember-page.png`, `blueprint-page.png`, `blueprint-dock.png`) were drawn in the fallback font. The pattern layer
(`page-pattern`), gauge styles (`theme-gauges`), card chrome, icon sets (`theme-icons`) and motion levels are built, so both
drafts draw fully. The pattern layer is built so that these goldens are redrawn if the pattern changes. Built-ins are
embedded resources `UrDeck.Themes.<file>` and `ThemeStore.LoadBuiltIn` merges the file over `default-dark`, as for Carbon and
Paper (`themes-carbon-paper`, the template for this change). `urdeck-cli theme validate` exists and owns the contrast rule.
The backgrounds the drafts name (`embers`, `warpgate`, `equalizer`; `plotter`, `contour`, `streams`) all exist in
`src/UrDeck.Engine/Backgrounds/Builtin`.

The decisions below are defaults; the panel look may overrule them.

## Goals / Non-Goals

**Goals:**
- Both load from the engine alone, in their own fonts, with no warning and `theme validate` clean.
- What ships is what was looked at on the panel; each changed number has a reason.
- No engine change; engine findings become follow-ups.

**Non-Goals:**
- Host UI or a theme picker.
- Glyph-exact goldens on Linux (`[WindowsGlyphFact]`, as today).

## Decisions

### 1. Flat files, two fonts

`ember.json` and `blueprint.json` sit beside the other built-ins, with `BarlowCondensed-Bold.ttf`,
`IBMPlexMono-Regular.ttf`, `BarlowCondensed-OFL.txt` and `IBMPlexMono-OFL.txt`. The files come
from `spikes/theme-look-s3/fonts/` (untracked): compare each with its upstream release, confirm OFL 1.1, record version and
copyright in `THIRD-PARTY-NOTICES.md`. Not subset. Blueprint's title would take Medium through the per-role font keys, but those cover only value, unit and label; the
title is therefore weight 400 and no Medium file ships (follow-up recorded, task 1.4).

### 2. Corrections to the drafts

- **`valueScale`:** removed from both (Ember 1.2, Blueprint 1.05); a value is limited to its fitted size, so these do nothing.
- **Ember body weight:** 500 on a Bold-only file logs a warning on load. `body` is set to 700, the weight the file has; the
  built-in must load with no warning. A real 500 file is not added: a second condensed file is weight for no look.
- **Hex colours:** Ember's tick `#ffffff1c` is AARRGGBB, an opaque yellow-white; the intended white at alpha 0x1c is
  `#1cffffff`. Every hex value in both themes is checked for the same mistake. The other 8-digit values in the drafts
  already lead with alpha.
- **Gauge readout room:** an arc with ticks and an inside label needs extra readout room below the middle (STATUS.md,
  `theme-gauges`). Ember (tach) and Blueprint (marker, tick numbers) are tuned with the keys that exist (`gauge.layout`,
  tick length, `stroke.thickness`); if no key does it, a follow-up is recorded.
- **Contrast:** `urdeck-cli theme validate` is run on the drafts first. Expected findings: Blueprint's card fill
  `#cc0c2d52` is translucent over `#0c2d52`, so text and muted text are judged on the blended fill, and the dim track
  `#559fd0ff` and `critical` `#ff7a6b` are the likely `THEME060` to `THEME062` candidates; Ember's `textMuted` and
  `warning` on `cardFill` are probably fine. Fixes change only the offending value until the rule passes. Final values go
  in the contrast table.

### 3. Ember's lean

Slant 10 makes a value lean past its rectangle by about 50 px on a 4x2 clock (STATUS.md, "Typography as built"). Look at it
on the panel first; if the chrome clips or crowds it, reduce the slant. Whether the fit and the unit gap should count the
lean is an engine question: recorded as a follow-up, not fixed here.

### 4. Ember's glow cost

A glow on gauges costs 3.5x to 5.1x a plain gauge (`docs/perf/theme-look.md`); a tach is about +3 MB over a ring. Measure
Ember as a theme on the panel (private memory 25 s after start, CPU over 20 s, as in that document) with `motion: full` and
the `embers` background; accept if in line with the earlier Ember measurement (180 MB / 5.9 %), otherwise reduce
`gauge.glow.blur` or `valueGlow.alpha` until accepted. The numbers go in `docs/perf/theme-look.md`.

### 5. Icons and motion

Both name `icons.set: "outline"` (tint `accent` for Ember, `text` for Blueprint). Ember uses `motion.level: "full"`,
Blueprint `"reduced"`.

### 6. Built-ins replace fixtures; goldens

`Fixtures/themes/ember` and `blueprint` are deleted and their users (`GoldenTests`, `CardChromeTests`,
`CardUnderlayTests`, `GaugeThemeTests`, pattern tests) load the built-ins, as in `themes-carbon-paper`. Goldens stay at the
harness size: `ember-page`, `ember-dock` (added), `blueprint-page`, `blueprint-dock`, redrawn for fonts, pattern and tuning.
The v1 goldens and the other drafts' goldens must not change.

### 7. Tuning and site

The owner looks at each on the 1100x3840 panel at 100 % and 150 % over the default background and the static fallback, with
a deck like the site deck; the table (key, draft value, final value, reason) goes in the PR. Decks follow `06-themes.md`;
pictures are rendered with `scripts/Render-SiteDecks.ps1`. Docs describe both themes as drawn, including the hatch and grid.

### 8. Conflicts with sibling changes

Three other agents propose sibling theme changes. Shared files are `ThemeStore.BuiltInNames`, `THIRD-PARTY-NOTICES.md`,
`GoldenTests.cs` theory lists, `docs/themes.md`, `site/src/data/themes.json` and `STATUS.md`. Rebase on whichever lands
first; conflicts are list merges.

## Risks / Trade-offs

- [Condensed italic glowing numerals may crowd or clip in small cards] -> panel look; reduce slant or glow.
- [Glow cost] -> measure and accept (decision 4).
- [Real mono fonts widen Blueprint's tick numbers] -> tune tick labels and length on the panel.
- [The validator finds more than expected] -> fix values in this change; the rule wins.
- [A sibling lands first and moves shared files] -> rebase (decision 8).
- [Later pattern or gauge changes redraw these goldens] -> accepted; that is what the goldens are for.

## Migration Plan

Additive. Rollback removes the two names from `BuiltInNames`; configs naming them fall back to the default theme with the
usual warning.

## Open Questions

- Should the fit and the unit gap count a slanted value's lean (engine follow-up)?
- Does the panel overrule the motion levels, icon tints or Ember's body weight choice?
