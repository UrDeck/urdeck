## Context

`PageRenderer.RenderToBitmap(width, height, widgets, loaded, time, chrome, dock, background)` draws a whole page and the
dock on one canvas with the live layout, card and widget code. It is how the `--snapshot` command works and how
`DeckBackgroundTests` and `BackgroundPlanTests` already render pages. The engine tests reference the Clock, Stats,
Weather and Shortcut widgets, and have fakes for readings (`IReadingSource` in `StatsWidgetTests`,
`WeatherWidgetTests`) and for the host services a shortcut uses (`ShortcutWidgetTests`). The built-in themes load Inter
from an embedded resource, so a built-in theme needs no font from the machine.

Text is the problem for a picture comparison across machines. `[WindowsGlyphFact]` exists because FreeType (Linux) and
DirectWrite (Windows) hint advances differently and their edge pixels differ. See the package notes in
`docs/design/look-and-feel/03-engine-work.md` section 6 for the intent (tolerance of 2 on a channel and 0.1 % of
pixels, an update switch).

## Goals / Non-Goals

**Goals:**
- A v1 theme that changes by one visible pixel fails a test, on the developer's machine and in CI.
- Updating a golden is deliberate, one command, and shows as PNGs in the pull request diff.
- The sample page is reusable by later changes without copying it.

**Non-Goals:**
- Goldens for the eight drafted themes. They are fixtures without their fonts today; each theme change adds its own
  goldens when it becomes a built-in (the package asks for 1100x1920 and 1100x3840 there).
- Goldens for shaders or pictures as backgrounds (their tests compare pixels already).
- Linux goldens. Text differs; see decision 2.
- Moving the sample page where the command line tool can reach it. `theme render` is its own change and moves or
  shares the page then.

## Decisions

### 1. Compare with a threshold, not byte for byte

`GoldenImage.Compare(actual, name)` passes when no channel of any pixel differs by more than a per-channel limit and
no more than a fraction of the pixels exceed it. The starting values are the package's: a channel difference of 2 and
0.1 % of the pixels. The apply step renders each golden twice, and on the developer's machine and on CI, and sets the
limits no wider than what those runs need, then records them in `CONTRIBUTING.md`.

*Alternative: exact bytes.* Rejected: a Windows update that retunes DirectWrite would fail every golden for no
reason. *Alternative: a perceptual score.* Rejected: it hides a one-pixel border moving, which is exactly the
regression this guards.

### 2. Goldens run on Windows only

The goldens are made and checked on Windows, like the pixel-exact text tests (`[WindowsGlyphFact]`), and show as
skipped with the same reason on Linux. The Linux job does not run them.

*Alternative: two sets of goldens, one per OS.* Rejected: double the files, double the review, and the Linux set
guards nothing the Windows set does not (the host is Windows-only).

### 3. Update with an environment variable, not a command-line switch

The package says `--update-goldens`. A test run takes no switches of its own through `dotnet test`, so
`URDECK_UPDATE_GOLDENS=1 dotnet test tests/UrDeck.Engine.Tests --filter Golden` rewrites the files. In that mode a
golden test writes the picture and passes, and says so in its output; a missing golden is an ordinary failure that
names the variable.

### 4. Failure leaves evidence

A failed comparison writes `<name>.actual.png` and `<name>.diff.png` (the differing pixels in red over a faded
golden) under `artifacts/golden-failures/` at the repository root (already outside the sources; add to
`.gitignore` if it is not). CI uploads that folder when the test step fails. Without it, a CI-only difference (a font
rasteriser on `windows-latest` that differs from the developer's machine) cannot be seen, only guessed.

### 5. The sample page is code, not a JSON file

The package suggests `Fixtures/sample-page.json`. The readings are objects of the test fakes, and a shortcut needs
a recorded icon source, so a JSON file would need a loader written only for the tests. `SamplePage.Build()` returns
the widgets, their configurations and the fixed time (13 October 2026, 21:47:32), with CPU 68 %, GPU 82 %, 18 °C
partly cloudy and the forecast 21/17/14/16/19 from the package. The shortcuts have no icon (`IconResult.Loading`), so
they draw their letter tile, which is deterministic.

### 6. What is covered

| Golden | Theme | Size | Around the page |
|---|---|---|---|
| `default-dark-page` | `default-dark` | 1100x1920 | none |
| `default-light-page` | `default-light` | 1100x1920 | none |
| `glass-page` | `glass` | 1100x1920 | none |
| `default-dark-dock` | `default-dark` | 1100x1920 | dock of four shortcuts, indicator (dots) |

`glass` is drawn over its own background colour, not over a shader, so the picture does not depend on the shader
renderer.

## Risks / Trade-offs

- [`windows-latest` rasterises text differently from the developer's Windows] → decision 4 shows the difference; if it
  is more than the tolerance, the goldens are made from the CI artifact and the tolerance stays tight. If neither
  agrees, only the cards and the shapes are compared and the text areas are masked; apply must record which.
- [A tolerance wide enough for text hides a one-pixel hairline moving] → the first task measures the real noise and sets
  the limit just above it, and a test shifts a card border by one pixel and expects a failure.
- [Goldens go stale and people update them without looking] → the update is a diff of PNGs, the pull request template
  line in `CONTRIBUTING.md` asks for the reason, and v1 goldens changing is called out as a rule, not a habit.
- [The page includes the weather animation and the flap clock] → both draw from the time they are given, so a fixed
  time is deterministic; the first test renders twice and expects identical bytes.
