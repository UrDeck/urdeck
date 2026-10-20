## Why

A theme author has no way to check a theme except to load it in the host and read `urdeck.log`. The six themes still to
come (Prism, Obsidian, Ember, Blueprint, Pop, Rigline) each have to be "validated clean", and the findings that matter
most (a text colour that does not contrast with its card, a shadow that reaches into the next card, a font weight the
file does not have, a background the theme does not pair with, a key that is misspelt) are only visible in a running
host. `urdeck-cli` already does this for backgrounds (`shader validate`, `render`, `init`); themes have nothing.

The engine cannot yet feed a tool. Its theme checks write prose to the log, so they have no codes, no file and no
severity, and `ThemeDefinition.Sanitize` is internal. A second set of rules in the tool would drift from the host's the
first time a value's range changes.

## What Changes

- **Structured findings in the engine.** The theme checks (`ThemeValueCheck`, `ThemeDefinition.CheckRules`, the resolver's
  font and reach warnings) report a `ThemeFinding` (code, severity, key path, message) to a sink. The host's sink writes
  the log lines it writes today, unchanged; the tool's sink keeps the findings. One set of rules, two readers.
- **`ThemeLint`**, a public engine entry point: `ThemeLint.Check(source, name)` over an `IThemeSource` (a folder on disk,
  or a built-in theme embedded in the engine) returns the findings, with the codes `THEME001` and up.
- **`urdeck-cli theme validate`** checks a theme folder, every folder in a directory (`--all`), or the built-in themes
  (`--builtin`), in the formats `shader validate` has (`text`, `markdown`, `json`, `sarif`) and with the same exit codes
  and `--fail-on-warnings`.
- **New findings** the tool owns: keys the host does not read (with "did you mean"), text contrast on the card fill and on
  every card tint, a default background that is not in `pairsWith`, `pairsWith` and `icons.set` naming something that does
  not exist, font files (missing, outside the folder, too large, no licence file), weights a font does not have, shadow
  or glow reach beyond the gap, and outside corner marks that reach past the gap. The set-aside keys of
  `docs/design/theme-format-2-deferred-keys.md` are not in the schema, so they are reported as unknown keys.
- **`urdeck-cli theme render`** draws the sample page in a theme to a PNG, on any platform, from a shared sample page
  that moves out of the engine tests.
- **`urdeck-cli theme init`** copies a built-in theme into a new theme folder; **`theme schema`** prints the schema.
- **A report model shared by `shader` and `theme`** (a subject kind, a name, a folder, findings). The `shader` output
  does not change by a byte.
- **CI** validates the built-in themes with `--fail-on-warnings`.

Not in this change: icon-set findings (`THEME030` to `THEME032`; `theme-icons` owns them), the animation cost class
(`motion` is not built), a command that checks a whole repository of themes, backgrounds and plugins (recorded in the
design as a direction), a recursive `--all`, a change to any theme's pixels.

## Capabilities

### New Capabilities

- `theme-lint`: the findings about a theme: their codes and meanings, which ones fail a run, the sources a theme can be
  read from, and the rule that the host and the tool report the same findings.

### Modified Capabilities

- `urdeck-cli`: the `theme` commands (`validate`, `render`, `init`, `schema`), their options, output formats and exit
  codes; the tool's description now covers themes as well as backgrounds.
- `theme`: the engine reports the findings it logs as structured findings as well, and logs them unchanged.

## Impact

- `src/UrDeck.Engine/Themes`: `ThemeFinding`, `IThemeSource` with a directory and an embedded implementation,
  `ThemeLint`; `ThemeValueCheck`, `ThemeDefinition.Rules.cs`, `ThemeStore` and the resolver's warnings take a sink.
  The log text is unchanged (a test compares it for every built-in and fixture theme).
- `tools/UrDeck.Cli`: `theme` commands; the report writers and models are generalised over the subject kind.
- A shared sample-page project (`src/UrDeck.Preview`, referencing the engine and the four first-party widgets); the
  sample page and its builder move out of `tests/UrDeck.Engine.Tests/Support`. `GoldenTests.cs` changes its `using` only,
  which is the one place that can conflict with the `page-pattern` change.
- `tests/UrDeck.Cli.Tests`: a failing and a passing fixture folder for each code.
- `.github/workflows/ci.yml`: one step. `docs/themes.md`, `site/src/content/docs` (the command line tool page),
  `docs/BACKLOG.md` and `docs/design/look-and-feel/STATUS.md`.
- No new runtime dependency. The schema (`docs/schemas/v2/theme.json`) is embedded in the tool for `theme schema` and
  is not read by the engine.
