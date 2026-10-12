Read `proposal.md`, `design.md` and `docs/perf/backgrounds-spike.md` first. Work on the branch `feat/backgrounds`
(it already holds this change and the spike note); one pull request, title `feat(backgrounds): shader and image
backgrounds behind the cards`. The spike's code is on the local branch `spike/backgrounds`
(`spike/RenderHostSpike/Backgrounds.cs`): the shader layer, the render-scale trick and the pacing there are the
reference for group 6. Tasks marked **(owner)** need the owner at the panel: prepare what they need, then stop and ask.

## 1. Engine: configuration

- [x] 1.1 Add `UrDeckConfig.Background` (`JsonElement?`, `[JsonPropertyName("background")]`) in `Config/ConfigStore.cs`. Leave `PageConfig.Background` as it is. Check that a config without the key serialises without it (as `display` and `providers` do) so existing files are not rewritten differently
- [x] 1.2 New folder `src/UrDeck.Engine/Backgrounds/`. `BackgroundSettings`: an immutable result of parsing the element (kind `None`/`Shader`/`Image`, `Name`, `Path`, `Speed`, `Scale`, `Colors`, and the remaining keys as parameters), with `Parse(JsonElement?)` applying every rule of design decision 2: not an object, unknown `type`, missing `name`/`path`, `speed` outside 0..10, `scale` outside 0.1..1, `colors` not an array / with an invalid colour / longer than four, extra keys on an image. Each problem logs one `UrDeckLog.Warn` that starts with `Background:`; none throws
- [x] 1.3 Name validation: letters, digits, `-` and `_` only; anything else is a warning and kind `None`, decided before any file access
- [x] 1.4 When any page has a `background` value, log one info line per load: only the top-level `background` is supported
- [x] 1.5 Tests (`tests/UrDeck.Engine.Tests`): each row of the table in decision 2, each rule above with its warning (redirect the log as the theme tests do), a config file round trip with and without `background`, an older file without it

## 2. Engine: theme palette

- [x] 2.1 `ThemeColorsDefinition.BackgroundPalette` (`List<string>?`). In `MergeOver` it is NOT taken from the baseline (design decision 6): the merged definition keeps this theme's own value or null. Add a comment saying why, because every other member falls back
- [x] 2.2 `Sanitize`: a palette that is not one to four valid colours is set to null with one warning (`colors.backgroundPalette`)
- [x] 2.3 `BackgroundPalette.Resolve(IReadOnlyList<string>? configColors, LoadedTheme theme)` returning four opaque `SKColor`s: config colours, else the theme's palette, else derived (background mixed towards accent by 0, 20, 40 and 60%, per component); fewer than four repeat the last; alpha forced to 255
- [x] 2.4 Add the starting palettes of decision 6 to `default-dark.json`, `default-light.json` and `glass.json`
- [x] 2.5 Tests: each built-in theme has a palette; a user theme without one gets the derived palette from its own colours and logs nothing; a partial user theme with only the palette; an invalid palette warns and derives; one, two and four colours; config colours win; alpha ignored

## 3. Engine: shaders

- [x] 3.1 Add the four files of decision 13 to `src/UrDeck.Engine/Backgrounds/Builtin/` and embed them (`<EmbeddedResource Include="Backgrounds\Builtin\*.sksl" LogicalName="UrDeck.Backgrounds.%(Filename)%(Extension)" />` in `UrDeck.Engine.csproj`). `.sksl` files need no SPDX header check unless the format step complains; if a header is wanted, use a `//` comment line, which SkSL accepts
- [x] 3.2 `ShaderStore(string backgroundsDirectory)`: `Load(name)` returns the text and where it came from (user file first, then embedded), or null with a warning; `BuiltInNames`. A missing folder is not an error. Read user files with sharing, as `ConfigStore.ReadShared` does
- [x] 3.3 Declaration parser: from the text, the list of uniforms with name, component count (1 to 4) and default, per decision 4 (types and aliases, the optional `// default:` comment with a number, comma-separated numbers or a colour). Unsupported types are reported, not thrown
- [x] 3.4 Compile with `SKRuntimeEffect.CreateShader(text, out string errors)`. A null effect or a `uniform shader` child is a failure with the compiler's message in the warning. Compare the effect's `Uniforms` names with the parsed ones and warn for each the parser missed
- [x] 3.5 Uniform binding: host inputs (`iTime`, `iResolution`, `iColor0`..`iColor3`) only when declared with the right type; parameters from their defaults, overridden by the configuration's values with the rules of decision 4 (number, array of the right length, colour string; warnings for wrong form, non-finite, unknown key, a key naming a host input). Colours become straight RGBA floats, alpha dropped for three components. Set values with `SKRuntimeEffectUniforms` (`uniforms["name"] = float` or `float[]`) and build the shader with `effect.ToShader(uniforms)` (SkiaSharp 4.153.1, as in the spike)
- [x] 3.6 `BackgroundClock.Wrap(double elapsedSeconds, double speed)`: `(elapsed * speed) mod 14400` as a `float`; a constant for the snapshot time (10)
- [x] 3.7 Tests, all on Skia's CPU backend: each built-in compiles and paints a 64x256 surface whose pixels are not all one colour and are opaque; two different times give different pixels for each built-in; a parameter default applies and a config value overrides it (a test shader that returns a uniform as its colour makes this a pixel probe); a colour parameter; the palette reaches `iColor0`..`iColor3`; a shader with no uniforms; unknown key, wrong form and host-input key each warn; unsupported uniform type warns; a syntax error fails with a message; a user file overrides a built-in of the same name; a name with `..` or a separator reads nothing; a missing folder still serves the built-ins; the clock at 0, just below and at the wrap, with speeds 0, 1 and 2

## 4. Engine: images

- [x] 4.1 Path resolution: expand environment variables, then absolute as it is or relative to the application folder (passed in, not read from `AppContext` inside the function, so tests can use a temp folder)
- [x] 4.2 Decode through the existing `ImageFiles` loader in `Icons/` (extend it if it does not apply a photo's encoded orientation; check with a rotated JPEG). A missing or undecodable file is a failure with the path in the warning
- [x] 4.3 `BackgroundImage.CoverRect(SKSizeI image, SKSizeI target)`: the source rectangle that, scaled to the target, covers it centred with the overflow cropped. Scale once to the target size with a good-quality filter and release the decoded original
- [x] 4.4 Tests: cover rectangle for wider, taller and same-proportion pictures, including 3840x2160 on 1100x3840; a relative path; an environment variable; a missing file; a file that is not an image; a small generated PNG painted to a surface with pixel probes at the corners and the centre

## 5. Engine: the background object and the snapshot

- [x] 5.1 `DeckBackground` (`IDisposable`): `Create(BackgroundSettings, LoadedTheme, ShaderStore, string appDirectory)` returns null for kind `None` and for every failure of decision 10 (after logging it). It exposes `IsMoving` (shader, speed above 0, declares `iTime`), `Scale` (1 for an image), `Describe()` for the log line, the value-equal `Key` of decision 8, and `Paint(SKCanvas canvas, int width, int height, double elapsedSeconds)`. `Paint` does not clear: the caller has drawn the theme colour. It owns the effect or the scaled bitmap and disposes them
- [x] 5.2 For an image, the scaled bitmap depends on the target size: build it for the size given, rebuild when `Paint` is asked for another size, and offer an async preparation (`PrepareAsync(width, height)`) so the host can decode off the UI thread and the snapshot can wait for it
- [x] 5.3 `PageRenderer.Render` and `RenderToBitmap` take an optional `DeckBackground? background = null` (last parameter, so existing calls compile). After the clear: a shader is painted into an offscreen CPU surface of `width * Scale` by `height * Scale` at the snapshot time and drawn stretched with linear filtering; an image is painted at full size. A painting exception is logged and the page is drawn without the background
- [x] 5.4 Tests: `Key` equality (same settings equal; a changed parameter, palette colour, speed, scale or file time differ); `IsMoving` for speed 0 and for a shader without `iTime`; a snapshot through `PageRenderer` with `plasma` differs from one without and is identical on a second run; with `default-light` it differs from `default-dark`; without a background the bitmap is byte-identical to today's (guard the existing snapshot tests); a failing background still renders the page
- [x] 5.5 Keep the `UrDeck.Engine` line-coverage floor: `dotnet test tests/UrDeck.Engine.Tests -c Release -p:CollectCoverage=true`

## 6. Host: the layer

- [x] 6.1 `HostContext`: a `ShaderStore` for `Path.Combine(appDir, "backgrounds")`, beside `Themes`
- [x] 6.2 `BackgroundLayer` (host, internal, `IDisposable`), constructed with the `_background` grid: `Apply(DeckBackground? background, System.Drawing.Size screenDips, double scaling, SKColor themeColor)`, `SetVisible(bool)`, `SetDisplayOn(bool)`, `Release()`. An equal `Key` keeps the element and the clock and disposes the newly built duplicate; a different one disposes the old element and background first
- [x] 6.3 Moving background: an `SKSwapChainPanel` with `EnableRenderLoop = false`. Still background: an `SKXamlCanvas`. Both `IsHitTestVisible = false`, aligned top left, `Width`/`Height` = the screen size in DIPs times `Scale`, and the composition visual scaled by `1/Scale` (`ElementCompositionPreview.GetElementVisual(element).Scale`). Do not use `RenderTransform` or a `Viewbox` (spike finding 7). `PaintSurface` clears with the theme colour and calls `DeckBackground.Paint` with the layer's elapsed time; an exception there is logged once and the layer drops the background
- [x] 6.4 Pacing: while running, a `CompositionTarget.Rendering` handler that invalidates when a frame is due (interval 1/30 s, a quarter interval of slack, `next = max(next + interval, now)`, as in the spike). Subscribe on start, unsubscribe on stop. Elapsed time is a `Stopwatch` started and stopped with the frames and reset when the key changes
- [x] 6.5 Running = moving and visible and display on. Log each change once: `Background frames started (reason)` and `Background frames stopped (reason): N frames in S s`. On apply, log the line of design decision 7 with the surface's real pixel size (from the first `PaintSurface`) and `moving` or `still`; for none, `Background: none`
- [x] 6.6 Image: call the background's async preparation on the thread pool for the current pixel size, then invalidate on the UI thread; discard the result if the layer's key changed meanwhile. The theme colour shows until then
- [x] 6.7 `MainWindow`: build the `DeckBackground` from the config and `_loadedTheme` and call `Apply` in `RebuildLayout` after the `_pageHeld` check and before the `LayoutKey` comparison; `HideDeck` calls `SetVisible(false)` and `ShowDeck` `SetVisible(true)`; `ReleasePage` calls `Release()`; `OnClosed` disposes the layer. `ApplyBackground` keeps setting the theme-colour brushes. Update the class comment (it says a later change adds the background layers)
- [x] 6.8 Display power: a small host class that registers `GUID_CONSOLE_DISPLAY_STATE` with `RegisterPowerSettingNotification` on the deck's window, handles `WM_POWERBROADCAST` / `PBT_POWERSETTINGCHANGE` in its own window subclass (its own subclass id; keep the delegate alive; unregister and remove on close), logs `Windows: display off` / `Windows: display on`, and raises an event that `MainWindow` forwards to `SetDisplayOn`. It must be installed in both the normal and the `URDECK_ACTIVATABLE=1` mode
- [x] 6.9 `SnapshotCommand`: build the background from the config and the theme in effect (`--theme` included), wait for an image's preparation, pass it to both `RenderToBitmap` calls, dispose it. Update the usage comment

## 7. Install script

- [x] 7.1 `scripts/Install-Local.ps1`: add `backgrounds` to the folders that are never replaced or removed (`$userDirs`) and to the header comment
- [x] 7.2 Run it against a scratch `-Destination` that holds a `backgrounds/x.sksl`: the file survives an update

## 8. Checks without the owner

- [x] 8.1 `dotnet build urdeck.slnx -c Release` with 0 warnings, `dotnet test urdeck.slnx -c Release`, the engine coverage floor, `dotnet format urdeck.slnx --severity warn`, `openspec validate --all --strict`
- [x] 8.2 Snapshots at 1100x3840 (work on a copy of the build output so the tracked sample config is not edited; see `AGENTS.md`): no background (compare with a snapshot from `main`: identical), each of the four built-ins, `plasma` under `default-light` and `glass`, `plasma` with `colors`, an image, a broken user shader (theme colour and a warning in `urdeck.log`). Look at each PNG once
- [x] 8.3 Start the host from the copy with `URDECK_ACTIVATABLE=1` and `plasma` configured; from `urdeck.log` confirm the apply line (surface 550x1920 on the panel), `Background frames started`, and after quitting a stop line whose frames divided by seconds is between 29 and 31
- [x] 8.4 While it runs: save an unrelated config edit (no stop/start lines, no new apply line); change `speed` (one stop, one apply, one start); set `speed` to 0 (apply says `still`, no frames); switch to an image and back; remove `background` (`Background: none`)
- [x] 8.5 Plugin hot-reload still works with a background shown: rebuild a widget while running; the log shows the reload and `Unloaded plugin context(s) collected`, and the background did not restart

## 9. Checks on the panel (owner)

- [x] 9.1 **(owner)** Look at each built-in at the default scale under the dark, light and glass themes: motion, colours, legibility of the cards over it, any banding. Swipe pages over a moving background: it stays put and keeps moving, and swipes that start on the background work
- [x] 9.2 **(owner)** With a moving background: sleep and wake the PC; let Windows switch the display off and wake it (the log has `Windows: display off` then a stop line, then `display on` and a start line); unplug and replug the panel or power-cycle it; if a second scaling is available, set the panel to 150% once and check the apply line's surface size. If the surface is blank or frozen after any of these, implement the rebuild of design decision 8 ("After resume") and repeat
- [x] 9.3 **(owner)** Settle the three built-in palettes and each built-in's default parameters by eye; record the final values in `docs/themes.md` and `docs/backgrounds.md`
- [x] 9.4 **(owner)** Measure with the method of `docs/perf/nexus-baseline.md` on the owner's usual page: no background, an image, `plasma` and `julia` at the default scale, `julia` at scale 1; and once with a game running beside the deck. Write the table to `docs/perf/backgrounds.md` and compare with the budget of design decision 14. A case over budget is reported to the owner, not tuned silently
- [x] 9.5 Measure what `FrameClock` really delivers: with one animating widget for 60 s, count its paints (a temporary log line is enough, removed afterwards). Record the rate in `docs/perf/backgrounds.md` and, if it is not about 30, add a backlog entry; do not change `FrameClock` in this change

## 10. Documentation

- [x] 10.1 New `docs/backgrounds.md` for users: the `background` setting with examples, the four built-ins and their parameters, `speed`, `scale` (and that lowering it is the remedy for a heavy shader), `colors` and where the palette comes from, images, writing a shader (the `main` signature, the host inputs, parameters with `// default:`, a small complete example, porting from Shadertoy in three lines), the four-hour time wrap, what happens on errors and where to read them, that shaders are re-read when the config is saved
- [x] 10.2 `docs/themes.md`: `colors.backgroundPalette`, that it is not inherited from the default theme and how it is derived
- [x] 10.3 `README.md`: a "Backgrounds" entry under "Configuration" linking to `docs/backgrounds.md`; the `backgrounds/` folder in the install section's list of kept files
- [x] 10.4 `AGENTS.md`: `Backgrounds` in the engine's layout line; gotchas: the render-scale rule (visual scale, never `RenderTransform`), pacing from `CompositionTarget.Rendering` and why not `FrameClock`, the log lines to look for, `backgrounds/` next to the exe and that the install script keeps it
- [x] 10.5 `docs/BACKLOG.md` item 9: status, what shipped, and what stays open (per-page backgrounds, a video file, a guard for heavy shaders, reacting to readings, blur behind cards); "Current state" if it lists backgrounds; `docs/ROADMAP.md` where it mentions backgrounds
- [x] 10.6 Archive the change when every task is ticked and verified
