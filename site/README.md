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

## Where things are

Content and layout are kept apart, so a new design replaces the second without touching the first.

| Path | Holds |
|---|---|
| `src/content/docs/` | The documentation pages, Markdown. `section` and `order` in the front matter place a page in the navigation. |
| `src/data/tested-on.json` | The hardware list; the "Tested on" page is made from it. |
| `src/pages/` | The landing page, "Tested on", and the route for the documentation. The landing copy is in `index.astro`. |
| `src/layouts/`, `src/components/`, `src/styles/` | Layout and style. `home.css` is the landing page's, `global.css` is shared with the documentation. |
| `src/data/widgets.json`, `themes.json`, `backgrounds.json` | What the landing page's showcases list: the widgets with their sizes, the sample decks, the backgrounds. |
| `src/assets/widgets/`, `src/assets/backgrounds/`, `public/media/` | Pictures and loops of the widgets and backgrounds, found by file name. See `MEDIA-CHECKLIST.md`. |
| `decks/<name>/urdeck-config.json` | The config of each sample deck. |
| `src/assets/decks/<name>.png` | The sample decks, rendered by UrDeck itself and committed. |
| `public/` | Served as they are: `CNAME`, the favicon, the fonts with their licences, and the loops in `media/`. |

## Sample decks

Run `pwsh scripts/Render-SiteDecks.ps1` from the repository root on a Windows machine (it builds the host and runs
`UrDeck.Host.exe --snapshot --size 1100x3840` for each folder under `decks/`). The pictures show the readings of the
machine that drew them and the weather needs the network, so look at each one before committing it: nothing personal, the
readings present, the weather drawn. CI does not render them.

## Pictures and loops on the landing page

The widget and background showcases list what is in `src/data/*.json` and look for media by name, so adding a widget, a size
or a background is a data entry and a file. A size or background with no media is left out of the built page. The full list
of what to render, with sizes and file names, is in `MEDIA-CHECKLIST.md`.

## Widget and background media

`npm run media` renders the pictures of each widget size and the background posters and loops with UrDeck itself (Windows,
see the top of `scripts/render-media.mjs`); `MEDIA-CHECKLIST.md` lists what the page wants. Look at each picture before
committing it.

## Licence

Everything in `site/` (text, pictures and code) is GPL-3.0-or-later, like the application, except the fonts in
`public/fonts/` (Bricolage Grotesque, Instrument Sans and JetBrains Mono), which are under the SIL Open Font License 1.1
(their licence files are next to them). Code samples in the pages
are MIT, as the README says for the docs.

## Publishing

A pull request builds the site and publishes nothing. A merge to `main` that touches `site/`, `docs/schemas/` or
`.github/workflows/site-deploy.yml` runs `.github/workflows/site-deploy.yml`, which deploys to GitHub Pages.
