## Context

See proposal.md for why. Depends on `preview-renderer` (archived first): `urdeck-cli preview render` (pages, a widget
alone, frames, `--background theme`, `--motion`), `urdeck-cli catalog export`, sample data, `[WidgetSize] Preview`.

Today: `site/scripts/render-media.mjs` builds the host and renders widget pictures with `--snapshot` (live readings,
Windows), background posters and loops with `urdeck-cli shader render`, and encodes loops with ffmpeg (forwards then
backwards, `-crf 28`, `+faststart`). `scripts/Render-SiteDecks.ps1` renders `site/decks/<theme>/urdeck-config.json` with the
host. `site/src/lib/media.ts` finds files by name in `src/assets/{widgets,backgrounds,decks}` and `public/media/`. About
7.6 MB of media is committed. `ci.yml` has `changes`, `build` (Windows), `test-linux`, `openspec`, `site`, `ci-ok` and
`pr-title`; actions are pinned by commit SHA. `site-deploy.yml` deploys on a push to `main` that touches `site/`.

The owner decided (explore, 2026-10-22): one page for every theme with optional per-theme override; Linux; a manual
workflow now, on major and minor release tags later; a GitHub App token for the pull request; backgrounds in their
pairing's colours, not 16 x 11.

## Goals / Non-Goals

**Goals:**
- A theme, background, widget or size added or removed needs no site file to get its media.
- A refresh is one button and one pull request to review.
- Re-running with nothing changed changes nothing.

**Non-Goals:**
- How pages show the media (`site-gallery`).
- A release-tag trigger (no releases exist; the workflow keeps `workflow_dispatch` so the trigger is one line later).
- Git LFS (measure first; see Risks).

## Decisions

### 1. The generator stays a Node script in `site/`

`site/scripts/render-media.mjs` is rewritten around three steps: **plan** (run `catalog export`, read the overrides,
compute the matrix and each entry's input key), **render** (only stale entries: `preview render` into a temporary folder,
`sharp` for any cut and the lossless WebP encoding (the tool writes PNG; lossless keeps every pixel, so a picture is still the tool's output), `ffmpeg` for loops), **write** (move into the generated folders, delete what
the matrix no longer names, write `media.json` and `catalog.json`). It calls the tool with `dotnet <path>/urdeck-cli.dll`
after one `dotnet build tools/UrDeck.Cli -c Release`. Flags: `--status` (plan only, print the report), `--all` (ignore keys),
and kind/name filters as today (`decks`, `widgets`, `backgrounds`, and names).

*Alternative:* a `urdeck-cli site media` command in C#. Rejected: ffmpeg and sharp orchestration and the site's file layout
are the site's concern, and Node is already the site's toolchain; the C# tool stays about the product.

### 2. Layout of the generated folders

```
site/src/generated/
  catalog.json                       from catalog export
  media.json                         manifest: entries[] {id, kind, theme, widget, size, background, files, key, settings}
  decks/<theme>.webp                 1100x3840
  widgets/<widget>-<WxH>-<theme>.webp
  backgrounds/<background>.webp      540x960 poster
site/public/generated/
  decks/<theme>.mp4
  widgets/<widget>-<WxH>-<theme>.mp4
  backgrounds/<background>.mp4
```

Pictures go under `src/` so Astro optimises them; loops under `public/` (served as they are). `media.json` records the exact
`preview render` options of each file (`settings`), which is what the website spec's "Reproducing a picture" points at.
Keys and lists are sorted and the file ends with a newline, so a refresh diff is small.

### 3. Input keys: content hashes, shared plus specific

An entry's key is SHA-256 over two parts:

- **Shared:** every file under `src/UrDeck.Engine`, `src/UrDeck.Preview`, `sdk/UrDeck.Sdk`, `widgets/` and
  `providers/*/sample.json` (excluding `bin/`, `obj/`, and the built-in theme and background folders
  `src/UrDeck.Engine/Themes/Builtin/` and `src/UrDeck.Engine/Backgrounds/Builtin/`), plus a settings version constant in
  the generator
- **Specific:** the theme's JSON and the font files it names, the background's folder, the widget's configuration as
  rendered (default or override), the showcase or deck configuration, and the entry's render settings

The built-in folders are left out of the shared part so that changing Ember's file stales only the entries that use Ember
("a theme changed" in the spec); each entry adds the theme and background files it uses to its specific part.

Content hashes rather than git tree hashes, so uncommitted local changes count and the CI and a laptop agree.

*Alternative:* hash the built `urdeck-cli` output. Rejected: build output is not reproducible byte for byte across
machines, so every run would look stale.

### 4. What moves, and loops

- **Deck loops:** made when the theme has an animated border, or its chosen background is a moving shader. 5 s at 30 fps,
  scaled to 540 wide.
- **Widget loops:** made when the widget's media differ between frame 0 and the last frame of a 3-second probe at 10 fps
  (cheap, decided per widget and size, independent of theme), then rendered for every theme. This picks up the weather
  icons, the stats keyframes and, with the override's `loopStart`, the clock's flip.
- `site/showcase/widgets.json` may give, per widget (and optionally per size): `config` (merged over the default
  configuration) and `loopStart` (seconds from the sample moment). The clock gets `style: flap` and a start 1.5 s before a
  minute; that is the only override shipped.
- Loops play forwards then backwards (as today), H.264, `yuv420p`, `+faststart`, no audio; `-crf` starts at 28 and the
  generator retries at 32 once if a file is over its limit, then fails.

### 5. The showcase page

`site/showcase/urdeck-config.json` is today's glass deck (split-flap clock 4x2, weather 4x4, stats 4x4 and 4x2, a dock of
four shortcuts) with sample-friendly shortcut targets (web addresses, no Windows paths), no `theme` and no `background`
(the generator passes `--theme` and `--background theme`). The eleven `site/decks/*` folders are deleted; the override
path stays supported and tested with a fixture.

### 6. The workflow

`.github/workflows/site-media.yml`, `on: workflow_dispatch` with an input `all` (boolean). One job on `ubuntu-latest`:
checkout, `actions/setup-dotnet` (10), `actions/setup-node` (22, the site's `.nvmrc`), `sudo apt-get install -y ffmpeg`,
`npm ci` in `site/`, `npm run media` (with `--all` when asked). If `git status --porcelain site/src/generated
site/public/generated` is empty, write "Media are current" to the summary and stop. Otherwise mint a token with
`actions/create-github-app-token` (pinned SHA; secrets `URDECK_MEDIA_APP_ID`, `URDECK_MEDIA_APP_KEY`), push branch
`chore/site-media-<run number>`, and open the pull request with `gh pr create` using that token. The body is the
generator's report (added, changed, removed per kind) and ends with the repository's PR attribution line. The commit author
is the app's bot user. `permissions: contents: read` for the job (the app token does the writing).

*Alternatives:* a PAT (expires, acts as the owner) or pushing a branch for the owner to open (a click per refresh). The owner
chose the app.

### 7. Staleness in CI

A step at the end of `test-linux` (it already builds the CLI): `npm ci` and `npm run media -- --status` in `site/`, with
`continue-on-error: true`, writing the counts to `$GITHUB_STEP_SUMMARY` and one `::notice::`. It needs Node in that job
(setup-node, cached). It runs only when the job runs (product changes); a site-only change that edits the showcase is
covered by also running the step in the `site` job, which has Node but not .NET: there the report uses the committed
`catalog.json` and says so.

### 8. `media.ts` reads the manifest

`site/src/lib/media.ts` imports `media.json` and resolves `deck(theme)`, `widget(widget, size, theme)`,
`background(name)` to the optimised image and the loop URL. Missing entries return undefined (the page leaves them out, as
today). `site-gallery` builds on these functions; this change keeps today's pages working with the glass entries.

## Risks / Trade-offs

- [Repository growth: about 120 widget pictures, 11 decks and 120 loops per theme refresh, roughly 40 to 60 MB when
  everything changes] → unchanged pixels give unchanged bytes, so git stores only what really moved (ffmpeg's version is
  pinned on the runner so loops re-encode identically, and `sharp` by the lockfile); refreshes are manual and later tied to releases; pictures are lossless WebP, about half the size of PNG
  (the first generation records the sizes). Not Git LFS (free plan: 10 GB
  of bandwidth a month counted on every CI checkout and Pages deploy, and objects cannot be deleted). If the repository
  nears 1 GB, the fallback is a release asset holding the generated folders, downloaded by the deploy job. Recorded in
  `site/README.md`.
- [The shared hash makes every engine change stale everything] → correct (any engine change can move pixels); the cost is a
  longer manual run, not a wrong picture. Measured in the first run and recorded.
- [Linux text differs slightly from Windows] → accepted in the explore (spike numbers in `preview-renderer`'s design).
- [The GitHub App needs setup before the first run] → a task for the owner, documented in `CONTRIBUTING.md`; the workflow
  fails with a clear message when the secrets are missing.

## Migration Plan

1. This change's own pull request carries the first full generation, run with `--all` in a Linux .NET container, and
   deletes the old `site/src/assets/{decks,widgets,backgrounds}` and `site/public/media/`, so the site is never without
   media.
2. The owner creates the GitHub App and its secrets (documented in `CONTRIBUTING.md`).
3. The first manual run of the workflow after the merge should report "Media are current"; that is the workflow's
   acceptance test.

Rollback: revert the change; the old asset folders come back with it.
