# Handoff: the `urdeck-cli` tool and generated background documentation

**For:** a fresh session that writes the proposals for changes (b) and (c) once `shader-manifest` (a) is merged
(`/opsx:propose`, or `/opsx:explore` first). **Date:** 2026-10-12.
**Read first:** `openspec/specs/background-manifest/spec.md` (exists after (a) is archived), `docs/backgrounds.md`
("Authoring backgrounds"), and the design of `shader-manifest` for the diagnostic codes and the public lint facade.

Nothing here is built. It is what the exploration decided, so the proposals do not start from zero.

> **Update 2026-10-12 (change (b1), `openspec/changes/urdeck-cli`).** The tool is built, in two parts: this change has the
> commands that only read a background (`shader validate`, `shader render`); `shader init` and `render --update-manifest` are
> change (b2). Where it decided differently from the text below:
>
> - **No embedded schema.** `validate` calls `ShaderLint.Check` only; the hand-written reader already reports unknown keys, wrong
>   types and unknown enum values, and a test keeps the schema equal to it.
> - **The blank-picture check is in the render, not the lint** (`SHADER070`, a warning, added by `BackgroundRender` when every pixel is
>   still the theme's background colour). A NaN is not blank: Skia's CPU path draws it white.
> - **No `--scale`.** It never reached the raster path; `--size` is the size rendered (default 270x480).
> - **Formats `text|markdown|json|sarif`** instead of `json` with SARIF later; no GitHub workflow-command format (`json` plus `jq`).
> - **Exit code `2`** for "the tool could not do its job", so a parse error does not collide with "findings" (`1`).
> - **`SHADER012`**: a folder whose name is not a valid background name, reported by the lint.
> - The rest of this file (init, `--update-manifest`, the manifest writer, change (c)) is still to do.

## Where this comes from

The owner wanted every shader background documented in a machine-readable way, for a future configuration UI and for a
future community store that validates submissions in CI. The exploration ended in a background that is a folder with a
required `manifest.json` (change (a), `shader-manifest`), a lint in the engine with stable `SHADER###` codes, and a
command line tool on top of it. The owner's earlier exploration notes are not in the repository; this file is the record.

## Change (b): `urdeck-cli`

**A separate portable dotnet tool**, not part of the host. The owner uses DotMake.CommandLine for command line tools; use it.

- **Why not the host:** `UrDeck.Host` is a `WinExe`, so it has no console and printing from it needs `AttachConsole`; it is
  a self-contained WinUI app, so CI would need a Windows runner and the full build. `UrDeck.Engine` targets plain `net10.0`
  and references SkiaSharp only, so a console tool can reference it and run on Linux. Wrapping the host's `Program.Main`
  dispatch with DotMake was considered and left out: no use case yet, and a dependency in a WinUI app.
- **The tool calls the engine's public facade** (`ShaderLint.Check`, `BackgroundRender`, the manifest reader), so the host's
  load-time warnings, the unit tests and the CLI can never disagree. `ShaderProgram` stays internal.
- **First task: a spike.** SkiaSharp needs its native library on Linux (`SkiaSharp.NativeAssets.Linux`; `Directory.Packages.props`
  has none). Compile a shader and render one frame on a Linux runner. If it is awkward, a `windows-latest` job (what CI
  uses today) is the fallback; it is a convenience question, not a blocker.
- **Licence:** the tool links the GPL engine. Fine for a CLI; say in the docs that running it on a shader does not affect
  the shader's licence.

Commands (names are `shader <verb>` so `widget render` stays free; widget screenshots for documentation are a real later
door, since `PageRenderer.RenderToBitmap` is already in the engine and only `HostContext` is host-bound):

| Command | Does |
|---|---|
| `shader validate <folder>` | the lint; exit 0 clean, 1 on errors; `--fail-on-warnings` makes warnings fail too; `--format json` (SARIF later) so a store can annotate pull requests with the codes. Suppression is not built in v1, but the codes make it possible |
| `shader init <sksl>` | creates the folder, moves the shader in, writes a `manifest.json` with every parameter stubbed: `label` from the name, empty `description`, `kind: color` guessed for 3/4-component uniforms with a colour default or a colour-like name (and `layout(color)`), host inputs skipped, the `$schema` line. The stubs are valid but lint as warnings, so `validate` right after `init` lists what is left to fill in. Refuses to overwrite a manifest. An `--update` mode that adds new parameters without touching text is out of v1 |
| `shader render <folder>` | CPU render to a PNG: `--size`, `--time` (default the snapshot time, 10), `--theme` or `--colors`, `--param name=value`, `-o`. `--update-manifest` adds or updates the `samples` entry and records `settings` so the picture is reproducible. A blank-render check (every pixel the background colour, or NaN) as a lint warning |

- The raster render already works headless: `--snapshot` "is drawn on the CPU and does not need the GPU", and `DeckBackground.PaintAtTime`
  with `BackgroundClock.SnapshotTime` is in the engine. Heavy shaders (`julia`) are slow on the CPU at large sizes; default
  to `--scale 0.5` as the host does.
- **CI must not diff rendered PNGs.** CPU output is not guaranteed bit-identical across machines. Check that the file exists
  and is not blank; treat the picture as an artifact, not a golden file.
- The tool embeds its own copy of the schema, so validation works offline; the `$schema` URL is only for editors.
- The schema is hosted later at `https://urdeck.app/schemas/v1/shader-manifest.json` (the owner owns `urdeck.app`; a
  documentation site on GitHub Pages is planned). Name schema files for what they describe, not `schema.json`. Prefer a
  release-pinned URL once there are releases.
- Author workflow this enables: `init` -> fill in descriptions -> `render --update-manifest` -> `validate --fail-on-warnings`.

## Change (c): generated documentation and the drift check

Split from (b) on purpose, so (b) delivers real features and a template for later tools.

- `shader docs <folder|--all>` emits a Markdown blurb from the manifest and the parsed shader.
- **Solve the sync problem in CI, not with a hook.** A pre-commit hook is local and opt-in (a web-UI commit or a contributor
  without it bypasses it) and one that rewrites files during a commit is surprising. CI runs `shader docs --all` into a temp
  location and diffs against the committed text; a difference fails with "run `urdeck-cli shader docs --all` and commit".
  Same model as `dotnet format --verify-no-changes`. A pre-commit hook that calls the same command is an optional extra.
- `docs/backgrounds.md` mixes prose with the built-in table. Use a marked region (`<!-- urdeck:shaders:begin -->` ...
  `<!-- urdeck:shaders:end -->`), rewritten only between the markers, and compared only there, rather than splitting the
  file.
- After (a) the built-ins table in `docs/backgrounds.md` is still written by hand; (c) is what makes the manifests its source.

## Decisions already made (do not reopen without a reason)

- A manifest is required to load a background; every other manifest problem is a diagnostic. Loose `.sksl` files no longer load.
- The shader is the source of truth for which parameters exist, their types and defaults; the manifest annotates.
- `kind` is `number` or `color` in version 1. A separate `vector` kind is reserved for later: the default for a
  multi-component uniform already is "N numbers", and `color` is the only hint the shader cannot give.
- `hidden` hides from an editor only; the configuration can still set it.
- Store fields (`author`, `license`, `version`, `tags`) and `unit` are in version 1 as optional.
- `samples` is an array of `{ type, file, order, caption, settings }`; `video` is accepted and inert.
- The manifest has an integer version; the schema path carries the same number (`/schemas/v1/`).

## Not decided

- Whether `--format json` becomes SARIF, and the diagnostic JSON shape (decide in (b)).
- Whether the settings window shows lint diagnostics for the selected background (editor milestone).
- Whether `shader init` can also accept a Shadertoy source and port it (an idea, not scoped).
- How a community store addresses, signs or versions packages. Out of scope until the store is.
