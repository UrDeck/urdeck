Read `proposal.md`, `design.md` and `specs/website/spec.md` first. `ci-path-filters` must be merged before this starts.
Two pull requests (design decision 2): sections 1 to 7 on the branch `feat/landing-site`, title
`feat(site): landing site at urdeck.app`; section 8 on `feat/site-design`, title `feat(site): visual design`. Run
`openspec validate --all --strict` and the site build before pushing. Tasks marked **(owner)** need the owner: prepare
what they need, then stop and ask.

## 1. Scaffold

- [x] 1.1 Create the Astro project in `site/` with npm: `package.json`, a committed `package-lock.json`, `.nvmrc`, `astro.config.mjs` with `site: 'https://urdeck.app'`, trailing slashes on, no `base`. No UI or CSS framework
- [x] 1.2 `site/public/CNAME` with `urdeck.app`
- [x] 1.3 A base layout and a documentation layout with the navigation, in a plain readable style; Inter served from `site/` with its licence file
- [x] 1.4 A content collection for the documentation pages and `site/src/data/tested-on.json`
- [x] 1.5 `site/README.md`: how to run, build and check the site locally, and that content and layout are kept apart (design decision 2)

## 2. Sample decks

- [x] 2.1 `site/decks/<name>/urdeck-config.json` for at least three decks (design decision 7), with a neutral weather location and public shortcuts
- [x] 2.2 `scripts/Render-SiteDecks.ps1`: build the host, copy the output to a temporary folder, render each deck with `--snapshot --size 1100x3840` to `site/src/assets/decks/<name>.png`; fail when a snapshot fails or a file is missing
- [x] 2.3 **(owner)** Run the script on the machine with the panel, look at each picture (nothing personal, readings present, weather drawn) and commit them

## 3. Pages

- [x] 3.1 Landing page with the copy agreed while exploring, under the rules of design decision 9: what it is, why it exists (privacy first), the sample decks, the repository link, the "coming soon" download control that leads to the repository, links to the configuration and "Tested on" pages
- [x] 3.2 **(owner)** Read the landing copy and correct it; it is in the owner's voice
- [x] 3.3 Configuration overview: where the config file is, its top-level fields, pages and the dock, how a change is picked up (from the README's configuration section)
- [x] 3.4 One page per first-party widget (clock, stats, weather, shortcut): settings, values, an example, a picture. The shortcut page carries the README's network paragraph
- [x] 3.5 Themes page and backgrounds page: the user's view, each linking to `docs/themes.md` or `docs/backgrounds.md` on GitHub for the reference
- [x] 3.6 "Tested on" page from `tested-on.json`: the HYTE Y70 Touch entry (1100x3840, RTX 4090, Windows 11 Pro 26200, what was checked), the expectation for other monitors, USB LCD screens as not working, the link to the report form, the trademark line
- [x] 3.7 `.github/ISSUE_TEMPLATE/hardware-report.yml` (design decision 10)

## 4. Build checks

- [x] 4.1 Copy `docs/schemas/**` into the output under `/schemas/` at build time and fail when the copy differs from the source (design decision 6)
- [x] 4.2 An internal link check that fails the build on a link to a page that does not exist
- [x] 4.3 The third-party reference check of design decision 8 over the built output
- [x] 4.4 Check by hand, once: the landing page with JavaScript off, and with reduce-motion set; the network panel shows requests to the site only and no cookie

## 5. CI and deployment

- [x] 5.1 `ci.yml`: a `site` output on `changes` (`site/**`, `docs/schemas/**`, `.github/workflows/site-deploy.yml`), a `site` job on `ubuntu-latest` (`npm ci`, build with the checks of section 4), and `site` added to `ci-ok`'s `needs`
- [x] 5.2 `.github/workflows/site-deploy.yml` (design decision 4): push to `main` on the same paths and `workflow_dispatch`, `withastro/action` with `path: site`, `actions/deploy-pages`, write permissions on the deploy job only, third-party actions pinned as in `ci.yml`
- [x] 5.3 `.github/dependabot.yml`: an `npm` entry for `/site`, weekly, grouped, prefix `chore(deps)`
- [x] 5.4 Look at the dependency review's result on the pull request; if it rejects a build tool's licence, apply the mitigation in the design's risks and say so in the pull request

## 6. Repository documents

- [x] 6.1 `README.md`: remove the configuration reference that moved, link to the site for it, keep the quick start and the licence map
- [x] 6.2 `AGENTS.md`: `site/` in the layout, the site commands, and that a behaviour change to a widget's settings updates `site/src/content/docs/`; the same line in `.github/pull_request_template.md`
- [x] 6.3 `CONTRIBUTING.md`: how to work on the site, and how to add a "Tested on" entry
- [x] 6.4 `README.md` licence map and `site/`: say which licence the site's text, pictures and code are under **(owner decides)**
- [x] 6.5 `docs/ROADMAP.md` and `docs/BACKLOG.md`: the site, the "Tested on" page as the start of milestone 2's compatibility table, the hardware report template as done

## 7. Going live

- [x] 7.1 **(owner)** Verify `urdeck.app` for the `UrDeck` organisation
- [x] 7.2 **(owner)** Enable Pages on the repository with the source "GitHub Actions"
- [x] 7.3 After the merge, confirm the deploy workflow ran and the site is at the `github.io` address
- [x] 7.4 **(owner)** Add the DNS records (design decision 5), set the custom domain, and turn on "Enforce HTTPS" when the certificate is there
- [x] 7.5 Check the spec's scenarios against the live site: the apex and `www`, the schema address answering with the repository's file, the download control, the report link

## 8. Visual design (second pull request)

- [x] 8.1 **(owner)** Make the design in Claude Design from the brief: `proposal.md`, the merged copy, the rendered decks, the `glass` theme's palette, and the bounds in the spec ("Visitor Privacy", "Usable Without Script Or Motion")
- [x] 8.2 Apply the design to `site/src/layouts` and `site/src/components` without changing content files; a moving element only over a still picture and off under reduce-motion
- [x] 8.3 Repeat 4.3 and 4.4 on the result
- [x] 8.4 **(owner)** Look at the site on a desktop and a phone, then merge
