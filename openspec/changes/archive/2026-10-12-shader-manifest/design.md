## Context

See `proposal.md` for the motivation and scope. What the code does today:

- `ShaderStore(backgroundsDirectory).Load(name)` returns a `ShaderText` (name, text, origin, built-in flag): the file
  `backgrounds/<name>.sksl` if there is one, else the embedded `UrDeck.Backgrounds.<name>.sksl`. Nothing is cached.
- `ShaderDeclarations.Parse` reads uniforms with one regex that needs a line to start with `uniform`, and takes a default
  from a `// default:` comment. `ShaderProgram.Create(shader, settings, palette)` compiles and binds; it is `internal`.
- `DeckBackground.Create` and `RequestKey` are the only callers that turn a name into a background; the host, the
  snapshot and the tests all go through them. The key hashes the shader's text, so editing the file restarts the
  background and an unrelated config save does not.
- The four built-ins are embedded as `Backgrounds\Builtin\*.sksl` with `LogicalName="UrDeck.Backgrounds.%(Filename)%(Extension)"`.
- Themes already set the precedent for folders with a fixed file name (`themes/<name>/theme.json`) and for one reader
  that serves both embedded and user content.
- `UrDeck.Engine` targets plain `net10.0` and references SkiaSharp only. It carries the coverage floor.
- The widget and provider projects never see backgrounds, so there is no SDK change and no license boundary to cross.

## Goals / Non-Goals

**Goals:**
- One load path for built-in and user backgrounds, so the built-ins are the living examples and the lint test covers the
  real thing.
- One reader and one lint that the host, the unit tests and the later command line tool share, so they cannot disagree.
- A manifest that can grow (a store, a settings form) without breaking a manifest written today.

**Non-Goals:**
- The command line tool, generated documentation and the drift check, and any settings page (see the proposal).
- Playing a `video` sample, or any other use of the sample files by the host.
- Migrating loose shader files automatically. The hint names what to do.
- A new package dependency, in particular a JSON Schema validator.

## Decisions

### 1. A background is a `BackgroundPackage`, found by `ShaderStore`

`ShaderStore.Load(name)` returns a package: name, origin, whether it is built in, the manifest, the entry's text and a
`IBackgroundSource` that reads a relative file or says whether it exists. The user folder wins over the embedded one, as
the file does today. Two sources implement the interface: a directory and the embedded resources.

The lint and the loader both take the source, so "does `sample.png` exist" and "does `entry` stay inside the folder" are
answered by the same code for a folder on disk and for a built-in. A path is resolved against the source and refused
when it is rooted or contains `..` after normalising; that check lives in the source, not in each caller.

*Alternative: let the lint take a directory path only.* Rejected: the built-in test would need the embedded files copied
out, and it would test a different path from the one that runs.

### 2. The manifest is read by hand, not deserialised into a type

The reader walks a `JsonDocument` and builds the manifest model, collecting a diagnostic for every key it does not know,
every value of the wrong JSON type and every unknown `kind` or sample `type`, and carrying on with what it could read.

*Alternative: `JsonSerializer.Deserialize<Manifest>`.* Rejected: a wrong type throws or silently drops the value, and
the spec requires a warning for each such case and a background that still loads. The hand-written reader is about
a hundred lines and the one place that knows the key list; the schema file is checked against it (decision 7).

Only three things fail the load: no file, not valid JSON with an object on top, and a `manifest` that is not the
integer 1. Everything else is a diagnostic. The reader accepts the same JSON leniency as the configuration reader
(comments, trailing commas) so a hand edit behaves the same in both files; tasks check what that is.

### 3. Lint is a function from a package to diagnostics

`ShaderLint.Check(source)` returns `IReadOnlyList<ShaderDiagnostic>` (code, severity, message, file). It reuses the
manifest reader's diagnostics, `ShaderDeclarations`, and a compile through the same code `ShaderProgram` uses (a
compile-only entry point, so it needs no palette or size). It is public with `ShaderDiagnostic` and the severity enum:
that is the facade the command line tool will call, so `ShaderProgram` stays `internal`.

The codes are fixed here so the tool, the docs and the tests agree. A code never changes meaning; a retired one is not
reused.

| Code | Severity | Meaning |
|---|---|---|
| SHADER001 | error | `manifest.json` is missing |
| SHADER002 | error | the manifest is not valid JSON, or its top level is not an object |
| SHADER003 | error | `manifest` is missing, not an integer, or a version this product does not read |
| SHADER004 | warning | unknown key in the manifest, a parameter or a sample |
| SHADER005 | warning | a value has the wrong JSON type |
| SHADER010 | error | the entry file does not exist |
| SHADER011 | error | `entry` is rooted or leaves the folder |
| SHADER020 | error | the shader does not compile (message included) |
| SHADER021 | error | the shader declares a `uniform shader` |
| SHADER022 | warning | a uniform has a type that cannot be set; it stays 0 |
| SHADER023 | warning | a `// default:` cannot be read |
| SHADER030 | warning | the manifest annotates a parameter the shader does not declare |
| SHADER031 | warning | the shader declares a parameter the manifest does not annotate (hidden ones are exempt) |
| SHADER032 | error | the manifest annotates a name that belongs to the product (`speed`, `iTime`, ...) |
| SHADER033 | warning | `kind` `color` on a parameter without three or four components |
| SHADER034 | warning | unknown `kind` |
| SHADER035 | warning | `min` is above `max`, or the shader's default is outside the range |
| SHADER040 | warning | no `title` |
| SHADER041 | warning | no `description` |
| SHADER042 | warning | a parameter that is not hidden has no `description` |
| SHADER050 | warning | no `samples` |
| SHADER051 | error | a sample file does not exist |
| SHADER052 | error | a sample `file` is rooted or leaves the folder |
| SHADER053 | warning | unknown sample `type` |
| SHADER060 | info | `layout(color)` and the manifest's `kind` disagree; the manifest wins |

The "Manifest Required" failures (001 to 003, 011, 020 and 021) are errors that also stop the background. 010 does as
well, since there is nothing to run. The others are errors in the lint's sense (the folder is broken and a store would
reject it) but the background still runs, as the spec says.

### 4. Where load-time lint runs and how often

`DeckBackground.Create` runs the lint once and logs each diagnostic as `Background 'name': SHADERnnn message (path)`.
The host calls `Create` only when `RequestKey` changes, and the key now includes a hash of the manifest text, so editing
either file re-lints and an unrelated config save does not repeat the lines. A background that failed is not retried
until its key changes, which is the existing rule.

`RequestKey` must not compile, so it reads the package without linting (`warn: false`, as it does now).

### 5. Parameters: the shader decides, the manifest annotates

`ShaderDeclarations` stays the only place that knows what uniforms there are. The manifest model is joined to it by name
when something needs both (the lint, the range check, a future editor). The binding code gets the manifest's ranges and
applies "Parameter Ranges" next to the existing wrong-form check, so a bad value is handled in one place.

*Alternative: let the manifest declare types and defaults.* Rejected: it makes two sources to keep equal, and a shader
with no manifest would need a different path. The shader file must stay self-sufficient to run.

`layout(...)`: the declaration regex takes an optional `layout(<anything but ')'>)` before `uniform` and records whether
it contains `color`. **This starts with a spike** (task 1.1): whether Skia accepts `layout(color)` in the SkSL that
SkiaSharp compiles, and whether it reports the uniform as a colour. If it rejects it, the parser strips the prefix
before compiling and keeps the flag itself; either way the observable rule in the spec holds.

### 6. Built-ins are folders, with samples

Each built-in is `Backgrounds/Builtin/<name>/{manifest.json,<name>.sksl,sample.png}` embedded with logical names
`UrDeck.Backgrounds.<name>/<file>`, so the embedded source resolves a relative file by name. A built-in must lint clean
(spec), and `SHADER050` and `SHADER051` need a real sample, so each built-in embeds a small PNG (270 x 480, rendered
by `BackgroundRender` at the snapshot time in the default theme). Until the command line tool exists the pictures are
made by a small test-project helper that is run by hand and documented next to the guide; a later change replaces it
with `urdeck-cli shader render`.

*Alternative: exempt built-ins from the sample rules.* Rejected: it hides the rule where it is most visible, and the
pictures are what the settings picker will show.

### 7. The schema file is documentation for editors, checked against the reader

`docs/schemas/v1/shader-manifest.json` is a JSON Schema (2020-12) with `additionalProperties` left open so a newer key
does not light up red in an editor. The engine does not read it (no validator dependency). A unit test reads it as JSON
and compares its property names, enums and `required` with the reader's key tables, so the two cannot drift. When
`urdeck.app` serves the file the `$schema` URL starts working; until then the repository copy is the source.

### 8. `BackgroundRender`

`BackgroundRender.Render(BackgroundSettings, LoadedTheme, ShaderStore, width, height, time)` creates the background,
prepares it, clears a raster bitmap to the theme's background colour and calls `PaintAtTime`. It returns a result with
the bitmap or the failure reason. `DeckBackground` gains an internal `TryCreate(..., out failure)`; `Create` calls it and
logs the failure, so the log text and the returned reason are the same string.

### 9. The loose-file hint

When a name finds no folder, the store checks whether `<name>.sksl` exists in the backgrounds directory and, if so,
returns the specific message ("`backgrounds/waves.sksl` is a loose file; a background is now a folder: create
`backgrounds/waves/` with a `manifest.json` and move the file in as `waves.sksl`"). It never loads the file.

## Risks / Trade-offs

- [`layout(color)` does not behave as thought] → spike first (task 1.1); fall back to stripping the prefix; the spec text
  describes behaviour only.
- [Requiring a manifest turns away someone pasting a shader from the web] → the failure message says exactly which two
  files to make, the authoring guide has a copy-paste manifest, and the later `shader init` writes one from the shader.
  The cost of the rule is one small file; the gain is that every background in the wild has the same shape.
- [Embedded sample pictures grow the assembly] → 270 x 480 PNGs of smooth colour are small; measure and note the size in
  the PR. If `julia` is large, lower its resolution or quantise.
- [Lint at load logs a lot for a sparse manifest] → one line per diagnostic, once per key change; a user who does not
  care can ignore them, and all but the failures are warnings.
- [A hand-written reader can miss a new key] → the schema/reader consistency test, and the reader's key table is one
  list used for both the unknown-key warning and the test.
- [`RequestKey` now reads two files] → both are small and are read only when the configuration reloads, as now.
- [Breaking: loose files stop loading] → the only known user (the owner) has no `backgrounds/` folder; the hint covers
  anyone else.

## Migration Plan

1. Convert the four built-ins to folders with manifests and samples; nothing else in the product changes for a user
   who selects a built-in.
2. Ship the loader and lint together, so a loose file gets its hint in the same version that stops loading it.
3. Rollback is reverting the pull request; no configuration key or stored data changes shape.

## Open Questions

- Whether the settings window should show lint diagnostics for the selected background once it has a backgrounds page.
  Not needed to build this; the diagnostics are a plain list a page can show.
