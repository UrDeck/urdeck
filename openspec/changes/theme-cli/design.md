## Context

`urdeck-cli` has `shader validate|render|init` (`tools/UrDeck.Cli`, DotMake). It reads only the engine's public API (it is
not in the engine's `InternalsVisibleTo`), runs on Windows and Linux, and keeps three exit codes. For backgrounds the
engine already returns structured findings: `ShaderLint.Check(IBackgroundSource, name)` gives `ShaderDiagnostic(Code,
Severity, Message, File)`, the CLI prints them, and the host logs the same ones.

Themes have no such seam:

- `ThemeValueCheck`, `ThemeDefinition.CheckRules` and the resolver's `loaded.Warnings.Once(...)` write prose to
  `UrDeckLog`. About 26 call sites. No code, no key, no severity beyond the log level.
- `ThemeDefinition.Sanitize` and `CheckRules` are `internal`.
- User themes load in `ThemeStore.LoadUser` (folder, `ThemeLenientJson.Read`, `Sanitize`, font-path checks, `MergeOver`,
  `CheckRules`). Built-ins load in `ThemeStore.LoadBuiltIn` (embedded JSON, no checks, trusted). Built-ins are flat
  embedded resources named `UrDeck.Themes.<file>`; fonts are shared between themes (Carbon and Paper use the same
  JetBrains Mono). The handoff's `Builtin/<name>/theme.json` layout was overruled in `STATUS.md`.
- Unknown keys are silent at load on purpose (handoff decision D14); only the tool warns.
- `SamplePage` (a clock, two stats cards, a weather card, four shortcuts, fixed readings and time) is in
  `tests/UrDeck.Engine.Tests/Support`. It builds the real first-party widgets, which the engine cannot reference.
  `GoldenTests` renders it with `PageRenderer.RenderToBitmap`.
- `docs/schemas/v2/theme.json` exists and a test already fails when the schema and the model differ (spec `theme`,
  "Published Theme Schema"). It is not read at runtime.

`page-pattern` runs in another worktree and will touch `ThemeDefinition.Pattern`/`CheckPattern` and redraw Carbon's goldens.
`themes-carbon-paper` is merged.

## Goals / Non-Goals

**Goals:**
- One set of rules: the deck and the tool report the same findings, from the same code.
- `theme validate` on a folder, a directory of folders, or the built-in themes as shipped.
- `theme render`, `init`, `schema`, so the six remaining theme changes can each end with "validates clean, rendered".
- The `shader` output and the deck's log text do not change by a byte.

**Non-Goals:**
- Icon-set findings (`THEME030` to `THEME032`, `theme-icons`), the animation cost class (`motion`).
- A command that checks a whole repository (below, "Direction").
- Changing any theme's pixels or values.
- Reading the schema at runtime, or validating a theme by JSON Schema. The model is the truth, the schema follows it.

## Decisions

### 1. Findings are reported to a sink; the log is one sink

```
ThemeValueCheck / CheckRules / resolver ──► IThemeFindings.Report(ThemeFinding)
                                                  │
                          ┌───────────────────────┴──────────────┐
                    LogFindings (host)                      CollectedFindings (ThemeLint)
                    "Theme 'x': <message>"                  ThemeFinding[] → CLI
```

`ThemeFinding(Code, Severity, Key, Message)` is public; the file is added by the lint, which knows the source. A finding's
`Message` is the log text without the `Theme 'name': ` prefix; the log sink puts the prefix back, so the log is the same
text as now. Each existing call site gets its code from what it checks, not from a lookup: `Color` is `THEME006`,
`Number`/`Integer`/`Angle` `THEME004`, `Enum`/`NameList` `THEME005`, `Colors`/`Strings`/`Numbers`/`Palette` `THEME007`,
`WrongKind` `THEME003`, and each `Rule(...)` takes its own code (`THEME050` to `THEME053`, `THEME090`). `Missing` (a group
that needs a member) gets `THEME004`'s sibling only if a test finds it; if it fits no code it is `THEME007`.

`ThemeWarnings.Once(key, message)` keeps its once-per-load rule and forwards to the sink, so the resolver's font and reach
findings (`THEME023`, `THEME054`) arrive with the same dedupe. The sink is a property of the loaded theme, set by whoever
loads it; the default is the log.

The test that guards this: for every built-in theme and every fixture theme, plus a fixture with one failing value per
check, the log lines before and after the refactor are equal. The "before" lines are recorded once from `main` as a
golden text file.

Alternative: capture the log in the tool (`LogCapture`, which exists) and parse. Rejected: no codes, no key, and the
message would become an API by accident.

### 2. `IThemeSource`, with a folder and an embedded implementation

```
IThemeSource   Name · ReadSettings() · Open(file) · Exists(file) · Length(file) · Files()
 ├─ DirectoryThemeSource(folder)    the same containment test ThemeStore uses (backslash is a separator on every platform)
 └─ EmbeddedThemeSource(name)       UrDeck.Themes.<file> resources; Files() = the resource list
```

`ThemeStore.LoadUser` is rewritten on top of a shared `ThemeLoader.Load(source, baseline, sink)` (read, lenient parse,
sanitize, font-path checks, merge, rules), and `ThemeLint` calls the same method. There is then one load path for a
folder, and the tool cannot disagree with the host about a user theme. `LoadBuiltIn` keeps its fast path (no checks,
trusted); lint of a built-in goes through the loader with an `EmbeddedThemeSource`. A test asserts the two paths give the
same definition for every built-in, so the fast path cannot drift.

`--builtin` rather than pointing `--all` at the source directory: it checks the bytes that ship, so a font that is in the
tree but not embedded is `THEME020` instead of a runtime "font could not be read". The flat layout of
`src/UrDeck.Engine/Themes/Builtin` stays; no folder per theme, no duplicated fonts.

### 3. Unknown keys come from the model, not from the schema

`ThemeLenientJson.Clean` already walks the JSON against `ThemeDefinition` by reflection. A sibling walker, run by the lint
only (the host stays silent, D14), reports each key that has no property (case-insensitively, as the serializer reads),
with the closest sibling within two edits as the suggestion. Special cases: `$schema` is accepted at the top; the gauge
gradient with its converter is walked as `ThemeGradientDefinition`; arrays of groups walk each element.

The schema is not read: it would be a second key list that the existing drift test already keeps equal to the model. The
deferred keys are not in the schema or the model, so they are reported as unknown, with no special message.

### 4. Which checks the tool alone makes

The engine's findings in decision 1 plus these, all in `ThemeLint` (engine, so they can use internals; the CLI stays on
the public API):

- **Contrast** (`THEME060` to `THEME062`): WCAG ratio, using `ColorContrast` (it is internal to the engine and moves
  nothing). The card fill is `colors.cardFill`, or each stop of `card.fillGradient` when set. A translucent fill is
  composited over `colors.background`; a moving background is unknown, so the message says what was assumed. Each
  `colors.cardTints` entry is checked against `colors.text` (a tinted card keeps the theme's text colour).
- **Fonts** (`THEME020` to `THEME023`): file existence, extension, containment, size, licence file next to it, and the
  weight against the font file's range or its static weight (the resolver already computes the second; the lint reuses
  that routine, which is why `THEME023` comes from the sink and not a second implementation).
- **Backgrounds and pairing** (`THEME040` to `THEME042`, `THEME044`): the known backgrounds are `BackgroundCatalog`'s
  built-ins plus every folder in the directories given with `--background-dir`. Known icon sets: none exist before
  `theme-icons`, so `icons.set` is checked against an empty list and a theme that sets one gets a warning; `theme-icons`
  fills the list.
- **Reach** (`THEME054`): taken from the underlay's reach routine with the theme's own gap, not recomputed.
- **Metadata** (`THEME080`).

Messages are one line and end with the fix, as the shader lint's do.

### 5. The report model is generalised, the `shader` output is not

`BackgroundReport`, `Counts` and `ReportWriters` are typed to `ShaderDiagnostic`. They become a `SubjectReport(Kind, Name,
FullPath, Diagnostics)` over a small `Diagnostic(Code, Severity, Message, File, Key?)` the CLI owns, with
`ShaderDiagnostic` and `ThemeFinding` mapped in. The writers take the kind to choose the JSON array name (`backgrounds` or
`themes`) and a rule table for SARIF. A snapshot test of `shader validate` in all four formats, recorded before the
change, proves the old output is byte-equal. Code prefixes `SHADER`, `THEME` (and later `PLUGIN`) are reserved so SARIF
rule ids are unique in a mixed run.

### 6. The sample page moves to a shared project

`src/UrDeck.Preview` (GPL, `UrDeck.Preview.csproj`, `$(UrDeckTfm)`) references the engine and the four first-party widget
projects and holds `SamplePage` (public) and `ThemePreview.Render(LoadedTheme, page, width, height)`, which is today's
`GoldenTests.Render` over `PageRenderer.RenderToBitmap`. The CLI and the engine tests reference it; the engine does not.
The layering rule holds: widgets reference only the SDK, the engine loads plugins and never references them, the preview
project is above both. It is also where the later `PreviewRenderer` (PR 18) belongs.

`GoldenTests.cs` changes its `using` and calls `ThemePreview` for the render; no pixels change, and the goldens prove it.
That one file is the overlap with `page-pattern`. The move is its own first commit so the conflict, if there is one, is
small and mechanical. If `page-pattern` lands first, rebase over its `GoldenTests.cs`; if this lands first, `page-pattern`
takes the changed `using`.

`theme render` draws the page with the theme's cards and colours. The background (`BackgroundPlan`) is not drawn in this
change; the host's `--snapshot` does that on Windows. If wanted, `--background` is an additive option later.

The tool is a .NET tool; the preview project and the widgets are copied into the package with it. The tool package grows
by the four widget assemblies and the engine's themes are already inside. `SkiaSharp.NativeAssets.Linux.NoDependencies` is
already referenced.

### 7. `theme init` and `theme schema`

`theme init <name>` writes a small starter (`$schema`, `format`, `meta`, `colors` from `default-dark`), or with `--from` a
built-in's JSON as shipped plus the font and licence files it names, so the folder is self-contained and `validate`
reports what the original reports. `theme schema` prints the file `docs/schemas/v2/theme.json`, which the tool project
embeds as a linked resource, so the printed schema and the published one are the same file.

### 8. Direction, not built: a repository of themes, backgrounds and plugins

`--all` is the primitive: one level, ordinal order, exit 2 when empty. A later `urdeck-cli check <repo>` for a store or
a marketplace would walk a tree, classify a folder by its marker file (`theme.json` is a theme; `manifest.json` with a
`.sksl` is a background; a plugin manifest is a widget or provider), run each validator and aggregate one report. This
change leaves room for it and no more: sources are an interface (a checkout is one `DirectoryThemeSource` per folder),
the report is not shader-typed, `--all` is not recursive, and code prefixes are reserved. It is not built because a
widget is code, so what "valid" means for a plugin needs a trust policy that is its own exploration, and the shape of a
marketplace is not known.

## Risks / Trade-offs

- **A built-in fails a tool-only check** (contrast is the likely one: Glass is translucent). The spec requires the
  built-ins to check clean and this change does not touch a theme's values. Mitigation: run the checks first (task 5.1);
  if a built-in fails, stop and take it to the owner as a decision (adjust the colour and redraw its goldens with a panel
  look, or relax the rule), do not tune a threshold quietly.
- **The refactor touches 26 call sites on the hot load path of every theme.** Mitigation: the recorded log text test and
  the unchanged goldens; the finding is allocated only when a check fails.
- **The loader unification changes `ThemeStore.LoadUser`.** Mitigation: it is a move, with the existing theme store
  tests unchanged and green before the lint is added.
- **`page-pattern` overlaps `ThemeDefinition.Rules.cs`** (`CheckPattern`). Mitigation: keep the edit to that file to the
  call signature (adding the code); take its version on conflict and add the code afterwards.
- **Contrast under a moving background is a guess.** Stated in the message, and an `info`-level `THEME062` for muted text
  keeps the strict rule to the primary text.
- **The tool package gets larger** by the widgets. Installer size is not a concern here; CPU and memory are, and the tool
  has neither at rest.

## Open Questions

- Does `ThemeValueCheck.Missing` ("needs a member") deserve its own code? Decide while mapping the call sites; if it does
  it is `THEME008`.
- `--background` on `theme render`, drawing the theme's resolved background behind the cards. Not needed to land.
- Whether `theme validate` should resolve the theme at several display scalings to find size-dependent findings (the
  reach is). The first version resolves once at the 1100 pixel panel width.

## Findings during apply

Recorded as they came up; each says what was decided and why, so the next reader does not have to rediscover it.

- **Codes the plan did not list (section 2).** Mapping the ~26 call sites found three findings with no code in the spec:
  - `THEME008` for `ThemeValueCheck.Missing` (a gradient that needs `colors`). It fits none of `THEME003` to `THEME007`
    (`THEME007` is an array that is present and wrong), so the open question is closed with its own code, as the design
    said it would be if a test found it.
  - `THEME055` (warning): a line that resolves under one pixel and is drawn 1 px wide (`card.borderWidth`,
    `card.highlight.thickness`, `card.accentStrip.thickness`, `card.cornerMarks.thickness`).
  - `THEME056` (info): `card.compact.shape` `circle` on a card that is not square is a capsule.
  Both of the last two are logged by the resolver today, so the spec's "every warning the deck logs has a code" needs them.
  The `theme-lint` spec table has the three rows.
- **Severity is the finding's, the log level is the deck's.** Two findings differ between the audiences. `THEME054` (the deck
  limits a reach into the next card) is logged at INFO because the deck has dealt with it, but the spec makes it a
  warning for the author; `THEME020` is an error in the spec, and the deck logs "font could not be loaded" at WARN. The
  finding carries the spec's severity, and the log sink writes `Error` and `Warning` as WARN, and `Info` as INFO, with an
  internal `LoggedAsInfo` flag on the finding for the reach. The log text and level are unchanged, as the snapshot shows.
- **Log lines that do not read `Theme 'name': message`.** Four lines of the log have another shape (`Theme 'x' declares
  format ...`, `Theme 'x' could not be read from ...`, `Font 'x' could not be read: ...`, `The default theme's font ...`).
  A finding's message is always the part after the prefix, and an internal `ThemeLogForm` (`Colon`, `Space`, `Bare`) tells the
  log sink how to join the name, so the log is byte-equal and the tool prints the same message.
- **`THEME090` cannot fire yet.** `ContinuousAnimations()` counts only a spinning or pulsing border, so it is 0 or 1;
  `NoteAnimations` with a count above 1 is unreachable until `motion` adds a second source. The code and the call exist
  and are tested directly; the log snapshot cannot cover the line.
- **The capsule line is not in the log snapshot.** The sample page has no one-cell card that is not square, so no theme
  rendered by the snapshot reaches it. `ThemeFindingTests` checks its code directly.
- **Task 4.9 stopped the run: `THEME044` on Carbon and Paper (resolved by option (b): `"set": "outline"` removed from both; `theme-icons` re-adds it, see `STATUS.md` PR 10).** The lint on the five built-in themes finds nothing in
  `default-dark`, `default-light` and `glass`, and one warning in each of the two themes that came after the plan:
  `icons.set 'outline' is not an icon set that exists`. Carbon and Paper both set `"icons": { "set": "outline" }`, and the
  schema describes `outline` as the built-in set that missing keys fall back to, but no icon set is shipped yet, so the
  empty list of design 4 ("`icons.set` is checked against an empty list") rejects it. There is no contrast finding.
  Nothing was changed; this is the owner's decision (below). The probe that found it was a throwaway test and is not kept.
  - Options: (a) the known icon sets are `outline` until `theme-icons` fills the list (one constant, `THEME044` stays for
    any other name); (b) take `outline` out of Carbon and Paper (changes their JSON, not their pixels); (c) drop `THEME044`
    from this change and let `theme-icons` add it. The recommendation is (a): the schema already names `outline` as built in,
    and neither a theme nor the rule changes.
- **Case-only differences in a key are not unknown keys.** The deck reads keys in any case (`PropertyNameCaseInsensitive`),
  and design 3 says the walker is case-insensitive "as the serializer reads", but the spec's two scenarios used
  `card.borderwidth` as the misspelt key. A key the deck reads is not unknown, so the scenarios now use `card.borderWith`
  (one edit from `card.borderWidth`).
- **A wrong kind in a format 1 value fails the deck's read of the whole theme.** `card.gap: "wide"` is not a skipped value
  with a warning but `Theme 'x' could not be read ... is skipped`, as it always was (the lenient reader only covers the
  format 2 keys). The lint reports it as `THEME003` with the key from the reader's path, as an **error** (the spec table says
  warning, and that holds for format 2 keys), and stops there, because the deck has no theme to check further.
- **`ThemeLint.Check(source, options)` has no `name`.** The source already has the name (the folder's, or the built-in's), so
  a second argument could only disagree with it.
- **Two codes have no lint fixture.** `THEME056` (a circle on a card that is not square) needs a card size the lint does not
  have, and `THEME090` cannot fire yet (above); both are tested through the engine in `ThemeFindingTests`.
- **The lint resolves the theme once, at the 1100 pixel panel width,** through `ThemeResolver.Resolve` and
  `ResolvedCardStyle.Resolve` with a sink of its own, for `THEME023`, `THEME054` and `THEME055`. The resolver's own
  `THEME020` ("font could not be loaded") is dropped there: the lint checks font files itself, and a font named by family
  would otherwise give a different finding on each machine.
- **Task 4.10: the lint on the six fixture themes** (they are drafts in `tests/UrDeck.Engine.Tests/Fixtures/themes`, so findings
  are expected; the theme changes that follow must clear them before their theme ships):

  | Theme | Findings |
  |---|---|
  | Blueprint | `THEME020` font `IBMPlexMono-Regular.ttf` not in the folder; `THEME044` `icons.set` |
  | Ember | `THEME020` font `BarlowCondensed-Bold.ttf` not in the folder; `THEME044` |
  | Obsidian | `THEME020` for `Jost-Variable.ttf` (font, unit, label roles) and `BodoniModa-Variable.ttf` (value role); `THEME044` |
  | Pop | `THEME020` for `Archivo-Variable.ttf` (font, value, unit); `THEME044`; **`THEME061` `colors.cardTints[4]` `#3b5bff` under text `#111111` is 3.7:1** |
  | Prism | `THEME020` font `BricolageGrotesque-Variable.ttf`; `THEME044`; `THEME054` shadow reach 193 px limited to 25 px (the owner accepted this one in `card-chrome-front`) |
  | Rigline | `THEME020` font `ChakraPetch-Bold.ttf`; `THEME044` |

  What each theme change needs: ship the font file and its licence with the theme (all six); drop `icons.set` or wait for
  `theme-icons` (all six set `outline`, the same as Carbon and Paper did); Pop has to lighten or darken tint 4, or change its
  text colour on that tint; Prism's reach is a warning for the author and an information line in the deck, to be kept or fixed.
- **The starter from `theme init` has one `THEME080` warning.** The spec asks for an empty `description` "for the author to
  fill in", and the lint asks for a description, so `init` then `validate` gives exit code 0 (no error) and one warning that
  says what to write. Task 7.4's "clean for the starter" reads as "no error"; `--from` of every built-in is clean, which
  the test checks with warnings counted. Kept as the spec says: the warning is the reminder.
- **`theme render` prints the lint's findings, not only the loader's.** The picture is drawn from the loader's theme, but the
  findings on the error stream are the whole lint (contrast and fonts included), so the author sees the same lines as
  from `validate`; a theme the loader cannot read (`THEME001`, or a wrong kind in a format 1 value) gets no picture and
  exit code 1. The golden comparison runs on Windows only (`[WindowsGlyphTheory]`, as the engine's golden tests).
