# Handoff: site media automation, theme picker on the site, docs (PR 17 and PR 18)

**For:** fresh sessions that build the four OpenSpec changes listed under "The changes", in order, one per session, with
`/opsx:apply <change>`. All four are proposed (proposal, specs, design and tasks in `openspec/changes/<change>/`). The
decisions below were settled in an explore on 2026-10-22 and are recorded in those changes; this file is the index.
**Date:** 2026-10-22. **Read first:** `AGENTS.md`, `docs/design/look-and-feel/STATUS.md` (rows PR 17 and PR 18),
`docs/design/look-and-feel/07-pairing-and-editor.md` §3, `08-cli-validation-docs-site.md` §3 and §4, `site/README.md`,
`site/MEDIA-CHECKLIST.md`.

## The goal

Creating, changing or deleting a widget, theme or background regenerates every picture and loop the site needs, and the
site follows, from CI jobs, with no file ticked by hand and no picture that needs a human to check it for personal data.
The site gets a floating theme picker: choose a theme and every picture on the page shows that theme.

## Where it stands today

| Media | Made by | Data | Runs on | Coverage |
|---|---|---|---|---|
| Deck pictures (11, 1100x3840) | host `--snapshot`, `scripts/Render-SiteDecks.ps1` | live readings, live weather, wall clock | Windows | every theme |
| Widget pictures and loops | host `--snapshot`, `npm run media` | live readings, network | Windows | glass only; no stats or shortcut loop |
| Background posters and loops | `urdeck-cli shader render`, `npm run media` | deterministic | Windows, Linux | 4 of 16, glass colours |
| (not on the site) | `urdeck-cli theme render` (`ThemePreview`, `SamplePage`) | fixed readings and time | Windows, Linux | any theme, no background layer |

The "look at each picture before committing" warnings exist only because two of the paths render live data. The site's
`themes.json`, `backgrounds.json` and `widgets.json` copy what the engine and the widgets already say, and are stale
(`backgrounds.json` lists four, "the heaviest of the four").

The package planned PR 17 (site) before PR 18 (`PreviewRenderer`). `07` §3 already describes `PreviewRenderer` as the
one path for theme + background + sample page at a time, and `08` §4.1 wanted the site decks on fixture values. What was
built drifted: `SamplePage` is C# with hard-coded values, and the decks use the live host. **Reorder: the renderer first;
the site is its first user and the future editor its second.**

## Decisions

1. **Deterministic rendering only.** Every site picture comes from sample data and a fixed time. The host's live
   `--snapshot` stops feeding the site (it stays a development tool, and gains demo mode, decision 3).
2. **Sample data lives in a data provider sidecar, not a widget sidecar.** Each provider ships `sample.json` (embedded
   resource, like the built-in backgrounds): for each reading a value, or keyframes over time. A `SampleProvider` replays
   it behind `IDataProvider`, taking `Describe()` from the real provider, so `ReadingHub`, subscriptions, formatting and
   the stats history run unchanged. Per-place readings use a wildcard (`weather:{place}/...` answers any place and echoes
   its name). The weather sample covers the forecast days (a 4x4 card shows five; `SamplePage` has only today). A test
   requires the sidecar to cover every reading `Describe()` lists, with a value of the right kind. A widget that fetches
   its own data in `UpdateAsync` gets no sample data: documented as "take data through a provider to get a preview".
3. **Demo mode in the host.** A switch (`--sample-data`, or a config value; decide in the proposal) runs the real host
   with the sample providers. It makes host snapshots deterministic too, and is meant to be reused by a theme picker UI
   in the app later.
4. **The showcase is derived, with optional site overrides.**
   ```
   showcase config  =  site override (optional)  ??  widget.DefaultConfig
   readings         =  the provider sample sidecars
   sizes            =  every [WidgetSize] the widget declares
   copy             =  site override (optional)  ??  [Widget] name and description
   ```
   A widget with good defaults shows up on the site with no file of its own. The site keeps one overrides file (today's
   `widgetPictures` table in `site/scripts/render-media.mjs` and `widgets.json`, merged): the split-flap clock, a named
   place, shortcut targets, alt text.
5. **A widget's `DefaultConfig` must render something meaningful**, with tests. Today no widget overrides it: Stats has
   no slots, Weather no place, Shortcut no target. The same defaults serve the future editor's "add a widget".
6. **Every declared size is pictured** (about 130 widget pictures over 11 themes). Add an optional category to
   `[WidgetSize]` so a future widget with long repeating content can opt a size out of the preview.
7. **`sample-page.json` replaces the C# `SamplePage`** as the one description of the sample page (layout and configs;
   readings come from the provider samples). Goldens, `theme render`, the site and the editor read it. Breaking changes
   are fine (pre-1.0, no consumers); the goldens should come out byte-identical, and say why if not.
8. **Render on Linux in CI.** Spike on 2026-10-22: `urdeck-cli theme render` for carbon, rigline, paper, obsidian, pop
   and glass in `mcr.microsoft.com/dotnet/sdk:10.0` against Windows differs only at glyph edges (about 1 % of pixels,
   mean 0.6 to 0.9 levels; Rigline 7 % from faint glow halos, under 0.6 % by more than 32 levels). Small text is a pixel or
   two tighter (FreeType advances). The container has no system fonts and needs none: every theme uses bundled fonts. The
   owner accepts the difference. Not spiked: shader backgrounds (`shader render` already runs on Linux) and icons (no
   shell on Linux; sample shortcuts use bundled PNGs or `theme:<key>` glyphs).
9. **The site shows the same page in every theme.** The picker compares themes on one showcase page. A
   `site/decks/<theme>/urdeck-config.json`, when present, overrides it for that theme's hero picture ("the theme as
   designed"); a new theme needs no site file.
10. **Site data comes from the engine.** `urdeck-cli catalog export --json` writes what `ThemeCatalog`,
    `BackgroundCatalog`, `PairingIndex` and the widget descriptors know (names, `meta`, swatch colours, `pairsWith`,
    default background, manifests, sizes) for the Astro build. Node does not parse theme files (the engine reads them
    leniently). `themes.json`, `backgrounds.json` and `widgets.json` go away, except the overrides of decision 4.
11. **Backgrounds follow pairing.** All 16 backgrounds get a poster and a loop in their own pairing's palette (not 16 x 11).
    Choosing a theme in the picker marks the backgrounds it pairs with ("pairs with Ember") and shows the theme over its
    default background in the hero. No combination the themes were not designed for is shown.
12. **The theme picker is a website feature,** not the app's. STATUS says "no theme picker for now" about the
    configuration editor; this one does not conflict, and it exercises the editor's seams before the editor exists. Static
    site: the build emits a map slot -> theme -> optimised URL; a small script swaps `src` and `poster`; the choice is kept
    in `localStorage` and a `?theme=` link; without script the page shows glass, as today. Swatches come from the theme's
    colours. Restyling the site's own accents with the theme is optional (a light theme turns the page light).
13. **The media job is manual for now.** `site-media.yml` on `workflow_dispatch`: on Linux, renders what is stale and
    opens a pull request "chore(site): refresh media" with the pictures to review. The generator owns its output folder
    completely, so a deleted theme or widget deletes its files in that pull request. `ci.yml` gets a cheap, non-blocking
    staleness check (input hashes, no render) that says how many files are out of date. **Later**, with semver releases,
    the job runs on major and minor release tags (`v*.*.0`), not patches. Then decide whether the whole site deploys from
    release tags too: today a merge touching `site/` deploys, so docs could describe what the pictures do not show yet.

## The animated border is not in the page renderer

Rigline's turning border and Ember's glow are drawn by the host's compositor (`BorderMotion`, sprites), which is why
snapshots and goldens did not change with `motion`. A theme loop needs `PreviewRenderer` to draw the border per frame from
`BorderPicture` at the host's rotation (15 steps a second). The requirement to write down: frames match what the host
shows, not byte for byte the compositor. The underlay and the pattern already share one routine with the window.

## The changes, in order

Apply one per session, in this order; each change's first task checks that the one before it is archived. Every change
has "owner's look" tasks that stop and ask; do not tick them without the owner.

| # | Change | Tasks | Delivers | Model for `/opsx:apply` |
|---|---|---|---|---|
| 1 | `preview-renderer` (done, pending the pull request) | 31 | Provider sample data (`sample.json`, `SampleProvider`), the hub's manual sampling mode, the preview renderer with frames (widgets, background and animated border move), the sample page as `urdeck-config.json`, host `--demo`, meaningful `DefaultConfig`s with tests, `[WidgetSize(Preview = false)]`, `urdeck-cli preview render` and `catalog export` (with widget settings and finding codes) | **Opus**: cross-cutting engine work (reading hub, page renderer, border composition matching the compositor, goldens that must stay byte-identical) |
| 2 | `site-media-pipeline` | 21 | The media matrix from the catalog, rendering on Linux with input hashes, the generated folders and manifest, `site-media.yml` (manual, GitHub App token, refresh PR), the non-blocking staleness report, the first full generation | **Sonnet**: well specified scripting and workflow; Opus only if the input-key design needs rework |
| 3 | `site-gallery` | 17 | The floating theme picker (pictures and accents), the theme gallery, all sixteen backgrounds with pairing, pages built from the catalog | **Sonnet**: front-end work against a precise design |
| 4 | `docs-rewrite` | 22 | User docs only on the site, reference tables generated from the schemas and the catalog, a build that fails on gaps, the eight-recipe cookbook | **Sonnet**: mostly moving and rewriting prose; the table components are small |

Owner setup before change 2's workflow can run: a GitHub App with contents and pull request write on the repository, its
id and private key as the secrets `URDECK_MEDIA_APP_ID` and `URDECK_MEDIA_APP_KEY` (`CONTRIBUTING.md` after change 2).

The open questions of the explore were all decided in the changes' designs: `--demo` is a command-line switch only, the
size opt-out is `Preview = false`, the sample page and the site's showcase are `urdeck-config.json` files with a
`site/showcase/widgets.json` for site-only widget settings, the picker changes pictures and accents (not fonts or light
mode), the sample shortcuts keep their letter tiles, loops are 3 to 6 seconds with a sparkline pre-roll.

## Media size

A full refresh, when every picture changes, is roughly 40 to 60 MB. Three things keep the real growth much lower, and the
plan stays with plain git until the repository is about 1 GB:

- Output is deterministic: a re-rendered file whose pixels did not change has the same bytes, and git stores nothing new.
  Only a change that moves pixels adds weight. Pin the ffmpeg version on the runner so loops do not re-encode differently.
- Refreshes are manual now and tied to major and minor releases later.
- Pictures are committed as lossless WebP instead of PNG (decided; Astro optimises either), about half the size; the
  first generation records the real sizes.

Git LFS is not the fallback: on the free plan it is 10 GB of storage and 10 GB of bandwidth a month, and every checkout with
LFS files (CI jobs, each Pages deploy) counts against the bandwidth; LFS objects also cannot be deleted without deleting the
repository or asking GitHub support. If the repository outgrows plain git, the fallback is to stop committing generated media:
the refresh workflow uploads the generated folders as one archive to a GitHub release (`site-media`), and the deploy job
downloads it before `astro build`.

When all four changes are archived, the last task of `docs-rewrite` deletes this file.
