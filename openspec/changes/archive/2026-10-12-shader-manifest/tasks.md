Read `proposal.md`, `design.md` and the two delta specs first. Work on the branch `feat/shader-manifest`; one pull
request, title `feat(backgrounds): background folders with a required manifest and a lint`. Run build, format, tests and
`openspec validate --all --strict` before pushing (see `AGENTS.md`). Tasks marked **(owner)** need the owner: prepare
what they need, then stop and ask.

## 1. Spikes

- [x] 1.1 `layout(color)`: with a throwaway test, compile `layout(color) uniform half4 tint;` through `SKRuntimeEffect` (SkiaSharp 4.153.1). Record in the PR whether it compiles, and whether the effect's uniform reports a colour flag. If it does not compile, the parser strips the prefix before compiling (design decision 5)
- [x] 1.2 Embedded folders: confirm the `LogicalName` pattern for `Builtin/<name>/<file>` (MSBuild `RecursiveDir` uses `\` on Windows) and that `GetManifestResourceNames` returns `UrDeck.Backgrounds.<name>/<file>`; settle the exact pattern and write it into the csproj comment
- [x] 1.3 Check what JSON leniency `ConfigStore` allows (comments, trailing commas) and use the same options for the manifest reader

## 2. Engine: manifest and sources

- [x] 2.1 `IBackgroundSource` (`Exists(relative)`, `ReadText(relative)`, `Open(relative)`) with a directory implementation and an embedded-resource implementation. Path rule in one place: a rooted path or one that contains `..` after normalising is refused
- [x] 2.2 `BackgroundManifest` model and `BackgroundManifestReader.Read(text)` returning the manifest and diagnostics, per design decision 2: the three failures (no file / not JSON with an object on top / `manifest` not the integer 1) and a diagnostic for every unknown key, wrong JSON type, unknown `kind` and sample `type`. One key table used for the unknown-key check
- [x] 2.3 `ShaderDiagnostic` (code, severity, message, file), the severity enum and the code constants of the design's table
- [x] 2.4 `ShaderStore.Load(name)` returns a `BackgroundPackage` (name, origin, built-in flag, manifest, entry text, source), user folder first then embedded. Name rule unchanged; a failure returns the reason. The loose-file hint of decision 9. Keep `BuiltInNames`
- [x] 2.5 Tests: each failure and each diagnostic of the reader, entry inside and outside the folder (`..`, rooted, backslashes), user folder over built-in, the loose-file hint, a missing `backgrounds/` folder, a folder with only the shader, file sharing while an editor holds the file

## 3. Engine: declarations and parameters

- [x] 3.1 `ShaderDeclarations`: accept an optional `layout(...)` prefix, record `layout(color)` on `ShaderUniform` (a flag), from spike 1.1. Add tests for `layout(color) uniform half4 x;`, `layout(color)uniform...`, other `layout(...)` contents, and a line that is not a declaration
- [x] 3.2 Join manifest and declarations by name: effective `kind` (manifest wins over `layout(color)`), `hidden`, ranges
- [x] 3.3 Range check in the parameter binding: a configured value outside `min`/`max` (per component) logs one warning naming the parameter and the range and keeps the default. No range, no check. Tests for in range, out of range, a vector, no range, a range on a parameter set by default only
- [x] 3.4 `DeckBackground.RequestKey` includes the manifest text hash; check that an unrelated config save still does not change the key, and that editing the manifest does

## 4. Engine: lint

- [x] 4.1 `ShaderLint.Check(IBackgroundSource)` (public) producing every diagnostic of the design's table, reusing the reader, `ShaderDeclarations` and a compile-only path. `ShaderProgram` stays internal
- [x] 4.2 Load-time reporting in `DeckBackground.Create`: log each diagnostic once as `Background 'name': SHADERnnn message (path)`; stop the background only for the failures of the spec (001 to 003, 010, 011, 020, 021)
- [x] 4.3 Tests: one per code with a minimal folder that triggers exactly it; a clean folder returns nothing; hidden parameters are exempt from 031 and 042; the log has one line per diagnostic per key change and none on an unrelated reload
- [x] 4.4 Built-ins test: `plasma`, `aurora`, `gradient` and `julia` return no diagnostics of any severity

## 5. Built-ins as folders

- [x] 5.1 Move `Backgrounds/Builtin/*.sksl` to `Backgrounds/Builtin/<name>/<name>.sksl`; update the embedded-resource item in `UrDeck.Engine.csproj` with the pattern from spike 1.2, and the comment above it
- [x] 5.2 Write the four `manifest.json` files: title, description, every parameter with label, description, and a sensible `min`/`max`/`step` (aurora `intensity`, gradient `angle`, plasma `angle` and `zoom`, julia `zoom` and `radius`); `$schema` pointing at `https://urdeck.app/schemas/v1/shader-manifest.json`; license `GPL-3.0-or-later`, author the owner
- [x] 5.3 `BackgroundRender.Render(...)` (design decision 8) with `DeckBackground.TryCreate`; tests for the size, the theme colour behind, determinism on one machine, a failing shader returning the reason
- [x] 5.4 A small test-project helper that renders the four built-ins at 270 x 480 into `Backgrounds/Builtin/<name>/sample.png`, run by hand and described in `docs/backgrounds.md`; add the `samples` entries and embed the pictures. Record the added size of the assembly in the PR
- [x] 5.5 **(owner)** Look at the four samples and the live deck with each built-in selected; confirm nothing changed visually from before the conversion

## 6. Schema

- [x] 6.1 `docs/schemas/v1/shader-manifest.json`: JSON Schema 2020-12 for version 1 with `additionalProperties` open, enums for `kind` and sample `type`, `required: ["manifest"]`
- [x] 6.2 Consistency test: the schema's property names, enums and `required` match the reader's key tables; the four built-in manifests parse as JSON and declare the schema URL

## 7. Documentation

- [x] 7.1 `docs/backgrounds.md`: replace "Writing your own shader" with "Authoring backgrounds": the folder layout, a copy-paste minimal manifest, the manifest reference table, the lint codes (the design's table, with how to fix each), parameters with `layout(color)`, porting from Shadertoy, regenerating the built-in samples. Update the "Built-in shaders" table text to say the manifests are the source and the table is checked later
- [x] 7.2 Update the parts of the document that say `backgrounds/<name>.sksl`, the "Failures" section (a missing manifest, a loose file) and the snapshot paragraph if affected
- [x] 7.3 `AGENTS.md`: the background paragraph (folder, manifest, `urdeck.log` lines `SHADERnnn`); `README.md` if it names the file layout; `docs/BACKLOG.md` item status
- [x] 7.4 `scripts/Install-Local.ps1`: confirm it never touches `backgrounds/` including folders (read the script; add a test run against a temp folder if it has none)

## 8. Verify and close

- [x] 8.1 `dotnet build urdeck.slnx -c Release` with 0 warnings; `dotnet test urdeck.slnx -c Release`; the engine coverage run; `dotnet format urdeck.slnx --severity warn`; `openspec validate --all --strict`
- [x] 8.2 **(owner)** On the panel: select a built-in, then a copied user folder with a manifest, then a loose `.sksl` (expect the hint and the theme colour), then a folder without a manifest (expect the failure), then fix it and save the config; check `urdeck.log` shows each `SHADERnnn` line once
- [x] 8.3 Tick the tasks above only as verified; archive the change when all are done
