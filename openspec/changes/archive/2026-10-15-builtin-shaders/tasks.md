## 1. Bring the folders in

- [x] 1.1 Copy the twelve folders from `docs/design/look-and-feel/assets/shaders/` to `src/UrDeck.Engine/Backgrounds/Builtin/<name>/` (`.sksl`, `manifest.json`, `sample.png`)
- [x] 1.2 Add the twelve names to `ShaderStore.BuiltInNames`; confirm the recursive embed pattern picks the files up
- [x] 1.3 A test that `BuiltInNames` and the embedded folders agree (both directions)
- [x] 1.4 `urdeck-cli shader validate --all src/UrDeck.Engine/Backgrounds/Builtin --fail-on-warnings` is clean (needs the `pairsWith` key from `theme-backgrounds`); fix any finding in the shader or its manifest
- [x] 1.5 The lint test over every built-in (not only four) passes
- [x] 1.6 Check each manifest's author and licence; check Warp Gate's text and manifest do not mention Stargate or Star Trek

## 2. Pairing

- [x] 2.1 Fixture copies of the eight drafted themes are those of `theme-format-2` (`tests/UrDeck.Engine.Tests/Fixtures/themes`); the test for "Built-In Pairing Data" reads them with the twelve manifests
- [x] 2.2 The test fails on a pair named by one side, on an unknown theme or background name, and on a background with fewer than two themes or a theme with fewer than two or more than seven backgrounds
- [x] 2.3 The check reads the built-in themes instead of the fixtures once the themes ship (note in the test, for that change)

## 3. Samples and manifests

- [x] 3.1 Render each shader with `urdeck-cli shader render --update-manifest` at 270x480, time 10, with the palette of its paired default theme (from the fixture, `--colors`), replacing the authoring `sample.png`
- [x] 3.2 Check that each manifest's sample `settings` now record the product's render (size, time, colours)
- [x] 3.3 Look at all twelve pictures; reject any that is blank (`SHADER070`) or visibly wrong

## 4. The time wrap

- [x] 4.1 A test that renders every moving built-in at time 0 and 14,400 seconds and compares (rounding tolerance)
- [x] 4.2 Any shader that fails is fixed by changing its period to one that divides 14,400, not by changing the test

## 5. On the panel

- [x] 5.1 For each shader at the default scale on the owner's usual page: select it in the configuration, run the host detached, read `urdeck.log` (`Background: shader '...'` and the `frames started` and `frames stopped` lines, about 30 frames per second), and look at the screen
- [x] 5.2 Check each for banding in dark gradients, the `atan` seam at the left of centre in Warp Gate and Vortex, and (after letting the clock run past the first wrap if practical, or by starting the host at a late time) hash artefacts in Nebula and Embers
- [x] 5.3 Measure CPU, GPU and memory with the method of `docs/perf/nexus-baseline.md` against the same page without a background; Nebula and Shapes first; record all twelve in `docs/perf/builtin-shaders.md`
- [x] 5.4 (not needed: both within budget) If Nebula or Shapes is over the budget, lighten its default parameters (fewer star layers or stars, fewer shapes), re-measure, and note it in the manifest's description and the perf note
- [x] 5.5 (nothing to replace: no defaults changed) Replace any sample that no longer matches after tuning (task 3.1 again for that shader)

## 6. Documentation

- [x] 6.1 `docs/backgrounds.md`, "Built-in shaders": each of the sixteen with a line, its parameters (range and default) and what its four colours mean; a test or check that every built-in is listed
- [x] 6.2 `site/src/content/docs/`: the backgrounds page lists the new shaders (AGENTS.md: a change to a background updates it); posters and loops are not part of this change
- [x] 6.3 `AGENTS.md`: the built-in list in the background paragraph
- [x] 6.4 `docs/design/theme-format-2-deferred-keys.md`: nothing to change; confirm

## 7. Verification

- [x] 7.1 `dotnet build urdeck.slnx -c Release` with 0 warnings; `dotnet test urdeck.slnx -c Release`; the Engine coverage floor; `dotnet format urdeck.slnx --severity warn`; `openspec validate --all --strict`
- [x] 7.2 The CI shader step (`shader validate --all ... --fail-on-warnings`) passes locally on the sixteen
- [x] 7.3 A snapshot of the deck with each new shader, at least `horizon`, `nebula` and `shapes`, to compare with the samples
