Read `proposal.md`, `design.md` and the two delta specs first. Work on the branch `feat/urdeck-cli-init`; one pull
request, title `feat(cli): shader init and render --update-manifest`. Run build, format, tests and
`openspec validate --all --strict` before pushing (see `AGENTS.md`), and the engine and tool tests on Linux as well.
Tasks marked **(owner)** need the owner: prepare what they need, then stop and ask.

## 1. Engine

- [x] 1.1 `ManifestSample.Settings` (`JsonElement?`, cloned) filled by `BackgroundManifestReader`; a `settings` that is not an object stays the `SHADER005` warning and gives null. Tests: the three values of the spec's scenario are read back; an unknown key inside `settings` gives no diagnostic; the element is usable after the reader returns
- [x] 1.2 `ShaderUniform.DefaultIsColor`, set by `ShaderDeclarations` when the `// default:` text starts with `#`. Tests: `#ff8800` on a `float3` and a `float4`, a numeric default, no default, a `#` default on a `float` (still `SHADER023`, flag false)
- [x] 1.3 `docs/schemas/v1/shader-manifest.json`: describe `size`, `time`, `theme`, `colors` and `parameters` under a sample's `settings`, leaving it open. The schema consistency test still passes; the engine coverage floor still passes

## 2. The manifest writer

- [x] 2.1 `ManifestWriter` in `tools/UrDeck.Cli` (design decision 1): a `JsonNode` tree to text with two-space indentation, LF, a final newline and relaxed escaping; writing through a temporary file in the folder and a rename
- [x] 2.2 The check of design decision 7: lenient and strict parse, the duplicate-key check, and a result that says what would be lost (a comment, a trailing comma, a duplicate key and its name)
- [x] 2.3 Tests: each built-in manifest read and written without a change gives the same bytes; unknown keys, key order and array order survive; `1.0`, `1e3` and a long decimal are written as they were read (if not, keep raw text for untouched values); `é` stays `é`; a comment, a trailing comma and a duplicate key are each detected and named

## 3. `shader init`

- [x] 3.1 The name function of design decision 4 with tests (`zoom`, `bandWidth`, `band_width`, `band-width`, `iColorMix`, a single letter, digits)
- [x] 3.2 The stub builder: `ShaderDeclarations.Parse`, `BackgroundParameter.Join(declarations, null)`, the key order of design decision 2, the colour guess of decision 3. The `$schema` value is one constant; a test compares it with the built-in manifests'
- [x] 3.3 `InitCommand` under `shader` (argument `path`, option `--output`) and `InitRunner`: the table of design decision 5, every refusal before anything is created, the manifest written before the shader is moved, the output naming the folder, the files, any uniform that cannot be set, and `urdeck-cli shader validate <folder>` as the next command. Exit codes in the help as for the other commands
- [x] 3.4 Tests, one per scenario of the spec: a loose shader (folder, moved file, manifest content), validate after init (no errors; `SHADER041`, `SHADER042`, `SHADER050` and nothing else from the manifest), a folder with only the shader, a colour by default, by `layout(color)` and by name, a vector that is not a colour, host inputs skipped, an unsupported uniform named in the output, a manifest exists, the folder exists, a name that is not valid, a shader that does not compile, `--output` to another directory and to one that does not exist
- [x] 3.5 A test that a stub made from each built-in shader lints with no error and no `SHADER031`, `SHADER033` or `SHADER034`

## 4. `render --update-manifest`

- [x] 4.1 The option on `RenderCommand`, and in `RenderRunner` the order of design decision 6: the output inside the folder, the rewrite check, then the render as now, then the update. The refusal prints the entry to paste on the error stream and writes nothing
- [x] 4.2 Building the entry: find by `file` (with `\` normalised), keep the other keys, replace `settings` with `size`, `time`, `theme` and, when given, `colors` and `parameters`; append a new `{ "type": "image", "file": ... }` otherwise. A `samples` that is not an array is refused
- [x] 4.3 Tests, one per scenario of the spec: first sample (and `validate` no longer reports `SHADER050`), rendered again with a `caption` kept and `parameters` recorded, the rest of the manifest kept, a manifest with a comment and one with a trailing comma (no picture, no manifest change, exit `2`, the entry printed), the output outside the folder, no picture then no update, a blank picture recorded, a sample in a subfolder (`samples/a.png` with `/` on Windows), a missing manifest (the render's own failure, exit `1`)
- [x] 4.4 The help text of `render` and the exit code text mention the option; `CliAppTests` for `init --help`

## 5. The built-ins

- [x] 5.1 Render the four built-in samples again with `--update-manifest` (270x480, the default theme, the snapshot time). The pictures must be byte-identical to the committed ones; commit the manifests. Say in the PR if a picture changed
- [x] 5.2 The built-ins still lint clean, and the round-trip test of 2.3 passes on the new manifests

## 6. CI and documentation

- [x] 6.1 `.github/workflows/ci.yml`, the smoke run in both jobs: copy a built-in `.sksl` to a temp folder, `shader init` it, `shader render ... --update-manifest`, then `shader validate` and check the exit code is `0` (warnings only)
- [x] 6.2 `docs/backgrounds.md`: `shader init` and `--update-manifest` in "The command line tool" (what the stub holds, that the colour is a guess, that the file is moved, when an update is refused and why, that a rewrite normalises formatting); "Writing a background" as the workflow `init`, fill in, `render --update-manifest`, `validate --fail-on-warnings`; `settings` in the manifest reference; "The minimal manifest" and "When something is wrong" (a missing manifest, a loose file) point at `init`; "Regenerating the built-in samples" uses `--update-manifest`
- [x] 6.3 `AGENTS.md` (the tool's commands), `docs/BACKLOG.md` (status of (b2), (c) next), `docs/handoff/2026-10-12-urdeck-cli.md` (a note: (b2) done, what it decided: refuse instead of losing comments, the colour guess, the meaning of `settings`, `init` accepting a folder)

## 7. Verify and close

- [x] 7.1 `dotnet build urdeck.slnx -c Release` with 0 warnings; `dotnet test urdeck.slnx -c Release`; the engine coverage run; `dotnet format urdeck.slnx --severity warn`; `openspec validate --all --strict`; the engine and tool tests on Linux with 0 failed
- [x] 7.2 **(owner)** Take a shader from the web, run `init`, read the stub, fill it in, `render --update-manifest`, `validate --fail-on-warnings`, copy the folder into `backgrounds/` and select it on the deck; confirm the workflow and the messages read well
- [x] 7.3 Tick the tasks above only as verified; archive the change when all are done
