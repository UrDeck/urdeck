## 1. Before touching anything: record what must not change

- [x] 1.1 Record `shader validate` output for the sixteen built-in backgrounds and for a folder with one finding of each severity, in `text`, `markdown`, `json` and `sarif`, as snapshot files in `tests/UrDeck.Cli.Tests`; the test compares byte for byte
- [x] 1.2 Record the log lines the engine writes when it loads every built-in theme, every fixture theme under `tests/UrDeck.Engine.Tests/Fixtures/themes`, and a set of themes with one failing value per kind of check (colour, number, integer, angle, enum, colour list, string list, number list, name list, wrong kind, missing member, each rule); store them as a text snapshot, taken on `main`
- [x] 1.3 Move `SamplePage` into the new `src/UrDeck.Preview` project (GPL header, `$(UrDeckTfm)`, references the engine and the four widget projects), add `ThemePreview.Render(theme, page, width, height)` over `PageRenderer.RenderToBitmap`, add it to `urdeck.slnx`, and repoint `GoldenTests` (this is its only edit); do it as its own commit
- [x] 1.4 Confirm the goldens are unchanged after 1.3 and that `Preview` builds and its test project runs on Linux

## 2. Structured findings in the engine

- [x] 2.1 Add `ThemeFinding(Code, Severity, Key, Message)`, `IThemeFindings`, a log sink that writes the text of 1.2, a collecting sink, and the code constants (one place, as `ShaderCodes` is)
- [x] 2.2 Give `ThemeValueCheck` a sink and a code per check kind (design 1); settle `Missing` (design, open question) and keep every message as it is
- [x] 2.3 Give `CheckRules` its codes `THEME050` to `THEME053` and `THEME090`; make `ThemeWarnings.Once` and `InfoOnce` forward to the sink so the resolver's font, weight and reach findings keep their once-per-load rule and get `THEME023` and `THEME054`
- [x] 2.4 Test: the log text for all themes of 1.2 is equal to the snapshot; the goldens are unchanged
- [x] 2.5 Test: each check kind produces one finding with the right code, key and severity

## 3. Sources and the shared loader

- [x] 3.1 Add `IThemeSource`, `DirectoryThemeSource` and `EmbeddedThemeSource`, with the containment test `ThemeStore` uses (a backslash is a separator on every platform)
- [x] 3.2 Move the body of `ThemeStore.LoadUser` into `ThemeLoader.Load(source, baseline, sink)` and call it from `LoadUser`; the existing `ThemeStore` tests stay as they are and pass before anything else is added
- [x] 3.3 Test: for each built-in theme the fast path of `LoadBuiltIn` and the loader over `EmbeddedThemeSource` give the same definition
- [x] 3.4 Test: a folder named like a built-in is read as the folder; a font path with `..\` or an absolute path is refused on Windows and on Linux and no file outside the folder is opened

## 4. `ThemeLint`

- [x] 4.1 `ThemeLint.Check(source, name, options)` returning findings with their file; `THEME001` for a missing, unparseable or empty file stops the check; comments and trailing commas are accepted
- [x] 4.2 The unknown-key walker (design 3): case-insensitive, `$schema` accepted, the gauge gradient and arrays of groups walked, a suggestion within two edits; run by the lint only, never by the host
- [x] 4.3 `THEME010` (format), and map the engine findings of section 2 into the result
- [x] 4.4 Fonts: `THEME020` to `THEME022` (existence, extension, containment, size over 3 MB, licence file beside it, named by family) and make the resolver's weight check usable for `THEME023` from the lint without drawing
- [x] 4.5 Backgrounds and pairing: `THEME040` to `THEME042` against `BackgroundCatalog` plus the directories in the options, `THEME043` for the still picture, `THEME044` against the (empty) list of icon sets
- [x] 4.6 Contrast: `THEME060` to `THEME062` with the card fill, its gradient stops and each card tint composited over `colors.background`; the message says what was assumed
- [x] 4.7 `THEME054` from the underlay's own reach routine at the 1100 pixel panel width; `THEME080` for missing `meta`
- [x] 4.8 Tests: a minimal failing folder and a passing one for every code in the `theme-lint` spec, under `tests/UrDeck.Engine.Tests/Fixtures/lint/` (the same folders are used by the CLI tests in section 6)
- [x] 4.9 Run the lint on all five built-in themes, with warnings counted. If any finding appears, stop and take it to the owner (design, Risks) before changing a theme or a rule; tick this only when `--builtin --fail-on-warnings` would be clean
- [x] 4.10 Run the lint on the six fixture themes (Prism, Obsidian, Ember, Blueprint, Pop, Rigline) and record the findings in the design's "Findings during apply"; the theme changes that follow will need them

## 5. The report model

- [x] 5.1 Generalise `BackgroundReport`, `Counts` and `ReportWriters` over a subject kind and a CLI-owned `Diagnostic` (design 5); the JSON array is `backgrounds` or `themes`; SARIF gets a rule table per kind
- [x] 5.2 Confirm the snapshots of 1.1 are still byte-equal

## 6. `theme validate`

- [ ] 6.1 Add the `theme` command group and `validate` with `<folder>`, `--all`, `--builtin`, `--background-dir` (repeatable), `--format` and `--fail-on-warnings`; arguments that exclude each other are exit code 2
- [ ] 6.2 `--all`: folders directly inside, hidden ones skipped, ordinal order, empty directory is exit code 2, not recursive; `--builtin`: the built-in names in order
- [ ] 6.3 File paths in every format: relative to the working directory with `/`; a built-in theme is `builtin:<name>/theme.json`
- [ ] 6.4 Tests (`tests/UrDeck.Cli.Tests`): the scenarios of the `urdeck-cli` delta for validate, validate-many and output formats, using the fixtures of 4.8, on Windows and Linux
- [ ] 6.5 Test: the `help` text of the group and of each command names the options and exit codes 0, 1 and 2

## 7. `theme render`, `init`, `schema`

- [ ] 7.1 `theme render <folder>|--builtin <name> -o <file>` with `--page`, `--size`, `--fail-on-warnings`; findings to the error stream; no file when the theme cannot be read
- [ ] 7.2 Test: a built-in rendered with the golden size and page equals the golden picture where the platform draws text the same way (`[WindowsGlyphFact]`), and renders to the same bytes twice on both platforms
- [ ] 7.3 `theme init <name>` with `--output` and `--from`: starter content, the self-contained copy of a built-in with its fonts and licences, no overwrite, the name rule
- [ ] 7.4 Test: `init` then `validate` is clean for the starter and for `--from` of every built-in; an existing folder is left as it is
- [ ] 7.5 `theme schema` and `--output`: embed `docs/schemas/v2/theme.json` as a linked resource; test that the printed text equals the file on disk
- [ ] 7.6 Update the tool's `Description` in the project file and the root command's help

## 8. CI, docs and bookkeeping

- [ ] 8.1 `.github/workflows/ci.yml`: add `theme validate --builtin --fail-on-warnings` where `shader validate --all` runs on Windows and on Linux, and an `init` then `validate` of a copy of a built-in, as there is for backgrounds
- [ ] 8.2 Update `site/src/content/docs` (the command line tool page) and `docs/themes.md` (a "Checking a theme" section with every code, what it means and how to fix it); the docs test that lists codes passes
- [ ] 8.3 Update `openspec/specs/urdeck-cli/spec.md`'s Purpose to mention themes (the archive does not change a Purpose)
- [ ] 8.4 Update `docs/BACKLOG.md` and `docs/design/look-and-feel/STATUS.md` (PR 11 done; the validator findings list is built; contrast is owned here); note the "check a repository" direction in `docs/ROADMAP.md`
- [ ] 8.5 `dotnet build urdeck.slnx -c Release` with no warnings, `dotnet format urdeck.slnx --severity warn --verify-no-changes`, `dotnet test urdeck.slnx -c Release`, the Linux test projects, and `openspec validate --all --strict`
- [ ] 8.6 Run the tool by hand on a theme with a deliberate mistake of each kind and read the output as an author would; fix any message that does not say how to fix it
