## 1. The manifest key

- [x] 1.1 `BackgroundManifest` and its reader: optional `pairsWith.themes` (array of strings), `PairsWith` on the manifest; a wrong type and an unknown key inside it are lint warnings; no `SHADER004` for `pairsWith`
- [x] 1.2 `docs/schemas/v1/shader-manifest.json`: add `pairsWith` (additive, `additionalProperties` unchanged); the existing schema-versus-reader test stays green
- [x] 1.3 Tests: read, wrong type, unknown inner key, no diagnostic for a valid `pairsWith`, order preserved

## 2. Settings and the plan

- [x] 2.1 `BackgroundSettings`: absent and `null` parse to "no choice"; `{"type":"none"}` parses to an explicit none; `IsExplicit`; page backgrounds still noted and ignored
- [x] 2.2 `BackgroundPlan.Resolve(settings, theme)`: ordered candidates (configuration, theme default, theme still, colour), with the origin of each; the theme's default and still background are not candidates when the user chose a shader, an image or `none`
- [x] 2.3 Plan table test: every combination of user (absent, shader, image, none, invalid) and theme (nothing, default, still, both)
- [x] 2.4 `DeckBackground.RequestKey` carries the origin and, for a theme's choice, the theme name and palette; a theme change that alters the resolved background changes the key, an unrelated save does not
- [x] 2.5 Failure passes to the next candidate; each failure is one warning; a failed candidate is not retried until its key changes
- [x] 2.6 Log line once per change: `Background: shader 'horizon' (theme default)` and the same for configuration, still background and colour

## 3. Still backgrounds and the scrim

- [x] 3.1 Gradient (linear by angle, radial by centre and radius) and colour as still backgrounds in the engine (`PaintAtTime` path) and in the host's still canvas
- [x] 3.2 Picture: path inside the theme folder (escape rejected with one warning), `BackgroundImage.Prepare`, cover; user themes only (built-ins are embedded and none needs one)
- [x] 3.3 Scrim: drawn last over a shader, a picture and a still background, in the swap chain panel, the still canvas and the snapshot; skipped for the plain colour and `none`; the colour's alpha multiplies the scrim alpha
- [x] 3.4 Snapshots: `--snapshot` draws the theme's default, still background and scrim exactly as the host does; a golden test for a radial gradient plus scrim (Linux and Windows, within the existing image tolerance)
- [x] 3.5 `motion.level` has no effect on the background (a test with `off` and a moving default)
- [x] 3.6 Host: `BackgroundLayer` takes the plan; a theme switch with a different default restarts it and one with the same does not; look at `urdeck.log` on the panel for the origin line and for no `Background frames started` on a theme with only a still background

## 4. Pairing and catalogs

- [x] 4.1 `ThemeCatalog.List` (built-in and user themes, `meta`, swatch colours, default background name; no fonts; a user theme replaces a built-in of the same name; unreadable entries skipped with a warning)
- [x] 4.2 `BackgroundCatalog.List` (built-in and user, manifest values, no compile)
- [x] 4.3 `PairingIndex` (listed, mutual, exact names) and `Recommend(theme, backgrounds, limit)` with the five rungs and reasons
- [x] 4.4 A test for each rung, for the limit, for a name that does not exist, and for determinism (same input, same list, twice)
- [x] 4.5 `meta` (name, description, mode) for `default-dark`, `default-light` and `glass`; snapshots of the three before and after are byte-identical

## 5. Documentation

- [x] 5.1 `docs/backgrounds.md`: "Theme backgrounds" (precedence, `none`, the effect of removing `background`), the scrim, `pairsWith` in the manifest reference
- [x] 5.2 `docs/themes.md`: the `background` and `meta.pairsWith` keys
- [x] 5.3 `site/src/content/docs/`: the background and theme pages say the same (AGENTS.md: a change to a theme or a background updates both); the `none` opt-out is in the first paragraph of the background page
- [x] 5.4 `AGENTS.md`: the background paragraph (theme choice, plan, scrim, where the log line is)

## 6. Verification

- [x] 6.1 `dotnet build urdeck.slnx -c Release` with 0 warnings; `dotnet test urdeck.slnx -c Release`; the Engine coverage floor; `dotnet format urdeck.slnx --severity warn`; `openspec validate --all --strict`
- [x] 6.2 `urdeck-cli shader validate --all src/UrDeck.Engine/Backgrounds/Builtin --fail-on-warnings` still clean
- [x] 6.3 On the panel with a user theme that sets a default shader, a still gradient and a scrim: the theme's default shows with no `background` in the configuration; `none` switches it off; a different shader replaces it; deleting the key brings it back; a broken shader name shows the gradient; the frame lines in the log read about 30 frames per second only while a shader runs
