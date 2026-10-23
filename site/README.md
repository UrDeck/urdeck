# The UrDeck website

The site at <https://urdeck.app>: a landing page, the user documentation, and the "Tested on" page. It is an
[Astro](https://astro.build) project (static output, no UI or CSS framework). Nothing outside `site/` needs Node.

```powershell
cd site
npm ci            # Node 22, see .nvmrc
npm run dev       # a local preview
npm run build     # builds into dist/ and runs the checks below
```

## What the build checks

`npm run build` runs `astro build` and then `scripts/check-output.mjs` over `dist/`. Any problem fails it, and so fails
the `site` job of a pull request:

- `docs/schemas/**` is copied into the output under `/schemas/` and compared with its source (the one file the tool and
  the tests read is the one published, `/schemas/v1/shader-manifest.json`).
- Every internal `href` and `src` leads to a file of the output.
- No page makes a request to another host: no `src`, stylesheet, script, `url()` or `@import` pointing at anything but
  `urdeck.app`. A plain link (`<a href>`) to another site is allowed, it is the visitor's choice to follow.
- The theme picker: every theme of `catalog.json` that has a deck in `media.json` is in the picker and has an accent rule
  (a theme without a deck is named in the build log as left out, a warning, since a theme may land before its media
  refresh), and every file a media slot lists for any theme is in the output.

## Where things are

Content and layout are kept apart, so a new design replaces the second without touching the first.

| Path | Holds |
|---|---|
| `src/content/docs/` | The documentation pages, Markdown. `section` and `order` in the front matter place a page in the navigation. |
| `src/data/tested-on.json` | The hardware list; the "Tested on" page is made from it. |
| `src/pages/` | The landing page, "Tested on", and the route for the documentation. The landing copy is in `index.astro`. |
| `src/layouts/`, `src/components/`, `src/styles/` | Layout and style. `home.css` is the landing page's, `global.css` is shared with the documentation. |
| `src/lib/catalog.ts` | The themes, backgrounds and widgets the pages list, read from `src/generated/catalog.json` and `media.json`: one with media appears, one without is left out. Nothing about the product is typed into the site. |
| `src/data/site-copy.json` | The only words kept by hand: a theme's `alt`, a widget's `description` and, per size, a `note` and `alt`. All optional; an entry without them gets text made from the catalog (widget id, then size). |
| `src/scripts/theme-head.js` | The visitor's theme, inlined in `<head>` by `Base.astro`. |
| `src/generated/`, `public/generated/` | The site's pictures (`.webp`), loops (`.mp4`), `media.json` and `catalog.json`. **Generated** by `npm run media`; never edited by hand. See "Media" below. |
| `showcase/` | The page every theme is shown with (`urdeck-config.json`) and the site's widget settings (`widgets.json`). |
| `decks/<theme>/urdeck-config.json` | Optional: a page for one theme instead of the showcase page. None ships. |
| `public/` | Served as they are: `CNAME`, the favicon, the fonts with their licences, and the generated loops. |

## The theme picker

A floating picker on every page chooses the theme the product's pictures are shown in (`site-theme-picker` spec). How it fits
together:

- **Slots.** `ProductMedia.astro` renders one deck, widget or size picture or loop in glass (the page without script) and lists
  every theme's file in `data-variants` (`{theme: {src, srcset}}` for a picture, `{theme: {src, poster}}` for a loop). The slot
  name is `deck` or `widget:<id>:<WxH>`. Background pictures are not slots: each is drawn once, in its pairing's palette.
- **The choice** is made in `src/scripts/theme-head.js`, inlined in `<head>`: `?theme=<name>` (stored), else local storage
  (every access in `try`/`catch`), checked against the catalog's names, then `data-theme` on `<html>`. The hero swaps itself
  with an inline call right after its element; the other slots on `DOMContentLoaded`. `window.urdeckTheme` has `set(name)`
  (stores, swaps every slot, fires `urdeck:theme` on `document`), `on(callback)` and `titles`. Components that follow the
  choice (picker, gallery cards, the widgets' theme label, the backgrounds' "Pairs with" badges and order) use `on`.
- **A loop** that was playing plays on in the new theme, a paused one stays paused, and with reduced motion none is ever
  started. Where glass has a picture and a theme a loop (or the other way round), the element is replaced.
- **The accent rule.** For each theme, `src/lib/accent.mjs` takes the swatch's accent as `--accent`; when it reads under 4.5:1
  on `--ink` it keeps the hue and raises the OKLCH lightness in steps of 0.01 until it does (today only Pop's near-black one).
  `--accent-hover` is 0.08 lighter, `--accent-rgb` and `--glow-rgb` (the glow behind the aurora art) are the accent as a
  triplet. The rules are written into every page's head as `:root[data-theme="<name>"]` (glass has none: it is the site's own
  look); `--ink`, `--text` and the fonts never change. `npm test` has the test of the lift.
- Without JavaScript the picker is not shown and the page is glass.

## Media

Every picture and loop on the site is made by UrDeck itself, with `urdeck-cli preview render` and the providers' sample
data (no host, no network, no readings of the machine), so a visitor can reproduce it and nobody has to check a picture for
personal data. What the site needs is **computed**, never listed by hand (`openspec/specs/site-media`):

- a deck picture of `showcase/urdeck-config.json` (1100x3840) for every theme, over the theme's own background, and a loop
  when the theme or its background moves;
- a picture of every widget at every size it declares for previews, in every theme, and a loop when the widget moves by
  itself (the clock's flip, the weather icons, the stats keyframes);
- a poster and a loop of every background, in the colours of the theme it is shown with.

Adding a theme, widget, size or background adds its files; removing one deletes them.

```bash
cd site
npm run media                    # render what is missing or stale
npm run media -- --status        # say what is missing, stale and unneeded; render nothing
npm run media -- --all           # render everything
npm run media -- decks|widgets|backgrounds [name...]   # only some (carbon, clock-4x2, aurora)
npm test                         # the generator's tests (a stub catalog and a fake renderer)
```

It needs .NET (the tool is built from source), Node 22 and `ffmpeg` on the PATH (`apt install ffmpeg`,
`winget install Gyan.FFmpeg`), and runs on Linux and Windows. Text on Linux differs from Windows at the edges of glyphs
(about 1 % of pixels), so the committed files are the ones made on Linux, by the workflow below.

**Folders.** `src/generated/` holds `decks/<theme>.webp`, `widgets/<widget>-<WxH>-<theme>.webp`,
`backgrounds/<name>.webp` (lossless WebP, optimised by Astro), `media.json` and `catalog.json`; `public/generated/` holds
the loops (`.mp4`, H.264, played forwards then backwards so they have no jump; a loop's first frame is its picture). Files in
these folders that the matrix does not name are deleted. Nothing else is written.

**The manifest.** `src/generated/media.json` lists every entry (`id`, `kind`, `theme`, `widget`, `size`, `background`,
`files`), a `key` of its inputs and the exact options it was rendered with (`settings`), which is what you need to reproduce
a picture by hand. `catalog.json` is `urdeck-cli catalog export`, kept for the site's build. Both are sorted and end with a
newline so a refresh's diff is small. `src/lib/media.ts` reads the manifest; an entry it lacks is left out of the page.

**Keys.** A key is a SHA-256 over the shared inputs (every file of `src/UrDeck.Engine`, `src/UrDeck.Preview`,
`sdk/UrDeck.Sdk`, `widgets/`, `providers/*/sample.json`, except build output and the built-in theme and background folders,
and a settings version in `scripts/media/lib.mjs`) and the entry's own: its theme file and fonts, its background folder, its
widget configuration, the page and the render settings. Text files hash with LF line endings. An engine change therefore
stales everything (any of it can move pixels); changing Ember's file stales Ember's files and the posters shown in it.
A run with nothing changed renders nothing.

**The showcase.** `showcase/urdeck-config.json` is the page every theme's deck shows. `showcase/widgets.json` gives a
widget a configuration for the site (`config`, merged over the default, and `loopStart`, seconds after the sample moment,
for the whole widget or per size under `sizes`); the clock has `style: flap` and starts 26.5 s in, 1.5 s before a minute
turns, so its loop shows the flip. A `decks/<theme>/urdeck-config.json` replaces the showcase page for that theme.

**Limits.** Deck loops are at most 540 px wide and 1.5 MB, widget loops 800 px and 1 MB, background loops 540x960 and 1 MB,
all 4 to 6 s. A loop over its size is encoded again once at `-crf 32`; if it is still over, the run fails naming the file,
its size and the limit.

**Refreshing.** The *Site media* workflow (Actions, run by hand; tick "all" to render everything) renders what is stale on
Linux and opens a pull request `chore(site): refresh media`, or says the media are current. CI reports how many files are
stale (a notice and the job summary, never a failure). See `CONTRIBUTING.md`, "Refreshing the site's media", for the
GitHub App the workflow uses.

**Size.** The first full generation (11 themes, 4 widgets in 13 sizes, 16 backgrounds) is recorded below; unchanged pixels
give unchanged bytes, so a refresh adds only what moved. If the repository nears 1 GB the fallback is a release asset
holding the generated folders, downloaded by the deploy job (Git LFS is not used: its bandwidth quota counts every CI
checkout and Pages deploy, and objects cannot be deleted).

The first full generation (`--all`, in a `mcr.microsoft.com/dotnet/sdk:10.0` container with Node 22 and ffmpeg, 6 renders at once) took about 10 minutes and produced 148 pictures and 112 loops: 12 MB of pictures in `src/generated/` and 11 MB of loops in `public/generated/` (the largest loop is 0.5 MB). A re-run with nothing changed renders nothing. Drawing the shader backgrounds on the CPU dominates the time, which is why a deck loop is drawn at half size.

## Licence

Everything in `site/` (text, pictures and code) is GPL-3.0-or-later, like the application, except the fonts in
`public/fonts/` (Bricolage Grotesque, Instrument Sans and JetBrains Mono), which are under the SIL Open Font License 1.1
(their licence files are next to them). Code samples in the pages
are MIT, as the README says for the docs.

## Publishing

A pull request builds the site and publishes nothing. A merge to `main` that touches `site/`, `docs/schemas/` or
`.github/workflows/site-deploy.yml` runs `.github/workflows/site-deploy.yml`, which deploys to GitHub Pages.
