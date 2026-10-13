## Why

UrDeck has no public face: a visitor gets a long README, and `urdeck.app`, which the owner bought, resolves to nothing.
Four built-in background manifests already name `https://urdeck.app/schemas/v1/shader-manifest.json` as their schema,
an address nothing serves. A landing site shows the project off, says why it exists, holds the configuration pages a
user needs until the editor exists, and makes that address real.

## What Changes

- A website in `site/` (Astro), published to GitHub Pages at `https://urdeck.app` from GitHub Actions.
- **Landing page:** what UrDeck is, why it exists (privacy first), sample decks rendered by the application itself, a
  link to the repository, and a download button that says "coming soon" and offers no file until releases are signed.
- **Configuration pages:** the config file, widgets, themes and backgrounds, written for a user. They become the
  canonical user documentation; the README shrinks to a quick start that links to them.
- **"Tested on" page:** the hardware UrDeck has been run on, starting with one row (HYTE Y70 Touch), what is expected
  but untested, what does not work, and how to send a report.
- **Hardware report issue template** (`.github/ISSUE_TEMPLATE/`), asking for the lines `urdeck.log` already writes.
- **The schema** is served at `/schemas/v1/shader-manifest.json`, from the one file in `docs/schemas/`.
- **The site itself is private by construction:** no analytics, no cookies, no request to a third party.
- CI: a `site` job in the `ci-path-filters` structure builds the site on a pull request; a separate workflow deploys on
  a push to `main`. Dependabot gets an `npm` entry for `site/`.
- **(owner)** DNS for `urdeck.app`, domain verification for the `UrDeck` organisation, and enabling Pages.

The visual design is made in Claude Design from this proposal, the copy and the rendered decks, and is applied in a
later step of the same change. The site is useful, and correct, with a plain style before that.

Not in this change: a working download and the release workflow (milestone 2, with code signing), author
documentation for the SDK (milestone 5), generated documentation (`shader docs`), versioned documentation, a blog.

## Capabilities

### New Capabilities

- `website`: the public site at `urdeck.app`: what its pages hold, the addresses it promises, what it never does to a
  visitor, and how it is published.

### Modified Capabilities

None. `background-manifest` already names the schema address; this change serves it and changes no requirement there.

## Impact

- New: `site/` (an Astro project with its own `package.json` and lockfile), `.github/workflows/site-deploy.yml`,
  `.github/ISSUE_TEMPLATE/hardware-report.yml`, `scripts/Render-SiteDecks.ps1`.
- Changed: `.github/workflows/ci.yml` (a `site` filter group and job, added to `ci-ok`), `.github/dependabot.yml`,
  `README.md` (configuration reference moves to the site), `AGENTS.md` and `CONTRIBUTING.md` (layout, commands),
  `docs/ROADMAP.md` and `docs/BACKLOG.md`.
- Node and npm become a build dependency of the repository, for `site/` only. The .NET build does not use them.
- The dependency review in `security.yml` starts judging npm packages and their licences.
- Depends on `ci-path-filters` being merged first.
- Outside the repository: DNS records for `urdeck.app`, the organisation's verified domains, the repository's Pages
  settings.
