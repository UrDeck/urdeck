## Context

See `proposal.md` for the motivation and `specs/website/spec.md` for what the site must do. What shapes the approach:

- The owner wants a landing page that looks striking, made with Claude Design, and a small configuration section. Most
  of `docs/` (backlog, handoffs, measurements, design notes) is not for the site.
- `urdeck.app` is registered and has no DNS records. `.app` is on the HSTS preload list: browsers refuse plain HTTP on
  it, so the site does not load at all until GitHub has issued a certificate.
- GitHub Pages is not enabled on the repository. `main` takes changes only through squash-merged pull requests with
  required checks, restructured by `ci-path-filters` (a `changes` job, gated jobs, `ci-ok`).
- `UrDeck.Host.exe --snapshot` renders a page on the CPU with the configured theme and background, waits for the
  providers to report once and for icons, and reads the config next to the executable.
- The project's first promise is privacy, so the site must not do to a visitor what the application refuses to do.

## Goals / Non-Goals

**Goals:**

- A site that is correct and complete in a plain style, so the design can replace the style without touching content.
- One place for each fact: the schema, the sample decks and the hardware list each have one source file.
- A site-only pull request runs no .NET job.

**Non-Goals:**

- Previews of a pull request's site. Pages has none; the build is checked and the look is checked locally.
- A documentation theme with search and versions. Add one (Starlight is the candidate) when the pages outgrow a
  hand-made navigation.
- Moving `docs/backgrounds.md` and `docs/themes.md`. `AGENTS.md`, the tool's messages and the lint codes point at them.
  The site's pages are written for users and link to those files for the reference detail, until a later change
  decides where the reference lives.
- Running the built-in shaders in the browser. It is an idea for the design step (decision 8), not a requirement.

## Decisions

### 1. Astro, in `site/`, with npm

Astro puts no look of its own on a page: a page is HTML, CSS and whatever script it brings, so a design from Claude
Design drops in as it is, and Markdown pages cover the configuration section. Its output is static files, which is all
Pages serves. `site/` holds its own `package.json`, a committed `package-lock.json` and an `.nvmrc`; nothing outside
`site/` needs Node, and `node_modules/` is already git-ignored.

*Rejected:* a .NET generator (Statiq, docfx), to keep one toolchain: docfx is a documentation tool with a fixed shape
and Statiq has a small community; neither helps with a designed landing page. *Rejected:* Docusaurus or VitePress: a
documentation site first, a landing page second, the opposite of what is wanted. *Rejected:* hand-written HTML with no
generator: the navigation, the Markdown pages and the link check would be written by hand.

### 2. A plain style first, the design second

The change lands in two pull requests:

1. Structure and content: every page, the copy, the pictures, the schema, the deployment, in a plain readable style.
2. The design, from Claude Design, applied to that structure.

The brief for Claude Design is this proposal, the copy as merged, the rendered decks and the `glass` theme's palette.
The first pull request puts the site online and proves the pipeline before any time goes into the look; the second
changes styles and layout components, not content files. Layout and style live in `site/src/layouts` and
`site/src/components`, content in `site/src/content` and `site/src/data`, so the second pull request stays out of the
first one's files.

### 3. Layout and addresses

```
/                               landing page
/docs/                          configuration: overview and the config file
/docs/widgets/<name>/           clock, stats, weather, shortcut
/docs/themes/
/docs/backgrounds/
/tested-on/
/schemas/v1/shader-manifest.json
```

Addresses end in a slash and are lower-case. `/docs/` is where author documentation joins later, as `/docs/sdk/`.
`/schemas/` is a promise (the spec's "Schema Address"); the other addresses may still move before the first release.

### 4. Pages from GitHub Actions, with the build in two places

- **Check:** a `site` job in `ci.yml`, gated on a `site` filter group, runs `npm ci` and the build on `ubuntu-latest`
  and is added to `ci-ok`'s `needs`. The filter group is `site/**`, `docs/schemas/**` and the workflow files.
- **Publish:** `.github/workflows/site-deploy.yml` runs on a push to `main` that touches the same paths, and on
  `workflow_dispatch`. It builds with `withastro/action` (`path: site`) and deploys with `actions/deploy-pages`, with
  `pages: write` and `id-token: write` on the deploy job only. A workflow-level `paths:` filter is right here because
  no required check lives in this workflow.

The Pages source is "GitHub Actions": no `gh-pages` branch, nothing generated is committed.

*Rejected:* deploying from `ci.yml`: the write permissions would sit in the workflow that runs on pull requests.
*Rejected:* Cloudflare Pages, which has previews: a second account and service for a project whose point is having few
of them. Revisit if previews are missed.

### 5. The domain

- `site/public/CNAME` holds `urdeck.app`; Astro's `site` is `https://urdeck.app` and there is no `base`.
- **(owner)** DNS: the apex with GitHub's four `A` and four `AAAA` records, `www` as a `CNAME` to `urdeck.github.io`.
- **(owner)** Verify `urdeck.app` for the `UrDeck` organisation before the records go in, so nobody else can claim the
  name on Pages while it points at GitHub.
- **(owner)** Enable Pages with the Actions source, set the custom domain, turn on "Enforce HTTPS" once the
  certificate is there.

Order: verify, deploy once to the `github.io` address, add DNS, set the domain, wait for the certificate.

### 6. One source for the schema

The build copies `docs/schemas/**` into the output under `/schemas/`. The file is not duplicated into `site/public/`:
`BackgroundPackageTests` and the tool read the one in `docs/schemas/`, and a copy would drift. A build step compares
the copied file with the source and fails on a difference. A JSON file gets a JSON content type from Pages by its
extension.

### 7. Sample decks

- `site/decks/<name>/urdeck-config.json` holds one config per sample deck: at least `default-dark` with no background,
  `glass` over a moving shader, and `default-light` or a picture background. Each uses a neutral weather location and
  shortcuts to well-known public sites or Windows items, never the owner's.
- `scripts/Render-SiteDecks.ps1` builds the host, copies the build output to a temporary folder (the host build
  overwrites a config in `bin/`, and `--snapshot` reads the config next to the executable), puts each deck's config
  there, runs `--snapshot --size 1100x3840` and writes `site/src/assets/decks/<name>.png`.
- The PNGs are committed. CI does not render them: it would need the Windows build, the weather provider needs the
  network, and a CPU render is not the same on every machine. Astro makes the smaller sizes and formats at build time.
- The readings in a picture are those of the machine that rendered it. They show load and temperatures, nothing that
  identifies the machine; check each picture before committing it.

A short clip of the deck in motion on the real panel is the owner's to record, if the design wants one.

### 8. Motion, script and privacy

- The site ships no analytics and no third-party embed. Fonts are files in `site/` (Inter is already in the repository
  under the OFL). A check in the build greps the output for `http://` and `https://` references in `src`, `href` of
  `link`, and `url()` that are not `urdeck.app` or a plain anchor, and fails on one.
- Any moving element is an enhancement over a still picture that is in the HTML, and is off under
  `prefers-reduced-motion`. That is what lets the design step try the built-in shaders in WebGL (SkSL is close to
  GLSL; the port is untried) without the spec depending on it.

### 9. Copy

The copy is the owner's voice, first person, as agreed while exploring. Rules for it:

- It does not name or describe any other dashboard software. Hardware is named on "Tested on" only, with the trademark
  line.
- Privacy is stated as what happens: no account, no telemetry, and the two kinds of request UrDeck makes, both for
  something the user put on the page.
- Performance is described by how it is built (native, drawn on the GPU, redraws only what changed). No number and no
  comparison until there is a repeatable measurement of both sides.
- "No browser inside" is not claimed: the planned web page widget brings one.

### 10. Hardware list and report

`site/src/data/tested-on.json` holds the entries; the page is made from it, so a new report is a one-entry pull
request. `.github/ISSUE_TEMPLATE/hardware-report.yml` is an issue form asking for the display and how it is connected,
the GPU, the Windows version, the UrDeck version or commit, what works and what does not, and the lines `urdeck.log`
writes at startup (monitors, the display chosen, the background surface, the source of each reading).

### 11. The README

The README keeps what it is, a screenshot, the quick start from source and the licence map, and links to the site for
configuration. The configuration reference moves to the site in the same pull request that removes it from the README,
so it never exists twice.

## Risks / Trade-offs

- [The site is down between adding DNS and the certificate] → Nothing depends on the address yet; do it once, in the
  order of decision 5.
- [Configuration pages go stale when a widget's settings change] → `AGENTS.md` and the pull request template name
  `site/src/content/docs/` beside the README as a place a behaviour change must update. Generated pages are a later
  change.
- [Two documentation homes: `docs/backgrounds.md` and the site's backgrounds page] → The site page is the short user
  version and links to the reference; stated as a non-goal to resolve here.
- [The dependency review rejects an npm package's licence] → The site's packages are build tools, not shipped with
  UrDeck; if the check fails on one, scope the licence rule to the NuGet graph and keep the vulnerability rule on both.
- [npm brings a large dependency tree into a small project] → Astro alone, no UI framework, no CSS framework unless the
  design needs one; Dependabot groups the updates weekly.
- [A design from Claude Design needs a script-heavy effect] → The spec's "Usable Without Script Or Motion" is the
  bound: any effect is allowed that leaves the page whole without it.

## Migration Plan

1. `ci-path-filters` is merged and the ruleset requires `ci-ok`.
2. Pull request 1 (structure and content) merges; the owner enables Pages and the site is reachable at the
   `github.io` address.
3. The owner does the domain steps of decision 5.
4. Pull request 2 (the design) merges.

Rollback: disable Pages or revert the pull request; the application does not depend on the site (the tool embeds its
own copy of the schema).

## Open Questions

- Whether the landing page shows a clip of the deck in motion, and where the file is hosted if it is large. It is the
  design step's question and does not change the structure.
- Whether `www` should be the canonical name instead of the apex. The apex is assumed; switching is a DNS and `CNAME`
  file change.
