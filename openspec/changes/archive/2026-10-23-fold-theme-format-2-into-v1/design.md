## Context

See proposal.md - Why. Today the model has two layers. `ThemeDefinition` carries the original members; `ThemeFormat2.*`
partials carry the members added by the look-and-feel work, marked with `[Format2]`. `ThemeLenientJson` uses that mark
twice: a marked member of the wrong JSON kind is removed with a warning instead of failing the deserialization, and
nothing else is. `ThemeLint` (`THEME003`) mirrors this: an error for an unmarked member, a warning for a marked one.
`ResolvedCardStyle` has an "is it version 1" notion that is about whether a theme sets any card chrome value, not about
the `format` key; it is unrelated and stays.

## Goals / Non-Goals

**Goals:**
- One theme format with no version key, one schema path, one vocabulary in code, specs and docs.
- Pixel-identical output: all goldens pass unchanged without regeneration.

**Non-Goals:**
- No new theme keys, no change to a key's range or default, no change to merging.
- No config-file version. That is a separate milestone item (the configuration, not the theme).
- No rewrite of archived changes or of `docs/design/look-and-feel/`; they are the record of what was decided then.
- No change to the card chrome rule that a theme setting no card chrome value draws as before (`widget-card`); only the
  engine's internal naming around it, if it mentions "format", is touched.

## Decisions

1. **Drop `format` entirely; do not keep it as an ignored key.** A theme that still has one gets `THEME002`, which says
   the key is not read. Keeping a silently accepted key would keep the distinction alive in files. There are no outside
   themes; the repository's own are edited in this change. Alternative considered: keep `format: 1` as a future hook.
   Rejected: a version key is added with the first incompatible change, and a theme format that has not shipped has
   none to describe.

2. **Lenient reading applies to every key.** `ThemeLenientJson.Clean` no longer asks whether a member is marked; every
   scalar member whose JSON kind is wrong is removed with one `THEME003` warning and takes its default. This is the
   decision made with the owner: hand-edited files should lose one value, not the theme. It is the only behaviour change
   in this work, and it relaxes a failure into a warning. Alternative: keep strictness for the original keys. Rejected:
   the distinction exists only because of the history being removed.
   `Format2Attribute` is deleted; the converter check (`JsonConverterAttribute` members) and the gradient-stops
   special case stay as they are.

3. **`THEME010` is deleted, not reassigned.** Finding codes are published (SARIF rule ids, the site's troubleshooting
   page). A retired number stays retired; the table in `theme-lint` simply no longer lists it. The site's completeness
   check, which fails on an undescribed code, must pass after the removal.

4. **Move the schema with `git mv` and change only its `$id`, title and any "format 2" wording.** The existing
   schema-versus-model test keeps its role and reads `docs/schemas/v1/theme.json`. `theme schema` prints it byte for
   byte as before, so the CLI's linked resource path changes and nothing else. The site already copies schemas to
   `/schemas/<v>/` for `shader-manifest.json`; the check script and `site/README.md` name the new file.

5. **Rename partials mechanically.** Where `ThemeDefinition.<Group>.cs` already exists (`Card`, `Colors`, `Gauge`,
   `Typography`) the `ThemeFormat2.<Group>.cs` members are merged into it; the rest are renamed to
   `ThemeDefinition.<Group>.cs`. Each file keeps its SPDX header. `git mv` where a file is only renamed;
   `dotnet format` runs afterwards.

6. **Spec requirements are renamed, not rewritten.** The "Format 2 ..." requirements keep their rules and scenarios;
   only the title and the version wording change. `widget-card` and `components` use "format 2" as a phrase for "card
   chrome values / gauge values"; their rules do not change and their wording is left to the next change that touches
   them rather than widening this one. The `backgrounds` requirement that points at "Format 2 Values" is updated
   because the target is renamed.

## Risks / Trade-offs

- [A missed `"format"` key makes a built-in or test theme warn] → `theme validate --builtin --fail-on-warnings` and the
  "built-ins load with no warning" tests catch it; a grep for `"format"` in `src`, `site`, `tests` and `tools` is a task.
- [Lenient reading hides a typo in an original value behind a warning] → it is still a `THEME003` warning in the log and
  in `theme validate`, and `--fail-on-warnings` makes it a failing check for authors and CI.
- [The generated site reference or `catalog.json` drifts] → `npm run build` runs the completeness and reference checks;
  regenerate and commit `site/src/generated` if the build says it is stale.
- [Goldens move] → they must not. If any golden test fails, the rename touched behaviour; fix the change, do not
  regenerate (`URDECK_UPDATE_GOLDENS` stays unset).
