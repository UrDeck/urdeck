## 1. Groundwork

- [x] 1.1 `preview-renderer` is archived; on `feat/site-media-pipeline` the build, tests and `npm run build` in `site/` pass
- [x] 1.2 Read `site/scripts/render-media.mjs`, `site/src/lib/media.ts`, the showcase components, `scripts/Render-SiteDecks.ps1`, `ci.yml` (`test-linux`, `site`), `site-deploy.yml`, and the `preview render` and `catalog export` options

## 2. Showcase

- [x] 2.1 `site/showcase/urdeck-config.json` (design, decision 5) and `site/showcase/widgets.json` with the clock's `flap` style and `loopStart`; delete `site/decks/`
- [x] 2.2 A test fixture for a per-theme `site/decks/<theme>/urdeck-config.json` override (in the generator's tests)

## 3. Generator

- [x] 3.1 Plan: run `catalog export`, read the overrides, build the matrix (`site-media` spec, "The Media Matrix", including the background colour rule), compute input keys (design, decision 3)
- [x] 3.2 Render: decks, widgets (with the move probe of decision 4), backgrounds; `sharp` for cuts and lossless WebP; ffmpeg loops forwards-then-backwards; first frame equals the picture
- [x] 3.3 Limits (`site-media` spec, "Media Limits") with the one retry at a higher `-crf`; a failure names the file, its size and the limit
- [x] 3.4 Write: generated folders, delete unneeded files, `media.json` and `catalog.json` sorted with a final newline
- [x] 3.5 `--status`, `--all` and the kind/name filters; the report format (missing, stale, unneeded, with lists)
- [x] 3.6 Generator tests with Node's test runner (`node --test`), on a stub catalog and a fake renderer: matrix for added/removed theme, widget, size and `Preview = false`; keys change only for the right entries; a second run renders nothing; unneeded files deleted; a script `npm test` in `site/package.json`

## 4. Site reads the manifest

- [x] 4.1 `site/src/lib/media.ts` resolves decks, widgets and backgrounds from `media.json` (design, decision 8); today's pages keep working with the glass entries
- [x] 4.2 `check-output.mjs`: every file `media.json` names exists in the output

## 5. CI and workflow

- [x] 5.1 `.github/workflows/site-media.yml` (design, decision 6), actions pinned by SHA; a clear failure when the app secrets are missing
- [x] 5.2 The staleness step in `test-linux` and in `site` (design, decision 7), non-blocking, counts in the summary; `npm test` of the generator in the `site` job
- [x] 5.3 Remove `scripts/Render-SiteDecks.ps1` and `site/MEDIA-CHECKLIST.md`

## 6. First generation

- [x] 6.1 `npm run media -- --all` in a `mcr.microsoft.com/dotnet/sdk:10.0` container with Node 22 and ffmpeg; delete the old `site/src/assets/{decks,widgets,backgrounds}` and `site/public/media/`; record the run time and the total size in `site/README.md`
- [x] 6.2 Owner's look at a contact sheet of every generated picture and a few loops (stop and ask; do not tick without them)
- [x] 6.3 A second run reports nothing stale

## 7. Verification

- [x] 7.1 `npm run build` in `site/` with its checks; `dotnet build urdeck.slnx -c Release`; `openspec validate --all --strict`
- [ ] 7.2 After merge and the owner's app setup: the workflow run by hand reports "Media are current"; then a throwaway branch that changes one theme colour, the workflow on it opens a pull request whose required checks run (close it unmerged)

## 8. Documentation

- [x] 8.1 `site/README.md` (the generator, the folders, the manifest, the showcase, the size risk), `CONTRIBUTING.md` (refreshing media, creating the GitHub App and its secrets), `AGENTS.md` (commands: `npm run media`, `--status`), `docs/design/look-and-feel/STATUS.md` (PR 17 row)
