## Why

A background folder can be checked today only by selecting it in a running deck and reading `urdeck.log`, and a sample
picture can be made only by an explicit test in the engine's test project. A shader author has no command to run, and a
future community store has nothing to put in its CI. The lint and the renderer are already in `UrDeck.Engine` behind a
public facade (`shader-manifest`), and since #31 the engine builds and is tested on Linux, so a small command line tool
on top of them is now cheap.

This is change (b1) of `docs/handoff/2026-10-12-urdeck-cli.md`: the commands that only read a background. The commands
that write a manifest are the next change.

## What Changes

- A new command line tool, `urdeck-cli`, as its own project in this repository. It is a portable dotnet tool
  (`net10.0`, packable with `dotnet pack`), runs on Windows and Linux, and uses only the engine's public API.
- `urdeck-cli shader validate <folder>` checks one background folder with the engine's lint and prints its findings.
  `--all <directory>` checks every folder in a directory. It never renders and never uses the JSON Schema: the lint is
  the one judge, the same one the host runs when it loads a background.
- `--format text|markdown|json|sarif` chooses how the findings are printed (`text` by default). `json` is a small
  versioned shape of our own; `sarif` is SARIF 2.1.0 for code scanning. Findings name a file, not a line.
- `--fail-on-warnings` makes a warning fail the run.
- Exit codes are fixed and documented: `0` nothing that fails the run, `1` findings, `2` the tool could not do its job
  (bad arguments, a folder that does not exist).
- `urdeck-cli shader render <folder> -o <file.png>` draws one frame on the CPU: `--size`, `--time`, `--theme`,
  `--colors`, `--param name=value`. It renders the folder it is given, never a built-in of the same name.
- A picture in which the shader drew nothing is reported as a warning with a new code, `SHADER070`. It is a warning
  because a shader can be legitimately blank at one instant.
- The lint gains `SHADER012`: the folder's name is not a valid background name. The host can never select such a
  folder, so only the tool reports it.
- The explicit `BuiltInSampleTool` test is removed; the built-in `sample.png` files are regenerated with the tool.
- CI runs the tool on the four built-ins on Windows and on Linux, and packs it. CI never compares rendered pictures.
- `docs/backgrounds.md` documents the tool, the formats, the exit codes, the SARIF caveats, and that running the GPL
  tool on a shader does not affect the shader's licence.

Not in this change, each later: `shader init` and `render --update-manifest` (the manifest writer; it will refuse a
rewrite that would lose comments or trailing commas), `shader docs` and its drift check, publishing to NuGet.org and
standalone binaries, `widget render`, line numbers on diagnostics, suppressing a code, user themes for `render`.

## Capabilities

### New Capabilities
- `urdeck-cli`: the command line tool: its commands and options, the output formats, the exit codes, and what it
  guarantees across platforms.

### Modified Capabilities
- `background-manifest`: "Background Lint" gains the folder-name check; "Rendering A Background" gains rendering a
  given folder directly and the blank-picture warning.

## Impact

- New `tools/UrDeck.Cli` (GPL-3.0-or-later, it links the engine) and `tests/UrDeck.Cli.Tests`, both in `urdeck.slnx`.
- New package dependency: `DotMake.CommandLine`. `SkiaSharp.NativeAssets.Linux.NoDependencies` (already in
  `Directory.Packages.props` for the tests) becomes a dependency of the tool.
- `src/UrDeck.Engine/Backgrounds`: a public way to read a background from a folder path, a `BackgroundRender` overload
  that takes that background, the blank check, two new codes. No change to what the host shows or logs.
- `tests/UrDeck.Engine.Tests/BuiltInSampleTool.cs` is deleted.
- `.github/workflows/ci.yml`: the tool's tests and a smoke run in both jobs, `dotnet pack` of the tool.
- `docs/backgrounds.md`, `AGENTS.md` (layout, commands), `README.md` (licence map), `docs/BACKLOG.md`,
  `docs/handoff/2026-10-12-urdeck-cli.md` (what this change decided differently).
- No SDK change, no configuration change, nothing a user of the deck sees.
