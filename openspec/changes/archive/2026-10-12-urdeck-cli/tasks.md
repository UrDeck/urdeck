Read `proposal.md`, `design.md` and the two delta specs first. Work on the branch `feat/urdeck-cli`; one pull request,
title `feat(cli): urdeck-cli with shader validate and shader render`. Run build, format, tests and
`openspec validate --all --strict` before pushing (see `AGENTS.md`), and the tests on Linux as well (Docker,
`mcr.microsoft.com/dotnet/sdk` at the SDK version of `global.json`; do not share `bin/` and `obj/` with the Windows
build). Tasks marked **(owner)** need the owner: prepare what they need, then stop and ask.

## 1. Spikes

- [x] 1.1 DotMake.CommandLine: add the current version to `Directory.Packages.props` (the NuGet audit must stay clean) and, in a throwaway project, build `shader validate` / `shader render` as nested commands with an argument, a repeatable option and an enum option. Record in the PR that it builds with 0 warnings under the repository's analyzers and runs on Linux
- [x] 1.2 Exit code of a parse error: confirm what `Cli.Run` returns for an unknown option and a missing argument, and settle the entry point that returns `2` for them (design decision 9: parse first, then run). Confirm `--help` and `--version` return `0`
- [x] 1.3 Read `UrDeckLog`, `ThemeStore` and `ShaderProgram`'s parameter binding and record: whether the engine writes a log file unless told to (the tool must create no `urdeck.log`), whether the built-in theme names are public, and how a refused parameter value is reported (logged only, or returned). Write down the smallest public additions the tool needs; they become part of group 2

## 2. Engine

- [x] 2.1 `ShaderCodes.FolderName` = `SHADER012` (error) and the check at the start of `ShaderLint.Check`, without stopping the other checks. Tests: an invalid name gives exactly one `SHADER012` and the folder's other findings; a valid name gives none; the built-ins stay clean
- [x] 2.2 `ShaderStore.TryRead(string folder)` (public, static) returning a `BackgroundLoad` for the folder at that path, name from the folder's leaf, never falling back to a built-in. Tests: a folder outside any `backgrounds` directory, a folder named `plasma` holding another shader, a path with a trailing separator, a missing manifest
- [x] 2.3 `BackgroundRender.Render(BackgroundPackage, BackgroundSettings, LoadedTheme, width, height, time)` and the internal creation from a package in `DeckBackground` that the by-name path also uses (design decision 3). Tests: the folder named `plasma` renders its own shader, not the built-in; the existing render tests still pass
- [x] 2.4 `ShaderCodes.BlankRender` = `SHADER070` (warning), added by `BackgroundRender` when every pixel equals the cleared colour. Tests: a shader returning a transparent colour, one producing NaN, each built-in (no diagnostic), and that the lint never returns the code
- [x] 2.5 Path traversal on every platform: tests that an `entry` and a sample `file` written with `\`, with `/` and mixed are refused by the lint (`SHADER011`, `SHADER052`), running in both CI jobs. Fix `BackgroundPaths.Normalize` only if a case fails
- [x] 2.6 The additions found in 1.3 (built-in theme names, a quiet log, a refused parameter value the caller can see), each with a test
- [x] 2.7 `docs/schemas` and the schema consistency test are untouched; confirm the engine coverage floor still passes

## 3. The tool project

- [x] 3.1 `tools/UrDeck.Cli/UrDeck.Cli.csproj`: `Exe`, `$(UrDeckTfm)`, assembly name and `ToolCommandName` `urdeck-cli`, `PackAsTool`, references `UrDeck.Engine`, `DotMake.CommandLine` and `SkiaSharp.NativeAssets.Linux.NoDependencies`. Not in the engine's `InternalsVisibleTo`. SPDX GPL headers. Add it and `tests/UrDeck.Cli.Tests` to `urdeck.slnx`
- [x] 3.2 The entry point of design decision 9: `CliApp.Run(args, stdout, stderr)` returning the exit code, parse errors and unexpected exceptions as `2` on the error stream, the exit codes in the help text of the tool and of each command
- [x] 3.3 Tests for the entry point: unknown command, unknown option, missing argument (`2`, output stream empty), `--help` (`0`, names the exit codes), `--version` (`0`)

## 4. `shader validate`

- [x] 4.1 The command for one folder: resolve the path, exit `2` when it is not a directory, lint through `DirectoryBackgroundSource`, build the result model (design decision 8), compute the exit code with and without `--fail-on-warnings`
- [x] 4.2 `--all <directory>` (design decision 10): folders in ordinal name order, the worst exit code, `2` for no folders, `2` when given together with a folder argument
- [x] 4.3 The `text` writer and the summary line; the `markdown` writer
- [x] 4.4 The `json` writer (the shape of design decision 8, `version` 1) and the `sarif` writer (2.1.0, rules for the codes that occur, `info` as `note`, no region)
- [x] 4.5 Tests, one per scenario of the spec: clean, warnings only, `--fail-on-warnings`, an error, a missing folder, `.` as the folder, a folder with only a `.sksl`, `--all` with one bad among good, `--all` on an empty directory, the built-ins directory with `--fail-on-warnings`; that the findings equal `ShaderLint.Check`'s for the same folder
- [x] 4.6 Format tests: the exact `text` line of the spec's scenario; `markdown` has a heading and a table per background; `json` and `sarif` parse, hold the expected counts, levels and rule ids, and are the whole output stream; paths use `/` on Windows; a clean run in each format

## 5. `shader render`

- [x] 5.1 The command: `<folder>`, `-o`, `--size` (default 270x480), `--time` (default the snapshot time), `--theme`, `--colors`, `--param` (repeatable), `--fail-on-warnings`. Options become configuration JSON and go through `BackgroundSettings.Parse` (design decision 6); `scale` is fixed at 1 and there is no `--scale`
- [x] 5.2 Read the folder with `ShaderStore.TryRead`, render with the new overload, encode as PNG and write the file; no file when there is no picture; print the reason or `SHADER070`
- [x] 5.3 Exit codes: `2` for a missing folder, a malformed `--size` or `--param`, a `--param` naming a host key, an unknown theme, an output that cannot be written; `1` for no picture, and for a blank one with `--fail-on-warnings`
- [x] 5.4 Tests, one per scenario of the spec: size of the written PNG, `--param` changes the picture against the default, `--colors` and `--theme` change it, a shader that does not compile (no file, `1`), blank (`0`, and `1` with the flag, file written), a non-existent path whose leaf is `plasma` (`2`), an unknown theme (`2`), an existing output file is replaced, a refused parameter value is reported as the configuration reports it. No test compares a picture with a stored file
- [x] 5.5 Delete `tests/UrDeck.Engine.Tests/BuiltInSampleTool.cs`. Regenerate the four built-in `sample.png` files with the tool and compare with the committed ones; commit them only if they differ, and say so in the PR

## 6. CI and packaging

- [x] 6.1 `.github/workflows/ci.yml`, `test-linux`: add `tests/UrDeck.Cli.Tests`
- [x] 6.2 Both jobs: the smoke run of design decision 12 with the built tool (`validate --all` on the built-ins with `--fail-on-warnings`; `render --fail-on-warnings` of each built-in into a temp folder; a step that checks the four files exist)
- [x] 6.3 `dotnet pack tools/UrDeck.Cli -c Release` in the Windows job. Locally, once: install the package from the output folder with `dotnet tool install --tool-path` in a Linux container and on Windows, run `validate` and `render` with the installed tool, and record in the PR that the Linux native library is in the package
- [x] 6.4 Confirm `dotnet format urdeck.slnx --verify-no-changes --severity warn` covers the new projects

## 7. Documentation

- [x] 7.1 `docs/backgrounds.md`: a section "The command line tool" (how to build and run it from the repository until it is published, both commands with every option, the four formats with a short sample of each, the exit code table, the author workflow `render` then `validate --fail-on-warnings`); `SHADER012` and `SHADER070` in the lint code table with how to fix each; "Regenerating the built-in samples" rewritten for the tool
- [x] 7.2 In the same section: using it in CI (the `sarif` upload needs `security-events: write`, a pull request from a fork gets a read-only token, `json` plus `jq` as the alternative; findings name a file, not a line), and that running the GPL tool on a shader does not affect the shader's licence
- [x] 7.3 `AGENTS.md` (layout: `tools/`, the tool's commands under "Commands"), `README.md` (the licence map and a line on the tool), `CONTRIBUTING.md` if it lists the projects, `docs/BACKLOG.md` (status of (b), now (b1) and (b2))
- [x] 7.4 `docs/handoff/2026-10-12-urdeck-cli.md`: a short note at the top saying which points this change decided differently (no embedded schema, the blank check in the render, no `--scale`, `text|markdown|json|sarif`, exit code `2`, (b) split into this change and the manifest-writing one) and pointing at this change

## 8. Verify and close

- [x] 8.1 `dotnet build urdeck.slnx -c Release` with 0 warnings; `dotnet test urdeck.slnx -c Release`; the engine coverage run; `dotnet format urdeck.slnx --severity warn`; `openspec validate --all --strict`; the engine and tool tests on Linux with 0 failed
- [x] 8.2 **(owner)** Run the tool by hand: `validate` on a built-in folder and on a folder with mistakes in each format, `render` of a built-in and a look at the picture; confirm the output reads well
- [x] 8.3 Tick the tasks above only as verified; archive the change when all are done
