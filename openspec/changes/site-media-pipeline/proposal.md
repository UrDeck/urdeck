## Why

The site's pictures and loops are made by hand on Windows, from live data, with a checklist ticked by hand
(`site/MEDIA-CHECKLIST.md`), and they cover one theme and four of sixteen backgrounds. Adding, changing or removing a
theme, background or widget should regenerate everything the site needs without anyone deciding what to render or
checking a picture for personal data. `preview-renderer` makes every picture deterministic and possible on Linux; this
change uses it to make the site's media a generated output of the repository.

## What Changes

- **A media matrix derived from the product.** What the site needs is computed from `urdeck-cli catalog export`: for every
  theme a deck picture and loop of the showcase page over the theme's own background; for every widget, every previewed
  size and every theme a picture, and a loop where the widget moves; for every background a poster and a loop. Nothing is
  listed by hand; an optional overrides file changes a widget's configuration for the site.
- **One showcase page for every theme.** `site/showcase/urdeck-config.json` is the deck every theme is shown with. A
  `site/decks/<theme>/urdeck-config.json` still overrides it for one theme. **BREAKING** (site): the eleven per-theme
  deck configurations are removed, so every theme shows the same page.
- **The generator owns its output.** `npm run media` (rewritten) renders with `urdeck-cli preview render` and `ffmpeg`
  only, into `site/src/generated/` and `site/public/generated/`, with a manifest recording what each file was made
  from. A file it no longer needs is deleted; a file whose inputs did not change is not rendered again.
- **Linux.** The generator runs on Linux and Windows. **BREAKING** (tools): `scripts/Render-SiteDecks.ps1` and the
  host's part in `npm run media` are removed.
- **A manual workflow.** `.github/workflows/site-media.yml` on `workflow_dispatch` renders what is stale on Linux and opens
  a pull request "chore(site): refresh media" through a GitHub App token, so the pull request's required checks run.
- **A staleness report.** CI reports, without failing, how many media files are out of date. The checklist becomes this
  report.

Not in this change: how the site shows the media (`site-gallery`), the docs (`docs-rewrite`), running the workflow on
release tags (a later change, once releases exist).

## Capabilities

### New Capabilities

- `site-media`: the media matrix, the showcase page and its overrides, the generated output and its manifest, staleness,
  the refresh workflow and the report.

### Modified Capabilities

- `website`: "Sample Decks Are Real" now means rendered with sample data by the preview tool, reproducible exactly.

## Impact

- `site/scripts/render-media.mjs` rewritten; `site/scripts/media-status.mjs` new; `site/showcase/` new; `site/decks/`
  removed; `site/MEDIA-CHECKLIST.md` removed; `site/src/lib/media.ts` reads the manifest.
- Generated, committed through refresh pull requests: `site/src/generated/` (pictures, `catalog.json`, `media.json`) and
  `site/public/generated/` (loops). The current files in `site/src/assets/{decks,widgets,backgrounds}` and
  `site/public/media/` are replaced by the first refresh.
- `.github/workflows/site-media.yml` new; `ci.yml`'s `test-linux` job gains a non-blocking staleness step.
- `scripts/Render-SiteDecks.ps1` removed.
- Repository setup (the owner, once): a GitHub App with contents and pull request write on the repository, its id and
  private key as secrets.
- Docs: `site/README.md`, `CONTRIBUTING.md` (refreshing media, the app), `AGENTS.md` (commands).
