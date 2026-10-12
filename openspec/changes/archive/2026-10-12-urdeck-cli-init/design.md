## Context

See `proposal.md` for the motivation and scope. What exists after `urdeck-cli` (#32):

- `tools/UrDeck.Cli`: command classes in `Commands.cs` that only carry options, and a runner for each
  (`ValidateRunner`, `RenderRunner`) taking its output and error writers. A `ToolFailureException` becomes exit code 2
  with the reason on the error stream. `RenderRunner` already resolves the folder and the output path, and builds the
  configuration JSON from `--colors` and `--param`.
- The tool uses only the engine's public API and is not in its `InternalsVisibleTo`.
- `BackgroundManifestReader` reads the manifest by hand from a `JsonDocument`, accepting comments and trailing commas as
  the configuration does. It checks that a sample's `settings` is an object and then drops it: `ManifestSample` is
  `(Type, File, Order, Caption)`.
- `ShaderDeclarations.Parse` gives each uniform its name, component count, default as floats, and whether the
  declaration says `layout(color)`. A default written as `#rrggbb` is turned into floats; that it was a colour is lost.
- `BackgroundParameter.Join(declarations, manifest)` lists the parameters, without the host's inputs and keys.
- The lint treats an empty or blank `description` as missing (`SHADER041`, `SHADER042`) and a manifest with no samples
  as `SHADER050`, all warnings. So a stub with empty descriptions is valid and lists its own to-do.
- The engine never writes a manifest.

## Goals / Non-Goals

**Goals:**
- `init` then `validate` is a working loop from the first minute: the folder loads, and the warnings are the to-do.
- A manifest is never damaged: `init` does not overwrite, and `--update-manifest` either keeps everything or refuses.
- The engine stays the only place that parses a shader or reads a manifest; the tool only writes.

**Non-Goals:**
- Merging into an existing manifest beyond the `samples` entry (`init --update`).
- Editing JSON text in place to keep comments.
- Reading a sample's `settings` back to render it again.
- Guessing ranges, or anything about a parameter the shader does not say.

## Decisions

### 1. The writer lives in the tool

A `ManifestWriter` in `tools/UrDeck.Cli` builds and edits the manifest as a `JsonNode` tree and writes it indented with
two spaces, LF line endings, a final newline and text left readable (the relaxed JSON escaping, so `é` is not written
as `é`). The engine gains no writing code: the host never writes a manifest, and the settings editor writes the
configuration, not manifests.

*Alternative: a writer in the engine next to the reader.* Rejected: code under the coverage floor that only the tool
calls, and a second public surface to keep stable. If an editor ever writes manifests, it moves then.

### 2. `init` builds the stub from the engine's join

`init` reads the shader's text, calls `ShaderDeclarations.Parse` and `BackgroundParameter.Join(declarations, null)`,
and writes one entry for each parameter in that order. Host inputs and keys are already left out by the join; the
declarations' `Unsupported` names are printed as a note. The shader is not compiled, so a half-ported shader still
gets its manifest and `validate` reports the compile error next.

Key order in the stub: `$schema`, `manifest`, `title`, `description`, `parameters`; in a parameter: `label`,
`description`, `kind`. The `$schema` value is the one constant the built-in manifests use; a test compares it with
theirs.

### 3. The colour guess

`kind` `color` is written for a parameter with three or four components when any of these holds: the declaration is
`layout(color)`, the default was written as a colour (`#...`), or the name contains `color`, `colour` or `tint`
(ignoring case). For `layout(color)` the kind is already implied; writing it makes the manifest say what an editor
will do.

The second signal needs the engine: `ShaderUniform` gains `DefaultIsColor`, set by `ShaderDeclarations` when the
default text starts with `#`. Nothing else reads it.

A wrong guess costs one deleted line and never a diagnostic: the guess is only made where `color` is allowed, so
`SHADER033` cannot come from a stub.

*Alternative: guess from the default's values (all in 0 to 1).* Rejected: `0, 0, 1` is as likely a direction.

### 4. Titles and labels from names

One function: split on `_`, `-` and lower-to-upper steps, join with spaces, lower-case all but the first letter
(`bandWidth` and `band_width` both give `Band width`). An author is expected to rewrite them; the point is that the
key exists.

### 5. What `init` accepts and refuses

| Argument | Result |
|---|---|
| `x.sksl`, no folder `x` | creates `x/`, moves the file in, writes the manifest |
| `x.sksl`, folder `x` exists without `x.sksl` and without a manifest | moves the file in, writes the manifest |
| `x.sksl`, folder `x` holds `x.sksl` or a manifest | refuses |
| folder `x` holding `x.sksl`, no manifest | writes the manifest |
| folder `x` with a manifest, or without `x.sksl` | refuses |
| a name that is not a valid background name | refuses |

Every refusal is a `ToolFailureException` (exit code 2) raised before anything is created. The file is moved, not
copied, as the handoff says: a loose `.sksl` next to its folder is exactly what stopped loading. The manifest is
written first to a temporary file in the folder and renamed, and the shader is moved last, so a failure half-way
leaves the loose file where it was.

`--output <directory>` chooses where the folder is created (it must exist). There are no options for title, author or
licence: the stub is a text file the author opens next anyway.

### 6. `--update-manifest`: check, render, then write

Order in `RenderRunner`, so a refusal leaves nothing behind:

1. Resolve the output path and require it to be inside the folder (compare full paths; the relative path with `/` is
   the sample's `file`).
2. Read `manifest.json` and decide whether it can be rewritten (decision 7). If not, fail with exit code 2 and the
   entry to paste. A manifest that is missing or not JSON is left for the render to report as it does today.
3. Render and write the picture as now. No picture, no update.
4. Find the `samples` item whose `file` equals the relative path (after normalising `\` to `/`); create
   `{ "type": "image", "file": ... }` at the end if there is none; set its `settings`; write the manifest through a
   temporary file and a rename.

`settings` is `{ "size": "270x480", "time": 10, "theme": "default" }`, plus `colors` (the array given) and
`parameters` (an object of the `--param` values as parsed) when they were given. `size`, `time` and `theme` are always
written, defaults included, so a later change of a default does not change what the record means.

A `samples` that exists and is not an array is refused like a lossy rewrite: the tool does not repair a manifest.

### 7. What "would lose something" means

The manifest is parsed twice: leniently, as the reader does, and strictly (no comments, no trailing commas). If only
the lenient parse succeeds, the rewrite is refused and the message says which of the two was found. A key that occurs
twice in one object is refused too (the tree cannot hold it). Everything else survives a `JsonNode` round trip: unknown
keys, key order, array order, and numbers as written.

Indentation and blank lines are not kept; the file is rewritten in the tool's one style. That is stated in the
documentation and is not treated as loss. The built-in manifests are already in that style, and a test requires that
reading and writing each of them without a change gives the same bytes, which pins the style.

*Alternative: splice the `samples` array into the original text.* Rejected: it needs a tolerant tokenizer with
positions, for a case the refusal handles honestly.
*Alternative: rewrite and warn.* Rejected with the owner: silent loss of an author's comments.

### 8. The reader keeps `settings`

`ManifestSample` gains `Settings` (a `JsonElement?`, cloned so it outlives the document). The reader still only checks
that it is an object. This is for change (c), which prints how a sample was made, and it makes the spec's "a reader
gets `settings` as written" true. The schema file describes the five keys and stays open; the schema consistency test
is extended to the keys of `settings` only if the reader gets a table for them, which it does not need.

### 9. The built-ins use it

The four built-in samples are rendered again with `--update-manifest`, which adds `settings` to their manifests. The
pictures must not change (same size, time and theme as before); the manifests gain three lines each. The built-ins
must still lint clean.

## Risks / Trade-offs

- [A moved shader surprises someone who expected a copy] → the output names both paths; the handoff decided the move;
  the temporary-file order means a failure never loses the file.
- [The colour guess is wrong for a `float3` named `tintOffset`] → one line to delete, no diagnostic; documented as a guess.
- [Refusing on a comment annoys an author who comments manifests] → the message prints the exact entry to paste, so
  the cost is one paste; the hand-written manifest stays theirs.
- [A rewrite changes formatting in a hand-formatted manifest] → documented; the content is kept, and the round-trip
  test fixes the style so later versions do not reformat again.
- [`JsonNode` writes a number differently from how it was written] → a test round-trips `1.0`, `1e3` and a long
  decimal; if any changes, the writer keeps the raw text for values it did not touch.
- [`DefaultIsColor` is a public addition for one caller] → one boolean with a default, tested in the engine.

## Migration Plan

1. Engine additions (`Settings`, `DefaultIsColor`), then the writer and the two commands, then the built-in manifests
   and docs, in one pull request.
2. Rollback is reverting the pull request. A manifest with `settings` on its samples was already valid before this
   change, so nothing written by the tool breaks an older host.
