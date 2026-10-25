## Context

See proposal.md. Releases are made by `release.yml` as a draft and published by the owner; assets include
`UrDeck-win-Setup.exe` and `UrDeck-win-Portable.zip`. The site is Astro, built by `site-deploy.yml` on pushes to `main`
that touch `site/`; `check-output.mjs` verifies the built output. `signing-and-store` (not yet implemented) plans the same
download control behind a signed-release gate.

## Goals / Non-Goals

**Goals:** a working download button now; the latest stable release shown without a site edit; a build that never breaks
because GitHub is unavailable.

**Non-Goals:** client-side JavaScript calls to GitHub; showing prereleases; the Store link, signing policy and privacy
pages (those stay in `signing-and-store`).

## Decisions

**1. The button's URL is the permanent latest-release address, not a URL read from the API.**
`https://github.com/UrDeck/urdeck/releases/latest/download/UrDeck-win-Setup.exe` is resolved by GitHub on click to the
latest stable (non-draft, non-prerelease) release. Even a stale site therefore never serves an old file. Alternative: link
the asset's `browser_download_url` from the API; rejected, it goes stale between a release and the next deploy.

**2. The API is used only for the label** (version, date, release-notes URL, portable zip URL), fetched at build time by
`site/src/lib/release.mjs` from `GET /repos/UrDeck/urdeck/releases/latest` (which already excludes drafts and prereleases).
No browser request, so the visitor makes no network request before clicking. `GITHUB_TOKEN` is sent when present
(rate limit in CI); anonymous otherwise.

**3. Degrade, do not fail, on a missing answer.** A network error, 403/429, or 404 (no stable release yet) yields
`null`; the page then renders "All releases" to `/releases` with no version. A *successful* response whose assets lack
`UrDeck-win-Setup.exe` fails the build, since the permanent link would 404. The fetch and the choice are one pure function
over the parsed response, unit-tested with fixtures (`node --test`, like the other site libs).

**4. Rebuild on release.** `site-deploy.yml` gains `on: release: types: [published]` (published drafts fire it; a
`GITHUB_TOKEN`-created release does not, but the owner publishes by hand), and the checkout is pinned to `main` because a release event runs on the tag's commit and passes `GITHUB_TOKEN` to the build step. A
`workflow_dispatch` remains the manual fallback.

**5. `check-output.mjs`** asserts the landing page contains the permanent link, and that no "coming soon" download text
remains.

**6. Unsigned honesty.** A one-line SmartScreen note stays until `signing-and-store` ships; that change then removes the
note and adds the Store link on top of this control. Its `website` delta must be rebased: this change's "Download State"
becomes the base it modifies.

## Risks / Trade-offs

- Site shows an old version label until the deploy finishes, but the file served is already new (Decision 1).
- Rate limits during build → label omitted; page still correct.
- Two active changes touch one requirement → ordering noted in tasks; archive this one first.
