## Why

The eight new themes each pair with moving backgrounds that carry their mood, and the four shaders that ship today
(plasma, aurora, gradient, julia) are generic washes. Twelve new shaders were written for the purpose: a retro horizon,
slow RGB light, topographic contours, a nebula with irregular stars, data streams, pool caustics, rising embers, a
drafting plotter, flat shapes, a warp-speed tunnel, an equalizer and a vortex. They compile and validate with the
product's own `urdeck-cli` (0 errors; the only warning is the manifest key `pairsWith`, added by `theme-backgrounds`),
but they have not run on the panel, and two of them (Nebula, Shapes) are expensive per pixel on the CPU.

Shipping them as their own change keeps the art and its measurements reviewable apart from the engine work, and gives
every user twelve new backgrounds immediately, whatever theme they use.

## What Changes

- Twelve built-in background folders, each with its `.sksl`, `manifest.json` and a `sample.png` rendered by the host:
  `horizon`, `spectrum`, `contour`, `nebula`, `streams`, `ripple`, `embers`, `plotter`, `shapes`, `warpgate`, `equalizer`
  and `vortex`. They load through the same path as the four that exist and as a user's own folders.
- The built-in list grows from four to sixteen. Each is available regardless of the user's `backgrounds` folder, and a
  user folder of the same name replaces it, as for the first four.
- Every built-in lints clean (no error, no warning), is documented in `docs/backgrounds.md` with its parameters and
  palette roles, and has a pairing in its manifest (`pairsWith.themes`) that agrees with the draft themes' lists.
- Cost is measured on the panel at the default scale and recorded in `docs/perf/`; a shader over the budget ships with
  a lighter default (fewer stars, fewer shapes) rather than heavy. Nebula and Shapes are measured first.
- The periodic motion of each shader divides the four-hour time wrap, so the wrap is seamless; this is checked, along
  with the usual GPU differences (banding, an `atan` seam in Warp Gate and Vortex, large-time hash artefacts in Nebula
  and Embers).

Not in this change: the themes that default to these shaders, posters and loops for the website (media is rendered once
the themes exist), and audio reaction for Equalizer (it is free-running).

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `backgrounds`: "Shader Backgrounds" lists the built-in shaders (sixteen, not four); "Background Cost" gains the
  budget rule for the new ones.
- `background-manifest`: "Background Lint" requires every built-in, not four, to lint clean, and requires their
  pairings to agree with the shipped themes' lists.

## Impact

- `src/UrDeck.Engine/Backgrounds/Builtin/<name>/` (36 new files), `ShaderStore.BuiltInNames`, embedded resource patterns
  (already recursive), the tests that enumerate the built-ins.
- `docs/backgrounds.md` ("Built-in shaders"), `docs/perf/` (a measurement note), `site/src/content/docs/` (the
  backgrounds page, per AGENTS.md), `AGENTS.md` (the built-in list).
- The engine assembly grows by about 1 MB of files; installer size is not a concern for this product.
- Depends on `theme-backgrounds` (the `pairsWith` manifest key).
