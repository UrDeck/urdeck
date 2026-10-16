## 1. Comparison

- [x] 1.1 `GoldenImage.Compare(SKBitmap actual, string name)` in `tests/UrDeck.Engine.Tests/Support`: channel limit and differing-pixel fraction, golden folder `tests/UrDeck.Engine.Tests/Goldens`, copied to the output like `Fixtures`
- [x] 1.2 On a mismatch write `<name>.actual.png` and `<name>.diff.png` under `artifacts/golden-failures/`; the failure message names both files, the largest channel difference and the count of differing pixels
- [x] 1.3 `URDECK_UPDATE_GOLDENS=1` writes the golden and passes; a missing golden fails with a message that names the variable
- [x] 1.4 Tests of the comparer itself: identical passes, one channel off by the limit passes, off by one more fails, a small area changed within the pixel fraction passes and beyond it fails, a size difference fails, update mode writes the file

## 2. Sample page

- [x] 2.1 `SamplePage.Build()` in `Support`: clock (4x1), stats (2x1, CPU and GPU), weather (4x2), four shortcuts, with a fake reading source and host, and the fixed time of 13 October 2026, 21:47:32; the readings are the package's (CPU 68 %, GPU 82 %, 18 °C partly cloudy, forecast 21/17/14/16/19)
- [x] 2.2 A test renders the page twice with `PageRenderer.RenderToBitmap` and expects identical bytes (the animations draw from the given time)
- [x] 2.3 A test that the page lays out without overlap or a widget left unplaced at 1100x1920

## 3. Goldens

- [x] 3.1 Render `default-dark`, `default-light` and `glass` at 1100x1920, look at each picture, and commit them as the goldens
- [x] 3.2 Render `default-dark` with a dock of four shortcuts and the dot indicator, look at it, commit it
- [x] 3.3 `GoldenTests`: one test per golden, Windows only with the skip reason of `[WindowsGlyphFact]`
- [x] 3.4 Measure the noise: render each golden on the developer's machine twice and read the CI result; set the limits no wider than needed and write them into `Compare` and `CONTRIBUTING.md`
- [x] 3.5 A negative test: draw the dark page with the card border one pixel wider and expect `Compare` to fail
- [x] 3.6 Change a v1 theme value in a scratch copy (for example the card radius), confirm the matching golden fails and the others do not; note the result in the pull request

## 4. CI and documentation

- [x] 4.1 `.github/workflows/ci.yml`: upload `artifacts/golden-failures/` when the Windows test step fails; make sure the folder is ignored by git
- [x] 4.2 `CONTRIBUTING.md`: a "Golden images" section (what they are, the update command, the tolerance, "a pull request that changes a golden says why; a change to a v1 theme golden needs a reason in the description")
- [x] 4.3 `AGENTS.md`: one line under Commands for the update variable
- [x] 4.4 Build with 0 warnings, `dotnet format urdeck.slnx --severity warn`, `openspec validate --all --strict`, the engine tests with the coverage floor
- [x] 4.5 Open the pull request; confirm `ci-ok` is green on `windows-latest`, and that the goldens agree between the developer's machine and CI
