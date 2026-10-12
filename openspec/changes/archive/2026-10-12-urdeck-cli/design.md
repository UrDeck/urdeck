## Context

See `proposal.md` for the motivation and scope. What exists:

- `ShaderLint.Check(IBackgroundSource, name)` is public and needs no window, theme or size. `DirectoryBackgroundSource`
  is public, so a folder at any path can be linted today.
- `BackgroundRender.Render(settings, theme, ShaderStore, width, height, time)` is public, but finds the background by
  name through a `ShaderStore(backgroundsDirectory)`, which falls back to the embedded built-in of that name.
  `ShaderStore.Read`, which turns a source into a `BackgroundPackage`, is private.
- `DeckBackground.PaintAtTime` fills the size it is given. `scale` is only used by the host to size the element, so it
  has no effect on a raster render (checked in the exploration).
- `BackgroundSettings.Parse(JsonElement?)` is the one place that understands `colors` and parameter values.
- `ThemeStore(themesDirectory).Load(name)` serves the embedded built-in themes without a directory.
- Since #31 the engine and its tests build and run on Linux; `SkiaSharp.NativeAssets.Linux.NoDependencies` is in
  `Directory.Packages.props`. Shader compilation and CPU rendering pass there. Text is not pixel-equal across platforms,
  which does not matter here: a shader draws no text.
- `BuiltInSampleTool` (an explicit test) renders the four built-in `sample.png` files at 270 x 480, default theme.
- The owner uses DotMake.CommandLine for command line tools.

## Goals / Non-Goals

**Goals:**
- A tool thin enough that it cannot disagree with the host: every finding comes from the engine.
- The same behaviour on Windows and Linux, proven by tests and a smoke run on both in CI.
- Output and exit codes a store's CI can depend on for years.

**Non-Goals:**
- Anything that writes a manifest (`init`, `--update-manifest`), generated documentation, publishing a package or
  binaries, user themes for `render`, line numbers, suppressing codes.
- A second validator. No JSON Schema library and no embedded schema.
- Moving the tool to its own repository. It can move once the engine is a versioned package.

## Decisions

### 1. Where the tool lives and what it may touch

`tools/UrDeck.Cli` (assembly and tool command `urdeck-cli`, `PackAsTool`, `net10.0`, GPL-3.0-or-later) and
`tests/UrDeck.Cli.Tests`, both in `urdeck.slnx`. It references `UrDeck.Engine` as a project and is **not** added to the
engine's `InternalsVisibleTo`: what the tool needs must be public, which is also what keeps a later move to its own
repository possible. Its version is the engine's: "validated with urdeck-cli X" has to mean "by the rules host X applies".

*Alternative: commands on `UrDeck.Host.exe`.* Rejected in the handoff: a `WinExe` has no console and needs Windows.
*Alternative: its own repository now.* Rejected: it would need the engine published with a stable API first, and this
change alters that API.

### 2. The lint is the only judge

`validate` calls `ShaderLint.Check` and prints what it returns. It adds no checks of its own and does not validate
against `docs/schemas/v1/shader-manifest.json`: the hand-written reader already reports unknown keys, wrong types and
unknown enum values, and a test keeps the schema equal to the reader. The handoff's "the tool embeds its own copy of the
schema" is dropped; the lint is already offline.

The one check a tool can meet and the host cannot is the folder's name, so it goes into the lint as `SHADER012`
(error), first in `Check`, without stopping the rest. The host never calls the lint with an invalid name
(`ShaderStore` refuses it earlier), so nothing changes there.

### 3. A background read from a path

A public static entry on the store, `ShaderStore.TryRead(string folder)` returning the existing `BackgroundLoad`, exposes
what the private `Read` does for a `DirectoryBackgroundSource`, with the folder's leaf as the name. `BackgroundRender`
gains an overload `Render(BackgroundPackage, BackgroundSettings, LoadedTheme, width, height, time)`, and
`DeckBackground` an internal creation from a package that the by-name path also uses, so there is still one way to build
a shader background.

*Alternative: `new ShaderStore(parentOfFolder)` and the leaf as the name.* Rejected: a mistyped path whose leaf is
`plasma` renders the built-in and exits 0, and a folder at a path root has no usable parent.

The tool checks that the folder exists before calling the engine, so "no such folder" is exit code 2 and not a lint
finding about a missing manifest.

### 4. The blank check belongs to the render

`BackgroundRender` compares the finished bitmap with the colour it cleared to; when every pixel is equal it adds a
`SHADER070` warning to the result's diagnostics. A zero alpha leaves the cleared colour. (A NaN does not: Skia's CPU path draws it opaque white, which is not blank, so it is not reported.) The lint stays free of rendering (it runs on every load in the host and has no theme or size), and `validate`
never renders.

*Alternative: a lint check that renders a frame.* Rejected with the owner: cost at every host load, and the result
would depend on a theme, a size and a time.

`render` prints the reason when there is no picture, and `SHADER070`; it does not repeat the folder's other warnings.

### 5. No `--scale`

`scale` does not reach the raster path, so the option would do nothing. `--size` is the size rendered, default 270x480
(the built-in samples' size). The handoff's "default to `--scale 0.5`" is dropped. The settings object the tool builds
carries `scale` 1 for clarity.

### 6. Options become configuration JSON

`--colors` and each `--param name=value` are assembled into the JSON object the configuration would hold
(`{ "type": "shader", "name": <folder name>, "colors": [...], "<name>": <value> }`) and parsed with
`BackgroundSettings.Parse`. A value is taken as a JSON number or array when it parses as one, otherwise as a string (so
`--param tint=#ff8800` and `--param offset=[0.5,0.2]` both work). The engine then refuses and reports what the
configuration would refuse; the tool has no parameter rules of its own. A `--param` naming a host key (`speed`, `scale`,
...) is a usage error.

Whether the engine reports a refused value in a way the tool can show (it logs through `UrDeckLog` today) is checked in
task 1.3; the tool must not create an `urdeck.log`.

### 7. Themes

`--theme` takes a built-in theme name and the tool loads it with a `ThemeStore` that has no user directory. An unknown
name must be exit code 2, so the tool needs the list of built-in names; task 1.3 checks whether `ThemeStore` exposes it
and adds a public list if not (as `ShaderStore.BuiltInNames`).

### 8. Formats

One internal model (backgrounds, each with diagnostics) and four writers. Paths are the folder as given, joined with the
diagnostic's file, with `/`. `json` and `sarif` are written with `Utf8JsonWriter`, indented; no SARIF package.

- `json`: `{ "version": 1, "tool": { "name", "version" }, "backgrounds": [ { "name", "folder", "diagnostics":
  [ { "code", "severity", "message", "file" } ] } ], "summary": { "errors", "warnings", "infos" } }`.
- `sarif`: `version` `2.1.0`, one run, `tool.driver` with `name`, `version`, `informationUri` and a `rules` entry for
  each code that occurs; each result has `ruleId`, `level` (`info` becomes `note`), `message.text` and one location with
  `artifactLocation.uri`. No `region`, since there are no lines.

*Alternative: a GitHub workflow-command format.* Left out with the owner: SARIF covers annotations, and a store that
cannot upload SARIF (pull requests from forks get a read-only token) can turn `json` into annotations with one `jq`
line. The documentation says so.

### 9. Exit codes and the parser

`0`, `1`, `2` as in the spec. System.CommandLine, under DotMake, returns `1` for a parse error by default, which would
collide with "findings". The entry point parses first (`Cli.Parse`), prints the errors and returns `2` when there are
any, and only then runs. Task 1.2 confirms this with the current DotMake version and settles the exact shape.

A command is a class that takes its output and error writers, and the entry point is `CliApp.Run(args, stdout, stderr)`
returning the code, so the tests call it in-process and read the captured text. Unexpected exceptions are caught at the
entry point: message to the error stream, exit code `2`.

### 10. `--all`

Every directory directly inside the given one is a background, in ordinal name order; hidden entries and files are
ignored. A folder without a manifest is a finding (`SHADER001`), not skipped: a store's directory holds only
backgrounds, and skipping would hide a broken submission. No folders at all is exit code `2`.

### 11. Replacing `BuiltInSampleTool`

The test is deleted. `docs/backgrounds.md` "Regenerating the built-in samples" becomes four `urdeck-cli shader render
src/UrDeck.Engine/Backgrounds/Builtin/<name> -o .../sample.png` lines. The pictures are regenerated once with the tool
and committed only if they differ from the current ones (same code path, so they should not).

### 12. CI

- The Windows `build` job already builds and tests the solution, so the tool's tests run there once the projects are in
  `urdeck.slnx`. It also runs `dotnet pack` on the tool.
- `test-linux` adds `tests/UrDeck.Cli.Tests`.
- Both jobs run the built tool as a smoke test: `shader validate --all src/UrDeck.Engine/Backgrounds/Builtin
  --fail-on-warnings`, and `shader render ... --fail-on-warnings` for each built-in into a temp folder. The exit code
  covers "not blank"; a step checks that the files exist. No picture is compared with a stored one.

## Risks / Trade-offs

- [DotMake's parse-error handling differs from what its documentation shows] → spike 1.2 first; the fallback is to
  inspect the parse result ourselves, which the library supports.
- [The packed tool lacks the Linux native library] → the tool references `SkiaSharp.NativeAssets.Linux.NoDependencies`
  directly; a task installs the packed tool from a local folder in a Linux container and renders with it.
- [The engine logs to a file when the tool runs] → task 1.3 checks `UrDeckLog`'s default; the tool runs with logging
  off or to nowhere.
- [`SHADER070` false alarm on a shader that is blank at time 10] → it is a warning, and `--time` picks another instant.
- [A solid-colour shader equal to the theme's background colour is called blank] → accepted; it is indistinguishable
  from nothing, and it is a warning.
- [Output formats become a contract too early] → `json` has a `version`; the spec only promises that keys are added,
  never renamed.
- [Diagnostics without lines make SARIF annotations land on the file] → documented; an optional line on
  `ShaderDiagnostic` is a later change.
- [Two public additions to the engine only the tool uses] → both are small, tested in the engine's test project (the
  coverage floor applies), and the settings editor will want "read a folder" too.

## Migration Plan

1. Engine additions first (they are invisible to the host), then the tool, then CI and docs, in one pull request.
2. Rollback is reverting the pull request. No configuration, manifest or stored data changes shape; the two new codes
   are additions.

## Open Questions

- The package id on NuGet.org (`urdeck-cli` or `UrDeck.Cli`) and how the tool's version is set at release. Decided when
  publishing is; the tool command name `urdeck-cli` is fixed here.
