## Context

The drafts are in `tests/UrDeck.Engine.Tests/Fixtures/themes/{carbon,paper}/theme.json` and drive several engine tests
(`GoldenTests`, `CardChromeTests`, `CardUnderlayTests`, `GaugeThemeTests`). Those folders hold no font files, so every
render of the drafts so far fell back to the default font: the goldens `carbon-page.png`, `paper-page.png` and
`paper-dock.png` show Inter, not the themes' faces.

Built-in themes are embedded resources named `UrDeck.Themes.<file>` (the `Themes\Builtin\*.json;*.ttf;*.txt` glob in
`UrDeck.Engine.csproj`); `ThemeStore.LoadBuiltIn` reads `<name>.json`, merges it over `default-dark` and opens font files
by name from the same resources. `ThemeCatalog` lists `ThemeStore.BuiltInNames`. The built-in background manifests already
name `carbon` and `paper` in their `pairsWith.themes`, and every background the two drafts list exists.

`theme-cli` is being built at the same time in another worktree. It owns the contrast rule and will move or share the
sample page (`Support/SamplePage.cs`).

## Goals / Non-Goals

**Goals:**
- Both themes load from the engine alone, in their own fonts, with no warning, on Windows and Linux.
- What ships is what was looked at on the panel, and the numbers that changed say why.
- The engine is not changed: if tuning finds something only the engine can fix, it is recorded as a follow-up, not
  fixed here.

**Non-Goals:**
- Anything in the host. Selection stays the `theme` key.
- Glyph-exact goldens on Linux (the golden tests are `[WindowsGlyphFact]`, as today).

## Decisions

### 1. Flat files, fonts beside them, no folder per theme

`carbon.json` and `paper.json` go next to `default-dark.json`; the two `.ttf` files and `InstrumentSans-OFL.txt`,
`JetBrainsMono-OFL.txt` next to `InterVariable.ttf`. The existing globs embed them; nothing in `ThemeStore` changes but
`BuiltInNames`. The package's `Builtin/<name>/theme.json` layout was already overruled for the flat one (`STATUS.md`). Both
themes share the fonts, so one copy serves both. A folder per theme would need a loader change and duplicate the fonts.

The font files come from `spikes/theme-look-s3/fonts/` (untracked, downloaded for spike S3). Before committing, compare
each with its upstream release (Google Fonts' repositories), check that its licence file is OFL 1.1, and record the
version and copyright line in `THIRD-PARTY-NOTICES.md`. Not subset: installer size is not a concern (`STATUS.md`).

### 2. The built-ins replace the fixtures

`Fixtures/themes/carbon` and `paper` are deleted. Tests that used the draft (`Draft("paper")`, `draft: true`) load the
built-in instead, so there is one source of truth and the tests see the shipped values. A tuned value that changes an
asserted number (for example the underlay reach in `CardUnderlayTests`) updates the assertion in the same commit, with the
reason in the commit message. The other six drafts stay fixtures.

Alternative: keep the fixtures frozen as engine inputs. Rejected: two copies would drift, and the fixtures were only
ever stand-ins for these themes.

### 3. Goldens at the harness size; the 3840 picture is the site deck

`carbon-page.png`, `paper-page.png` and `paper-dock.png` are redrawn from the built-ins (they change: real fonts), and a
`carbon-dock.png` is added (Carbon's dock has corner marks and compact cards). They stay at the harness's 1100x1920. The
package also asked for 1100x3840 goldens; the sample page fills the first 1920, so a taller picture adds empty ground and
no coverage. The full panel height is checked by the site deck pictures (`Render-SiteDecks.ps1`, 1100x3840) and the panel
look. The v1 goldens must not change.

### 4. Settling the three open points

- **Paper's `valueScale: 1.1`:** removed. A value is limited to its fitted size, so 1.1 draws the same as 1.0; keeping it
  suggests a lever that does nothing. If the panel shows Paper's numerals too small, that is a fit question for the
  engine, recorded as a follow-up.
- **Carbon under load:** keep `gauge.colorBy: "accent"`. The bar stays phosphor, and the status word (`OK`, `WARM`, `HOT`,
  coloured by status) carries the load. `ramp` was considered: it blends the fill along the fraction, so at 68 % the bar
  is already part-way to amber while the word says `OK`, which contradicts itself. A fill that follows the status level
  (mint below 0.7, amber to 0.9, red above) needs an engine value that does not exist; it is a follow-up. The description
  in `meta` is reworded if it promises a bar that changes colour.
- **Paper's vermilion as text:** no role in Paper draws text in the accent colour unless a widget asks for it (the status
  chip is off in Paper). Its contrast (about 3.6:1 on the card fill) is recorded; if the panel shows accent text anywhere
  that must be read, the accent is darkened until it reaches 4.5:1, and the new value is recorded.

### 5. Contrast checked by hand, not by a test

Text and muted text on the card fill, and each status colour on the card fill, are computed with the WCAG formula and
listed in the pull request (first estimates: Carbon muted about 6.4:1, Paper muted about 5.2:1). The rule itself is
`theme-cli`'s; writing it here too would put two copies in `ThemeDefinition.Rules.cs`. Whichever change lands second
must pass "built-ins validate clean" with both themes.

### 6. Tuning on the panel

The owner looks at each theme on the 1100x3840 panel, over its default background (`contour`), over its static colour
(`background: none` is not the static fallback; force it with shaders unavailable or a theme override in a scratch
folder), and at 100 % and 150 % display scaling. Each value that changes is recorded in a table in the pull request:
key, draft value, final value, reason. Known things to look at: Carbon's corner marks against the hairline border at the
inset, Paper's shadow at the panel's real brightness, the tracking of mono labels in both, Carbon's dot pattern being
absent (expected; `page-pattern`).

### 7. Site

The decks follow `06-themes.md` ("Site deck widgets"). Pictures are rendered with `scripts/Render-SiteDecks.ps1`, so they
obey "Sample Decks Are Real". `site/src/data/themes.json` gains two entries with captions and alt text;
`site/src/content/docs/themes.md` lists the five built-ins. It describes Carbon as it draws today, with no dot grid; the
grid is added to the page with `page-pattern`. No media loops.

## Risks / Trade-offs

- [The real fonts change layout: mono numerals are wider, so a value may fit smaller than in the fallback renders] →
  The panel look is the judge; the sample page goldens show it first. Tracking values are the first lever.
- [`theme-cli` lands first and its contrast rule fails a draft value] → Fix the value in this change; the rule wins.
- [`theme-cli` moves `SamplePage`, conflicting with the golden edits] → This change only edits test data and the theory
  lists in `GoldenTests.cs`; rebase on whichever lands first.
- [Carbon ships without its dot grid, and its goldens change again with `page-pattern`] → Accepted (option C of the
  explore); `page-pattern` carries the redraw and a second panel look.
- [Built-ins merge over `default-dark`; a value Paper does not set comes out dark] → Spec scenario "Paper is light
  throughout"; a test compares every v1 colour of `paper` with its own file.
- [About 380 KB more in the engine assembly] → Accepted; installer size is not a concern. Fonts are opened only for a
  theme that names them.

## Migration Plan

Additive. Rollback is removing the two names from `BuiltInNames`; a config that names `carbon` or `paper` then falls back
to the default theme with the usual warning.

## Open Questions

- Whether Carbon's `meta.description` keeps "turns amber under load" (decided by how the panel reads, decision 4).
