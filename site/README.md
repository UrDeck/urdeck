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
| `src/layouts/`, `src/components/`, `src/styles/` | Layout and style. |
| `decks/<name>/urdeck-config.json` | The config of each sample deck. |
| `src/assets/decks/<name>.png` | The sample decks, rendered by UrDeck itself and committed. |
| `public/` | Served as they are: `CNAME`, the favicon, the Inter font with its licence. |

## Sample decks

Run `pwsh scripts/Render-SiteDecks.ps1` from the repository root on a Windows machine (it builds the host and runs
`UrDeck.Host.exe --snapshot --size 1100x3840` for each folder under `decks/`). The pictures show the readings of the
machine that drew them and the weather needs the network, so look at each one before committing it: nothing personal, the
readings present, the weather drawn. CI does not render them.

## Licence

Everything in `site/` (text, pictures and code) is GPL-3.0-or-later, like the application, except the Inter font in
`public/fonts/`, which is under the SIL Open Font License 1.1 (its licence file is next to it). Code samples in the pages
are MIT, as the README says for the docs.

## Publishing

A pull request builds the site and publishes nothing. A merge to `main` that touches `site/`, `docs/schemas/` or
`.github/workflows/site-deploy.yml` runs `.github/workflows/site-deploy.yml`, which deploys to GitHub Pages.
