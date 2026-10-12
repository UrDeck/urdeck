## Why

A background needs a `manifest.json` to load, and today an author writes it by hand: every parameter named again, the
`samples` entry typed after rendering the picture. Someone who pastes a shader from the web meets the "manifest is
missing" failure first. `urdeck-cli` can already check and render a folder (`urdeck-cli`, #32); the two steps that write
the manifest are what is left of the author's workflow.

This is change (b2) of `docs/handoff/2026-10-12-urdeck-cli.md`.

## What Changes

- `urdeck-cli shader init <shader.sksl | folder>` makes a background folder from a shader. Given a loose file it creates
  the folder next to it and moves the file in; given a folder that holds the shader and no manifest it works in place.
  It writes a `manifest.json` with the `$schema` line, `"manifest": 1`, a title from the name, an empty description, and
  one entry for each parameter the shader declares: a label from the name, an empty description, and `kind` `color`
  where the shader suggests a colour.
- The stub is valid and loads at once; what is left to fill in shows as warnings, so `shader validate` right after
  `init` is the to-do list. `init` never overwrites a manifest.
- `urdeck-cli shader render ... --update-manifest` adds the picture it wrote to the manifest's `samples`, or updates the
  entry that already names that file, and records in the entry's `settings` how the picture was made (size, time,
  theme, colours, parameters).
- `--update-manifest` refuses, before anything is written, when rewriting the manifest would lose something: a
  comment, a trailing comma, a duplicate key. It prints the entry to paste by hand instead. A rewrite keeps every key,
  value and their order.
- The manifest's `samples[].settings` gets a defined meaning (how a sample was made) and is kept when the manifest is
  read; the host still never uses it.
- The four built-in manifests get `settings` on their samples, written by the tool.
- `docs/backgrounds.md` documents both, and the author's workflow becomes `init`, fill in the descriptions,
  `render --update-manifest`, `validate --fail-on-warnings`.

Not in this change: `init --update` (adding new parameters to an existing manifest), porting a Shadertoy source,
guessing `min`, `max` or `step`, rendering again from a sample's `settings`, `shader docs` and its drift check (change
(c)), publishing the tool.

## Capabilities

### New Capabilities

None.

### Modified Capabilities
- `urdeck-cli`: a new requirement for `shader init`; "Rendering A Background To A File" gains `--update-manifest`.
- `background-manifest`: "Manifest Content" defines what a sample's `settings` holds.

## Impact

- `tools/UrDeck.Cli`: a new command, a manifest writer, the stub builder; `tests/UrDeck.Cli.Tests`.
- `src/UrDeck.Engine/Backgrounds`: `ManifestSample` keeps `settings`; `ShaderUniform` says whether its default was
  written as a colour (for the `kind` guess). No change to what the host shows or logs.
- `src/UrDeck.Engine/Backgrounds/Builtin/*/manifest.json`: `settings` on each sample.
- `docs/schemas/v1/shader-manifest.json` (the keys of `settings`, still an open object), `docs/backgrounds.md`,
  `docs/BACKLOG.md`, `docs/handoff/2026-10-12-urdeck-cli.md`.
- CI: the smoke run gains an `init` on a copy of a built-in shader.
- No new package dependency, no SDK change, no configuration change.
