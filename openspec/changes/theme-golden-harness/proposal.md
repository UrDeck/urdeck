## Why

The look-and-feel work changes how cards, text and gauges are drawn: card chrome, typography, gauges and pattern, one
change after another. Each of them must leave the three existing themes (`default-dark`, `default-light`, `glass`)
pixel-for-pixel as they are, and today nothing checks that except comparing bytes by hand. The format 2 change could
say "v1 invariance is trivially true" because it drew nothing. The next one cannot.

This change adds the check first, before anything is drawn differently: a way to render a fixed page with a fixed
theme and compare it with a picture kept in the repository, so a later change that moves a pixel of a v1 theme fails a
test instead of being found on the panel.

## What Changes

- `GoldenImage.Compare` in the engine tests: compares a rendered bitmap with a checked-in PNG under a per-channel and a
  per-pixel-count tolerance, and on a mismatch writes the actual picture and a difference picture beside the test
  results.
- A switch to rewrite the goldens (`URDECK_UPDATE_GOLDENS=1`) so an intended change is one command and a visible diff of
  PNGs in the pull request.
- A sample page (`SamplePage`): a clock, a stats card (CPU and GPU), a weather card and four shortcuts, with fixed
  readings and a fixed time, rendered through `PageRenderer`. Every later picture (theme changes, the CLI's `theme
  render`, the site) starts from this page.
- Goldens for `default-dark`, `default-light` and `glass` at 1100x1920, and one of `default-dark` with a dock and a page
  indicator, so the chrome around the page is covered too.
- A short section in `CONTRIBUTING.md` on what a golden is, when to update one and what a pull request must say when it
  does.
- CI uploads the actual and difference pictures when a golden test fails.

Nothing in the product changes. There is no new behaviour of the program to specify, so the change carries no spec
delta (`skip_specs`).

## Capabilities

### New Capabilities

None.

### Modified Capabilities

None. The harness is test tooling; the product behaviour it guards is already specified.

## Impact

- `tests/UrDeck.Engine.Tests`: `Support/GoldenImage.cs`, `Support/SamplePage.cs`, `GoldenTests.cs`, `Goldens/*.png`.
- `.github/workflows/ci.yml`: one upload step on failure.
- `CONTRIBUTING.md`: a "Golden images" section.
- No change to `src/`, `sdk/`, `widgets/`, `providers/`, no new dependency.
