## Context

The four built-ins live as embedded folders `Backgrounds/Builtin/<name>/{<name>.sksl, manifest.json, sample.png}`; the
csproj embeds them recursively and `ShaderStore.BuiltInNames` lists the names. The handoff package
(`docs/design/look-and-feel/assets/shaders/`, described in its `05-backgrounds.md`) holds twelve more folders in the
same shape. They were written and rendered with skia-python only. Before this change was drafted they were run through
the product's own `urdeck-cli shader validate --all --fail-on-warnings`: 0 errors, 0 remarks, and exactly one warning
per manifest, `SHADER004` for the key `pairsWith`, which `theme-backgrounds` makes a known key. Their pairing data was
checked against the eight drafted themes: every pair is named by both sides, every theme lists between two and seven
backgrounds, every background names two to four themes, and each theme's default is in its own list.

What has not been done: no shader has run on the GPU, the panel has not been looked at, and the CPU raster numbers in the
handoff (Nebula 115 ms, Shapes 167 ms at 270x480) are relative hints from another machine.

## Goals / Non-Goals

**Goals:**
- The twelve shaders ship as built-ins that lint clean, look right on the panel and stay inside the existing cost budget.
- Their samples are renders by the product, not by the authoring tool.
- The docs list them, with parameters and palette roles.

**Non-Goals:**
- Changing any shader's design. Fixes that the panel check forces are in scope; taste changes are not.
- Website posters and loops (made when the themes exist, by the existing media script).
- A host audio uniform for Equalizer; any new host uniform.

## Decisions

**One change for all twelve.** Splitting six and six (as the handoff's two PRs do) buys little: the work is the same
loop for each (validate, render, look, measure) and the review is of art, not code. The tasks are grouped so a session
can stop after any shader and the repository still builds.

**Pairing lives in the manifests and agrees with the drafts.** Each manifest keeps the `pairsWith.themes` it came with.
A test (the "Built-In Pairing Data" requirement) reads the twelve manifests and the eight drafted theme fixtures from
`theme-format-2` and fails on any pair named by one side only, so the data cannot drift while the themes are still
fixtures, and keeps working once they are built-in.

**`sample.png` is made by `urdeck-cli shader render`, with the theme each shader is paired with.** The authoring render
came from another Skia build; the product's render is what a user's own CLI run would produce. The command also
rewrites the manifest's sample `settings` (`--update-manifest`), recording size, time, theme and palette. Where a shader
has a default theme (the pairing table's `D`), that theme's palette is the sample's. The drafted themes are not
loadable by name yet, so the render uses the palette from the draft fixture passed with `--colors`; a sample made that
way is re-rendered when the theme ships (a task of that change).

**The two heavy shaders are measured first and lightened by default, not by `scale` alone.** Nebula (three star layers
plus fBm clouds) and Shapes (a 12-iteration SDF loop) are the ones the handoff warns about. The first thing to try if
either is over budget is its own default (fewer star layers, fewer shapes), because a theme default that needs the user
to lower `scale` is a poor first impression. `scale` stays the user's remedy.

**The time wrap is tested on the shader, not just asserted.** Each shader's periods divide 14,400 seconds by
construction. A test renders every moving built-in at time 0 and 14,400 and compares; this catches a period added later
that does not divide the wrap. GPU-only artefacts (banding, the `atan` seam in Warp Gate and Vortex, hash precision at
large times in Nebula and Embers) cannot be tested on the CPU and are a manual task on the panel.

**The built-in list has one source.** `ShaderStore.BuiltInNames` is what the embedded folders and the tests enumerate.
A test asserts that the list and the embedded folders agree, so a folder added without a name, or a name without a
folder, fails in CI.

## Risks / Trade-offs

- **GPU output differs from the CPU renders.** → Look at every shader on the panel at the default scale before ticking
  its task; replace `sample.png` with the product's render; log differences in `docs/perf/`.
- **Nebula or Shapes is over the 5% of a core budget.** → Lighten the defaults first; record both numbers.
- **Sixteen backgrounds is a lot to choose from in the future editor.** → Tags and pairing exist for that; not a concern
  of this change.
- **The twelve are original work with the repository's licence.** The manifests carry the author and
  `GPL-3.0-or-later` (the same as the existing built-ins); nothing here borrows from another project. A reviewer should
  confirm Warp Gate's text and manifest mention neither Stargate nor Star Trek (the name stays "Warp Gate").
- **Embedded size grows by about 1 MB.** Not a concern for this product; stated so nobody spends time on it.

## Migration Plan

Additive. No configuration changes. A user who already has a folder named like a new built-in (for example `contour`)
keeps theirs, as the existing rule says. Rollback is a revert.

## Open Questions

None.
