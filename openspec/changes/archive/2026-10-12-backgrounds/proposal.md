## Why

The deck's background is one flat theme colour. HYTE Nexus ships a few moving backgrounds and does not let the owner
add one. Backlog item 9 asks for a custom image and a painted visualization behind the cards, and it is the reason
composition moved to the GPU. The spike of 2026-10-11 (`docs/perf/backgrounds-spike.md`) settled how: a shader drawn
live on one GPU surface at half resolution costs about 3% of one core and under 1% of the GPU, which is far less
memory and GPU than playing a video of the same picture.

## What Changes

- A new top-level `background` setting in `urdeck-config.json` with two kinds:
  - `shader`: a moving background generated from parameters (speed, angle, colours, and whatever else the shader
    declares). Four are built in (`plasma`, `aurora`, `gradient`, `julia`).
  - `image`: a picture file of the user's (a wallpaper), scaled to cover the panel.
- User shaders: a `backgrounds/<name>.sksl` file next to the executable is selected by name exactly like a built-in
  one. Its parameters are its uniforms, set from the configuration by name, with defaults declared in the file.
- The background is deck-wide: one layer that stays put while pages slide over it.
- Colour: the theme supplies a palette of up to four colours to every shader, the configuration can replace it, and a
  shader is free to ignore it. Themes gain an optional `colors.backgroundPalette`; a theme without one gets a palette
  derived from its own background and accent.
- Cost rules: 30 frames per second, rendered at a configurable fraction of the panel's resolution (half by default),
  no frames while the deck is hidden or the display is off, and nothing at all when no background is configured or the
  background is still (an image, or a shader with speed 0).
- `--snapshot` draws the background too, on the CPU, at a fixed time.
- Failures (unknown name, a shader that does not compile, a missing or unreadable image) are logged and the deck shows
  the theme's background colour.
- `scripts/Install-Local.ps1` leaves `backgrounds/` alone, as it does `themes/`.

Not in this change: per-page backgrounds (the unused `pages[].background` field stays unused), a video file as
background, backgrounds that react to readings, blurring what is behind a translucent card, and any guard against a
user shader that is too heavy for the GPU (the scale setting is the only tool for now).

## Capabilities

### New Capabilities
- `backgrounds`: the `background` setting, the two kinds, built-in and user shaders, shader inputs and parameters, the
  colour palette, frame pacing and render scale, when frames stop, failure behaviour and the snapshot.

### Modified Capabilities
- `host-shell`: "GPU Composition" allows a frame source for an animated background, and "Theme Application" puts the
  theme's background colour under a configured background instead of making it the whole background.
- `theme`: a new requirement for the optional background palette and how it is derived when a theme has none.
- `distribution`: "Local Install" keeps the user's `backgrounds/` folder.

## Impact

- `src/UrDeck.Engine`: a new `Backgrounds` folder (configuration parsing and validation, the shader store with the
  embedded built-in `.sksl` files, uniform binding, the time wrap, image cover-fit, a CPU painter). `Config/ConfigStore.cs`
  gets `UrDeckConfig.Background`. `Themes` gets the palette. `Rendering/PageRenderer.cs` draws the background for
  snapshots and tests.
- `src/UrDeck.Host`: a background layer in `MainWindow` (an `SKSwapChainPanel` for a moving shader, an `SKXamlCanvas`
  for a still one), its frame pacing, and a display-power notification. `SnapshotCommand` passes the background on.
- `sdk/`: not touched. Widgets and providers do not see the background.
- No new packages. `SKSwapChainPanel` and the ANGLE libraries (`libEGL.dll`, `libGLESv2.dll`) already ship with
  `SkiaSharp.Views.WinUI`; they are in the published folder today.
- `scripts/Install-Local.ps1`, `README.md`, `docs/themes.md`, a new `docs/backgrounds.md`, `AGENTS.md`,
  `docs/BACKLOG.md` (item 9), `docs/perf/backgrounds.md` for the measurements on the real host.
- Cost: with a moving background the host is no longer idle. The spike's numbers are the budget (see the design).
