## Why

The landing page still says "Download: coming soon" and leads to the repository, but the release pipeline is done and 0.x
releases of `UrDeck-win-Setup.exe` are published on GitHub (the README and the installing page already tell people to get
it there). A visitor to urdeck.app should be able to download the installer from the first screen, and the site should
follow new releases without anyone editing it.

## What Changes

- The landing page's primary button downloads `UrDeck-win-Setup.exe` of the latest stable release, through GitHub's
  latest-release download address, so the file is always the newest without a site change.
- Next to the button the page shows the version and date of that release, the requirements (Windows 10 or 11, x64, no
  administrator rights), links to the portable zip, the release notes and all releases, and (while releases are unsigned)
  a short note about the SmartScreen warning.
- The version, date, asset names and notes address are read from the GitHub Releases API when the site is built. If the
  API cannot be reached or there is no stable release, the build still succeeds and the button falls back to the
  repository's releases page.
- The site is rebuilt and deployed when a release is published, so the shown version stays current.
- The "coming soon" notes (hero and closing section) are replaced.
- **BREAKING (spec):** replaces the website requirement that no file is offered until a signed release exists. Releases
  stay unsigned until the `signing-and-store` change lands.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `website`: "Download State" now offers the latest stable release's installer, shows its version, and rebuilds on release;
  adds the fallback when no release is readable.

## Impact

- `site/src/pages/index.astro`, a new `site/src/lib/release.ts` (build-time fetch), `site/scripts/check-output.mjs`
  (the download link must be present), tests in `site/src/lib/`.
- `.github/workflows/site-deploy.yml`: new `release: published` trigger and a token for the API call.
- `site/src/content/docs/installing.md` and `README.md` only if wording drifts.
- Overlaps `openspec/changes/signing-and-store` (its `website` delta modifies the same requirement and its task 5.2 does
  the switch); that change's delta must be reconciled on top of this one.
- Network: build time only. The visitor's browser makes no request to GitHub until they click, so "Visitor Privacy" holds.
