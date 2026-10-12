## Why

A shader background is one loose `.sksl` file. What it is called, what its parameters do and what range makes sense
exist only in `docs/backgrounds.md`, written by hand for the four built-ins and nowhere for a user's own. Nothing can
tell a shader author that a parameter is misnamed, and a settings editor (milestone 3) has no way to build a form for a
shader. Backgrounds also need a unit a person can install, share and delete without hunting for files, which a future
community store will need too.

Making a background a folder with a required manifest gives all of that one shape: a plugin format for backgrounds that
a person, a linter and a UI can all read.

## What Changes

- **BREAKING** A background is a folder, `backgrounds/<name>/`, holding `manifest.json` and the entry `.sksl`
  (`<name>.sksl` unless the manifest names another with `entry`). The folder name is the background's name in the
  configuration. A loose `backgrounds/<name>.sksl` is no longer loaded; the log names the folder layout it needs.
- **BREAKING** A background without a readable `manifest.json` of a supported version fails to load: one warning, the
  deck shows the theme's background colour. This is the only thing the manifest is required for; every other problem in
  it is a lint diagnostic that does not stop the background.
- The manifest (version 1) describes the background: `title`, `description`, `entry`, `author`, `license`, `version`,
  `tags`, per-parameter annotations (`label`, `description`, `kind` of `number` or `color`, `min`, `max`, `step`,
  `unit`, `hidden`) and `samples` (image files, with a `video` type reserved and inert). The shader stays the source of
  truth for which parameters exist, their types and their defaults; the manifest only annotates them. Unknown keys,
  kinds and sample types warn and never fail, so the format can grow.
- The four built-in shaders become embedded folders with manifests and load through the same path as a user's.
- A `ShaderLint` in `UrDeck.Engine` checks a background folder and returns diagnostics with stable `SHADER###` codes
  (error, warning, info). The host runs it when a background loads and logs the diagnostics; a unit test requires the
  built-ins to produce none.
- A `layout(...)` prefix on a uniform is accepted, and `layout(color)` marks a parameter as a colour, so shaders written
  for Skia or Flutter bring their parameters with them.
- A parameter with `min` or `max` whose configured value is outside the range logs a warning and uses the default, as
  `speed` and `scale` do.
- A public `BackgroundRender` renders a background to a bitmap at a size and time, for the later command line tool.
- The JSON Schema of the manifest is in the repository, `docs/schemas/v1/shader-manifest.json`, and a manifest points at
  `https://urdeck.app/schemas/v1/shader-manifest.json` for editor completion.
- `docs/backgrounds.md` gets an "Authoring backgrounds" guide: the folder, the manifest reference, the lint codes and
  porting from Shadertoy.

Not in this change, each its own change later: the `urdeck-cli` dotnet tool (`shader validate`, `init`, `render`),
the generated documentation and its drift check in CI, and a settings page that reads manifests.

## Capabilities

### New Capabilities
- `background-manifest`: the folder layout of a background, the version 1 manifest and what it may contain, the
  required-or-fails rule, the lint and its diagnostic codes, and rendering a background to a bitmap.

### Modified Capabilities
- `backgrounds`: "Shader Backgrounds" (a user shader is a folder, not a loose file) and "Shader Parameters" (the
  description file sentence goes; `layout(...)` prefixes, `layout(color)` and manifest ranges are added).

## Impact

- `src/UrDeck.Engine/Backgrounds`: `ShaderStore` loads folders (user folder first, then embedded); a new manifest model
  and reader, `ShaderLint`, `BackgroundRender`; `ShaderDeclarations` accepts `layout(...)`; `ShaderProgram` applies
  ranges. Built-ins move from `Builtin/*.sksl` to `Builtin/<name>/` with manifests and are embedded with a new
  `LogicalName` pattern.
- `docs/backgrounds.md`, `docs/schemas/v1/shader-manifest.json`, `AGENTS.md` (the background paragraph),
  `scripts/Install-Local.ps1` (it never touches `backgrounds/`; confirm folders are covered the same way).
- Tests in `tests/UrDeck.Engine.Tests`; the engine coverage floor still applies.
- A user's own loose shader stops working until it is moved into a folder with a manifest. The owner's installed copy
  has no `backgrounds/` folder today, so nothing there breaks.
- No new package dependency. No SDK change: widgets never see backgrounds.
