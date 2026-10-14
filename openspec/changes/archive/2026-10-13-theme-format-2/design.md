## Context

`ThemeDefinition` (`src/UrDeck.Engine/Themes/ThemeDefinition.cs`, 329 lines) is a partial of nullable members:
`Colors, Card, Typography, Stroke, Gauge, Indicator, Press, Dock`. `Sanitize(themeName)` clears each invalid value and
logs a warning; `MergeOver(baseline)` fills gaps from `default-dark`, except `colors.backgroundPalette`. Unknown JSON
keys are ignored by `System.Text.Json`, which is what lets a format 2 theme degrade on an older host. `ThemeStore`
loads user themes from folders and built-ins from flat embedded `UrDeck.Themes.<name>.json` resources;
`ThemeResolver` turns cell fractions into pixels for the SDK `Theme`.

The look-and-feel handoff package (`docs/design/look-and-feel/`) specifies about 160 new keys and eight draft
themes. This change takes the keys the eight drafts use. Spikes S1 to S3 (`docs/perf/theme-look.md` on the spike
branches, `docs/handoff/2026-10-13-theme-look-spikes.md`) are done; none of them changes this model, but their
findings are noted where a later change needs a key to behave in a certain way.

## Goals / Non-Goals

**Goals:**
- One data model for format 2: parse, merge, sanitize, keep on the loaded theme.
- Every existing theme loads and renders exactly as before (nothing here draws).
- A published schema that cannot drift from the model.
- The eight drafts proven loadable, as fixtures.

**Non-Goals:**
- Drawing any new value (card chrome, typography, gauges, icons, pattern, motion) and the SDK `Theme` additions that
  carry them to widgets.
- Registering the eight themes as built-ins, bundling fonts or icons.
- The `urdeck-cli theme validate` command and its `THEME###` codes (a later change); `ThemeDefinition` only logs.
- Golden-image tests. v1 invariance is trivially true while nothing draws; the harness arrives with card chrome.
- The deferred keys (`docs/design/theme-format-2-deferred-keys.md`).

## Decisions

**One class, nullable members, split by group.** Format 2 is not a fork: new members sit in `ThemeDefinition` beside
the old, in partial files per group (`ThemeDefinition.Card.cs`, `.Typography.cs`, `.Gauge.cs`, ...). Each group is a small
class with the same `MergeOver` and `Sanitize` pair. Alternative considered: a separate `ThemeDefinition2` for format 2.
Rejected: two classes force a conversion and two sets of merge rules, and `format` is optional, so a loader could not
choose before reading the file.

**Merge by member, recursively, with a short list of groups that are not inherited.** The rule is the existing
`value ?? baseline`, applied to nested objects, so `card.shadow.blur` inherits independently of `card.shadow.color`.
`default-dark` defines none of the new groups, so inheriting and being absent coincide for them; the spec states the
list anyway so nobody adds one to `default-dark` and changes every theme. `colors.ramp`, `colors.cardTints` and the
background palette are explicitly not inherited (a light theme must not receive the dark theme's).

**A key whose absence means "off" stays absent after the merge.** The merged result never contains a default object
for a feature that is off. This is what keeps the three built-ins and every old user theme drawing as before, and it
lets the later changes test `Card.Shadow is null` for "no shadow".

**Sanitize clears, the merge takes the default.** The v1 template is reused word for word:
`Theme '{name}': {path} {value} is outside {min}..{max}; using the default.` New `ValueCheck.Enum`, `Colors(path, list,
min, max)`, `Angle` and `Array` helpers replace open-coded checks. Enumerations are parsed from strings by hand (as
`ThemeGaugeDefinition.Parse` does today), case-insensitively, rather than through `JsonStringEnumConverter`, so a bad
name is a warning and not a deserialisation failure that skips the whole theme.

**Cross-field rules run after the per-field checks and only on what the file alone decides.** The rules are the seven
of the handoff's section 5 that need no other file. Two things are deliberately left to the later validator command and
the card chrome change, because they need more than one theme file or a rendered layout:
- shadow or glow reach against the gap when the card fill is translucent (the S1 finding; a sprite underlay cannot cut
  out the neighbours). It is a warning there, never an error. Prism's draft breaks it (reach about half a cell, gap
  0.09, fill 15% white), and the mock does too, so it must stay loadable without a warning;
- the contrast check of text on card fill.

**The new gauge styles are names only.** `GaugeStyle` in the MIT SDK is not extended in this change. The definition
accepts `meter`, `tach`, `marker` and `thickbar` for `gauge.style`, keeps them, and the resolver maps them to `ring`
for the SDK `Theme` until the gauge change adds the enum values and the drawing. Alternative: add the enum values now.
Rejected: it would be new public SDK surface with no behaviour behind it, and the SDK is an API third-party widgets
compile against.

**The drafts are fixtures, not built-ins.** `ThemeStore.LoadBuiltIn` resolves any embedded `UrDeck.Themes.<name>.json`
regardless of `BuiltInNames`, so putting the drafts under `Themes/Builtin/` would make `"theme": "carbon"` work by
accident, with fonts and icons that do not exist yet. They go under `tests/UrDeck.Engine.Tests/Fixtures/themes/<name>/`
instead and move to `Themes/Builtin/<name>.json` (the flat layout) in the change that delivers each theme. Four edits
are made to the drafts on the way, all recorded in the deferred-keys document: Prism loses `card.blur`, Rigline loses
`typography.valueFill.animate` and `pattern.scroll`, and Pop takes its numerals from Archivo (S3: Archivo Black
duplicates Archivo at weight 900 within 1%).

**The schema is trimmed to the model and tested against it.** `assets/schema/theme.schema.json` from the package is
copied to `docs/schemas/v2/theme.json` with the deferred keys removed. A test walks the schema and the model's
properties by reflection and fails on a key present in one and not the other. The test uses a JSON Schema validator
package referenced from the test project only (no runtime dependency), added to `Directory.Packages.props`.

**Validation is advisory about files.** `icons.set`, `background.default.name` and `meta.pairsWith.backgrounds` name
things that may not exist (a user's own set or background); the model does not check, as the spec says. Checking
them is the validator command's job.

## Risks / Trade-offs

- **About 140 keys in one change is a lot to review.** → Commit group by group (colours, card, pattern and parts,
  typography, gauge, icons, background, motion, meta), each with its tests, so the diff can be read in order.
- **A value is accepted and does nothing for several changes.** A theme author sees no effect. → The reference marks
  each group with the change that draws it; `docs/themes.md` is written to say "read, not yet drawn".
- **`System.Text.Json` and nullable value types.** A number that is `null` in the file and an absent number look the
  same. → Same as format 1; no new handling.
- **Schema and model both hand-maintained.** → The drift test, and every key has a default in one place.
- **Prism's draft shows a shadow through neighbours at render time (S1).** Not a model problem; carried to the card
  chrome change as a validator warning and an eye check on a snapshot.

## Migration Plan

Additive. No user action, no config change, no data migration. Built-in and user themes load as before. Rollback is
reverting the change; nothing is persisted.

## Open Questions

None that change what gets built. The package's open question about naming (`background.default` or `background.selects`)
is answered: `background.default`.
