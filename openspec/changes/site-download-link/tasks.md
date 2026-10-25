## 1. Release lookup

- [x] 1.1 `site/src/lib/release.mjs`: fetch the latest stable release at build time (token when present), return version, date, notes URL, portable URL, or `null` on any failure; throw when a release lacks `UrDeck-win-Setup.exe`
- [x] 1.2 Unit tests with fixtures (stable, no release/404, rate limited, missing installer, prerelease never chosen) added to `npm test`

## 2. Landing page

- [x] 2.1 `index.astro`: hero button links the permanent latest-release address; version line, requirements, portable/notes/all-releases links; SmartScreen note while unsigned; fallback to the releases page without a version
- [x] 2.2 Replace the "coming soon" copy in the hero note and the closing section
- [x] 2.3 `check-output.mjs`: fail if the landing page lacks the permanent link or still says the download is coming

## 3. Deployment

- [x] 3.1 `site-deploy.yml`: `release: published` trigger and `GITHUB_TOKEN` passed to the build
- [x] 3.2 Document in `site/README.md` how the version label is obtained and the fallback

## 4. Verify and reconcile

- [x] 4.1 `npm ci && npm run build && npm test` in `site/`; the built page links the installer; the fallback is covered by the unit tests (the network was not blocked in a real build)
- [x] 4.2 Look at the page in a browser at desktop and phone width
- [x] 4.3 Rebase `signing-and-store`'s `website` delta and task 5.2 on this change
- [x] 4.4 `openspec validate --all --strict`
