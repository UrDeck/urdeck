# Media for the landing page

The landing page finds its pictures and loops **by file name**: drop a file in the right folder, no code change.
Nothing here is required for the site to build. A widget size or background with no media is simply left out of the built
page (`npm run dev` shows a labelled blank for a missing background, so you can see what is missing).

Tick a box when the real file is in. Files marked **interim** exist today as a stand-in, cut from the existing glass-theme
deck picture or taken from a built-in background's `sample.png`; replace them with a proper render under the same name.

## How to render

`npm run media` (Windows; builds the host and the tool) renders the widget pictures and the background posters, and the
background loops when `ffmpeg` is on the PATH (`winget install Gyan.FFmpeg`). `npm run media -- widgets|posters|loops [name...]`
limits it. It is how all the pictures below were made; a widget loop has no tool yet (the host cannot render a widget at a
given time). The manual commands it wraps:

- **A widget:** put only that widget on a page in a scratch config and run
  `UrDeck.Host.exe --snapshot out.png --size 1100x<height>` (the grid is four square cells across: a 4x2 card is about
  1100 by 550). Use the `glass` theme over the `aurora` background so every picture matches. Crop to the card's edge.
- **A background frame:** `urdeck-cli shader render src/UrDeck.Engine/Backgrounds/Builtin/<name> -o frame.png --size 540x960 --time <seconds> --theme glass`
- **A loop from frames** (30 per second, a few seconds, ffmpeg):
  `ffmpeg -framerate 30 -i frames/%04d.png -c:v libx264 -pix_fmt yuv420p -crf 28 -movflags +faststart -an <name>.mp4`
  If a shader does not return to where it began, play it forwards then backwards so the loop has no jump.

## Widget pictures (required for a size to appear)

Folder: `site/src/assets/widgets/`. PNG, glass theme, sized as rendered (1100 px wide for the four-cell-wide ones).

- [x] `clock-4x2.png`
- [x] `stats-4x4.png`
- [x] `stats-4x2.png`
- [x] `weather-4x4.png`
- [x] `weather-4x2.png`
- [x] `shortcut-1x1.png`

## Widget loops (optional, shown instead of the picture, which becomes its poster)

Folder: `site/public/media/widgets/`. MP4 (H.264), no audio, 3 to 6 seconds, the same size as the picture or smaller,
under 1.5 MB, first frame equal to the picture, loops without a visible jump.

- [ ] `clock-4x2.mp4`: the split-flap digits flipping at a minute change
- [ ] `weather-4x4.mp4`: the animated weather icons
- [ ] `stats-4x4.mp4`: the gauges moving to new readings
- [ ] `shortcut-1x1.mp4`: a tap (the card shrinks and dims while the finger is down)
- [ ] `stats-4x2.mp4`, `weather-4x2.mp4`: only if worth it

## Backgrounds

All four show today as stills. Each wants a proper poster and a loop.

Posters, folder `site/src/assets/backgrounds/`, PNG, 540 by 960 (9:16):

- [x] `aurora.png`
- [x] `plasma.png`
- [x] `gradient.png`
- [x] `julia.png`

Loops, folder `site/public/media/backgrounds/`, MP4 (H.264), no audio, 540 by 960, 4 to 6 seconds, under 1 MB each, `glass` theme colours:

- [x] `aurora.mp4`
- [x] `plasma.mp4`
- [x] `gradient.mp4`
- [x] `julia.mp4`

## When a theme, widget or background is added

- **A theme:** `site/src/assets/decks/<theme>.png` (rendered like the others, 1100 by 3840) and an entry in `src/data/themes.json`.
- **A widget:** an entry in `src/data/widgets.json` (name, type id, description, sizes) and a picture per size as above.
- **A background:** an entry in `src/data/backgrounds.json`, a poster and a loop as above.

## Also useful

- [ ] `site/public/og-image.png`, 1200 by 630: the picture shown when the site is shared. The page uses it when the file exists.
- [ ] Re-render `site/src/assets/decks/*.png` if the app's look changes (`pwsh scripts/Render-SiteDecks.ps1`). The hero uses `glass.png`.
