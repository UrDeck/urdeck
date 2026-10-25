## 1. Pages first (needed by the SignPath application)

- [ ] 1.1 Site: the app privacy statement page as website "App Privacy Statement" says (Open-Meteo, site icons, the update check, how to turn each off, no telemetry); check every network request in the engine, providers and host against it
- [ ] 1.2 Site: the code signing policy page as website "Code Signing Policy Page" says (SignPath attribution, what is signed, roles, approval rule, the rule that a release submitted to the Store is never deleted or replaced); footer link; `npm run build` passes
- [ ] 1.3 README: link both pages

## 2. SignPath Foundation

- [ ] 2.1 After 0.1 is published: apply to SignPath Foundation with the repository, the 0.1 release and the policy page; record the answer in `docs/BACKLOG.md`
- [ ] 2.2 On acceptance: the SignPath project, its artifact configuration, a release signing policy with the owner as the only approver, and origin verification for `UrDeck/urdeck`, `main` and `release.yml`
- [ ] 2.3 GitHub: a `release` environment limited to `main` with `SIGNPATH_API_TOKEN` as its secret and the organization, project and policy slugs as variables; `release.yml` runs its signing job in that environment

## 3. Spike: what needs signing and how

- [ ] 3.1 List every `.exe` and `.dll` of the published folder and of a packed release with its signature state (design decision 2); record the list and the unsigned third-party files in `docs/perf/signing.md`; ask SignPath about any third-party file to sign
- [ ] 3.2 Try shape A (design decision 1) on a throwaway version: a `--signTemplate` script that submits the batch to SignPath and waits; if origin verification refuses it, try shape B; record the result in design.md decision 1
- [ ] 3.3 `scripts/Test-ReleaseSignatures.ps1` (design decision 3) with a test against a folder holding a signed and an unsigned file

## 4. Release workflow

- [ ] 4.1 `release.yml` and `Pack-Release.ps1`: the chosen signing shape between publish and pack; the wait with a one-hour limit; the run fails without drafting when the request is rejected or times out; a run outside `main` stops before signing
- [ ] 4.2 The signature check after signing and after packing; an unsigned file fails the run and names the file
- [ ] 4.3 A throwaway signed release (`0.1.1-rc.1`): every file passes the check, the downloaded `Setup.exe` shows a valid signature in its properties; delete the draft afterwards

## 5. First signed release and the site

- [ ] 5.1 Release the next stable version signed; install it on a clean account from the browser download (no "unknown publisher"); `Test-ReleaseSignatures.ps1` on the downloaded assets
- [ ] 5.2 Site: remove the SmartScreen note from the download control (already offering `releases/latest/download/UrDeck-win-Setup.exe`, see `site-download-link`), add the Store link when it is live (website "Download State"); README and the installing page drop the unsigned SmartScreen paragraph

## 6. Microsoft Store

- [ ] 6.1 `Setup.exe --silent` of the signed release on a clean account: no window, installed, the deck starts
- [ ] 6.2 Partner Center: reserve the name, create an "EXE or MSI app", the versioned package URL with `--silent`, x64, the listing text and pictures from the site's media, category, age rating, the privacy statement's address, and the certification notes (self-updating under 10.2.9, no driver, how to see it work on any monitor)
- [ ] 6.3 Submit; record the result and any rejection reason in `docs/BACKLOG.md`; once live, link the listing from the landing page and the README
- [ ] 6.4 `CONTRIBUTING.md`: the per-release Store routine (stable releases only, the new versioned address, never delete or replace a submitted release) and approving a signing request

## 7. Wrap-up

- [ ] 7.1 `docs/BACKLOG.md` and `docs/ROADMAP.md`: milestone 2 part C (signing, the Store listing)
- [ ] 7.2 `openspec validate --all --strict`
